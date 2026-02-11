using System.Net.Sockets;
using Google.Protobuf;

namespace MyServer.Network
{
    public class Session
    {
        //클라이언트 식별을 위해 속성 추가.
        public int PlayerId { get; set; } 
        public string Name { get; set; }

        Socket _socket;
        // 플래그 설정(Interlocked.Exchange에 사용)
        int _disconnected = 0;
        // 수신 버퍼 (TCP는 패킷이 뭉치거나 쪼개질 수 있어 버퍼링 필요)
        SessionBuffer _recvBuffer = new SessionBuffer(65535);

        // 비동기 Send를 위한 큐와 Lock
        object _lock = new object(); //한 스레드만 접근
        Queue<byte[]> _sendQueue = new Queue<byte[]>(); //패킷 배열 큐
        bool _pending = false; // 현재 전송 중인지 여부
        SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs(); // 전송 데이터

        public void Start(Socket socket)
        {
            _socket = socket;

            //듣는건 Start안에 정의(바로 들어야 하기 때문에)
            SocketAsyncEventArgs recvArgs = new SocketAsyncEventArgs();

            //ReceiveAsync용 이벤트 핸들러 등록
            recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);

            // SendAsync용 이벤트 핸들러 등록
            _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendCompleted);

            RegisterRecv(recvArgs);
        }

        void RegisterRecv(SocketAsyncEventArgs args)
        {
            // 처리 데이터 지우고 남은 데이터 앞으로 땡기는 코드
            _recvBuffer.Clean();
            // 세그먼트사용 이유: 복사x 및 배열주소, 시작위치, 길이가 있음.
            // 남은 쓰기 가능한 공간 리턴
            ArraySegment<byte> segment = _recvBuffer.WriteSegment;

            // 빈 공간 주소를 args에 전달
            args.SetBuffer(segment.Array, segment.Offset, segment.Count);

            try
            {
                // 클라이언트 소켓이 args에 데이터를 작성하기를 대기. 
                bool pending = _socket.ReceiveAsync(args);
                // 대기없이 바로 처리하게 되면 Completed가 실행 안됨.
                if (pending == false)
                    OnRecvCompleted(null, args);
            }
            catch (Exception e)
            {
                Console.WriteLine($"RegisterRecv Error: {e}");
            }
        }

        void OnRecvCompleted(object sender, SocketAsyncEventArgs args)
        {
            // 0바이트 수신은 연결 종료를 의미(args.BytesTransferred: 정확한 데이터 크기)
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                try
                {
                    // 쓰기커서 옮김.
                    if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                    {
                        Disconnect(); // 남은 공간보다 많이 썼다.
                        return;
                    }
                    // 유효 데이터 처리
                    int processLen = ProcessPacket(_recvBuffer.ReadSegment);

                    // 읽기 커서 옮김.
                    if (_recvBuffer.OnRead(processLen) == false)
                    {
                        Disconnect(); // 있는 것보다 더 많이 읽었다.
                        return;
                    }

                    RegisterRecv(args); // 계속 (구조: start-register-completed-register-completed......)
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Recv Failed {e}");
                }
            }
            else
            {
                Disconnect();
            }
        }

        // 패킷 파싱 로직 (PacketManager 사용)
        int ProcessPacket(ArraySegment<byte> buffer)
        {
            int processLen = 0;

            while (true)
            {
                // 전체 들어온 버퍼크기 - 현재 처리한 데이터크기
                int remainingBytes = buffer.Count - processLen;
                // 일단 헤더 크기는 사이즈2+아이디2.
                if (remainingBytes < 4) break;

                // 앞부분 헤더 확인 코드
                ushort dataSize = BitConverter.ToUInt16(buffer.Array, buffer.Offset + processLen);
                if (remainingBytes < dataSize) break;

                // 현재 처리할 패킷 조각만 잘라서 넘겨줌
                ArraySegment<byte> packetData = new ArraySegment<byte>(buffer.Array, buffer.Offset + processLen, dataSize);

                // // 제너레이터가 만들어준 코드에서 ID를 확인하고 적절한 핸들러 호출
                PacketManager.Instance.OnRecvPacket(this, packetData);

                processLen += dataSize;
            }

            return processLen;
        }

        // Protobuf 메시지를 패킷 형식(헤더+바이트)으로 만드는 함수
        public void Send(IMessage packet)
        {
            // 제너레이터가 만들어준 코드에서 ID를 찾음
            ushort packetId = PacketManager.Instance.GetPacketId(packet);

            if (packetId == 0)
            {
                Console.WriteLine("ID를 찾을 수 없는 패킷입니다!");
                return;
            }

            // Protobuf -> 바이트 배열 직렬화
            byte[] bodyData = packet.ToByteArray();
            ushort size = (ushort)(bodyData.Length + 4); // 헤더 크기(4) 포함

            // 헤더 + 바디 합치기
            byte[] sendBuffer = new byte[size];

            // Size(2) + ID(2)
            Array.Copy(BitConverter.GetBytes(size), 0, sendBuffer, 0, 2);
            Array.Copy(BitConverter.GetBytes(packetId), 0, sendBuffer, 2, 2);

            // 바디 작성
            Array.Copy(bodyData, 0, sendBuffer, 4, bodyData.Length);

            // 전송
            Send(sendBuffer);
        }

        // 전송하는 함수
        public void Send(byte[] sendBuff)
        {
            lock (_lock)
            {
                // 1. 보낼 데이터를 큐에 넣음
                _sendQueue.Enqueue(sendBuff);

                // 2. 만약 현재 전송 중인 게 없다면, 바로 전송 시작
                if (_pending == false)
                    RegisterSend();
            }
        }

        void RegisterSend()
        {
            // _pending 상태를 true로 변경 (전송 시작) 
            _pending = true;

            // 1개씩 보내는 코드
            // byte[] buff = _sendQueue.Dequeue();
            // _sendArgs.SetBuffer(buff, 0, buff.Length);

            // 리스트로 한꺼번에 보내는 코드
            // 큐에 있는 모든 패킷을 꺼내서 리스트에 담음 (복사 X, 참조만 이동)
            List<ArraySegment<byte>> list = new List<ArraySegment<byte>>();
            while (_sendQueue.Count > 0)
            {
                byte[] buff = _sendQueue.Dequeue();
                list.Add(new ArraySegment<byte>(buff, 0, buff.Length));
            }

            // BufferList에 할당 (한 방에 보낼 준비)
            _sendArgs.BufferList = list;

            bool pending = _socket.SendAsync(_sendArgs);
            if (pending == false)
                OnSendCompleted(null, _sendArgs);
        }

        void OnSendCompleted(object sender, SocketAsyncEventArgs args)
        {
            lock (_lock)
            {
                if (args.SocketError == SocketError.Success)
                {
                    // 보낼 게 더 남았는지 확인
                    if (_sendQueue.Count > 0)
                    {
                        RegisterSend(); // 남았으면 다음 전송
                    }
                    else
                    {
                        _pending = false; // 다 보냈으면 대기 상태로
                    }
                }
                else
                {
                    Disconnect();
                }
            }
        }

        // Disconnect는 여러곳에서 일어날 수 있기에 중복을 조심해야한다.
        public void Disconnect()
        {
            // 중복 방지
            if (Interlocked.Exchange(ref _disconnected, 1) == 1) return;
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
                Console.WriteLine("Disconnected!");
            }
            catch(Exception e)
            {
                Console.WriteLine($"Disconnect Error: {e}");
            }
            Program.Room.Push(() =>
            {
                Program.Room.Leave(this);
            });
        }
    }
}