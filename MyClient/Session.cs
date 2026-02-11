using System.Net.Sockets;

namespace MyClient
{
    public abstract class Session
    {
        // 통신의 주체
        Socket _socket;
        // 수신 관련
        SessionBuffer _recvBuffer = new SessionBuffer(65535);
        SocketAsyncEventArgs _recvArgs = new SocketAsyncEventArgs();
        SocketAsyncEventArgs _sendArgs = new SocketAsyncEventArgs();
        object _lock = new object();
        Queue<byte[]> _sendQueue = new Queue<byte[]>();
        bool _pending = false;
        int _disconnected;

        public void Init(Socket socket)
        {
            _socket = socket;

            _recvArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnRecvCompleted);

            _sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(OnSendCompleted);

            RegisterRecv(_recvArgs);
        }

        public void Send(byte[] bytesBuff)
        {
            lock (_lock)
            {
                _sendQueue.Enqueue(bytesBuff);
                if (_pending == false)
                {
                    RegisterSend();
                }
            }
        }

        void RegisterSend()
        {
            List<ArraySegment<byte>> list = new List<ArraySegment<byte>>();
            _pending = true;
            while (_sendQueue.Count > 0)
            {
                byte[] buff = _sendQueue.Dequeue();
                list.Add(new ArraySegment<byte>(buff, 0, buff.Length));
            }
            _sendArgs.BufferList = list;
            bool pending = _socket.SendAsync(_sendArgs);
            if (pending == false)
            {
                OnSendCompleted(null, _sendArgs);
            }
        }

        void OnSendCompleted(object sender, SocketAsyncEventArgs args)
        {
            lock (_lock)
            {
                if (args.SocketError == SocketError.Success)
                {
                    Console.WriteLine($"Send Success! Bytes: {args.BytesTransferred}");
                    if (_sendQueue.Count > 0)
                    {
                        RegisterSend(); // 남았으면 다음 전송
                    }
                    else
                    {
                        _pending = false;
                    }
                }
                else
                {
                    Disconnect();
                }

            }
        }

        public void RegisterRecv(SocketAsyncEventArgs args)
        {
            _recvBuffer.Clean();
            ArraySegment<byte> segment = _recvBuffer.WriteSegment;
            args.SetBuffer(segment.Array, segment.Offset, segment.Count);
            try
            {
                bool pending = _socket.ReceiveAsync(args);
                if (pending == false)
                {
                    OnRecvCompleted(null, args);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine($"RegisterRecv Failed {e}");
            }
        }

        void OnRecvCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.BytesTransferred > 0 && args.SocketError == SocketError.Success)
            {
                if (_recvBuffer.OnWrite(args.BytesTransferred) == false)
                {
                    Disconnect();
                    return;
                }
                int ProcessLen = ProcessPacket(_recvBuffer.ReadSegment);
                if (_recvBuffer.OnRead(ProcessLen) == false)
                {
                    Disconnect();
                    return;
                }
                RegisterRecv(args);

            }
            else
            {
                Disconnect();
            }
        }

        int ProcessPacket(ArraySegment<byte> buff)
        {
            int ProcessedLen = 0;
            while (true)
            {
                if (buff.Count < 2)
                {
                    break;
                }

                ushort DataSize = BitConverter.ToUInt16(buff.Array, buff.Offset);

                if (buff.Count < DataSize)
                {
                    break;
                }

                OnRecvPacket(new ArraySegment<byte>(buff.Array, buff.Offset, DataSize));

                ProcessedLen += DataSize;

                buff = new ArraySegment<byte>(buff.Array, buff.Offset + DataSize, buff.Count - DataSize);
            }

            return ProcessedLen;
        }

        public abstract void OnRecvPacket(ArraySegment<byte> buffer);

        void Disconnect()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) == 1) return;

            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
                Console.WriteLine("Disconnected!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Disconnect Error: {e}");
            }
        }

    }
}