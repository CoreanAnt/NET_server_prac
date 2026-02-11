using System.Net;
using System.Net.Sockets;

namespace MyServer.Network
{
    public class Listener
    {
        // 소켓 정의
        Socket _listenSocket;
        // 콜백 함수(클라이언트 성공 접속 이후 행동 저장)
        Action<Socket> _onAcceptHandler;

        // 소켓 생성 및 설정
        public void Init(IPEndPoint endPoint, Action<Socket> onAcceptHandler)
        {
            // 소켓 생성(매개변수:IPv4,IPv6인지 /TCP사용(TCP는 Stream으로 해야함))
            _listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            // onAcceptHandler은 Init 안에서 저장해두고, 나중에 접속하면 실행될 콜백 함수
            _onAcceptHandler = onAcceptHandler;

            // 소켓에 구체적인 주소,포트 붙이는 작업
            _listenSocket.Bind(endPoint);

            // 연결요청 운영체제에 알림(매개변수는 대기 가능 크기)
            _listenSocket.Listen(1000);

            // 비동기 재사용 가능 객체 생성 (SocketAsyncEventArgs: Buffer, AcceptSocket, Completed, UserToken)
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            // 비동기 객체 완료시 OnAcceptCompleted함수 실행.
            // 이벤트 헨들러 문법 EventHandler<T> 이면 ()안에 들어갖는 두번째 매개변수는 T타입을 받아야 한다.
            args.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);
            //비동기 모드 접속 대기 시작 (처음)
            RegisterAccept(args);
        }

        void RegisterAccept(SocketAsyncEventArgs args)
        {
            // 재사용을 위해 초기화
            args.AcceptSocket = null;

            // 리스너 소켓이 args라는 빈 컨테이너를 운영체제에 주면서, 소켓 데이터 달라는 코드.
            bool pending = _listenSocket.AcceptAsync(args);
            // 운영체제가 true라고 하면 알아서 이벤트를 발생시키지만, false를 반환하면 내가 수동으로 해야 함.(true 대기중. 보통 대기중임)
            // 당연하지만 false시 처리가 끝난후 반환, true시 처리전 반환
            if (pending == false)
                OnAcceptCompleted(null, args);
        }

        // sender: 이벤트를 발생시킨 객체 (재사용하므로 항상 같은 주소값, 즉 args 자신)
        // args: 실제 접속된 클라이언트 정보(AcceptSocket)와 결과가 담긴 데이터 가방
        void OnAcceptCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (args.SocketError == SocketError.Success)
            {
                // 클라이언트 접속 처리 중 에러가 발생해도 서버는 죽지 않아야 함
                try
                {
                    _onAcceptHandler.Invoke(args.AcceptSocket);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Listener] Accept Handling Error: {e}");
                }
            }
            else
            {
                Console.WriteLine($"[Listener] Socket Error: {args.SocketError}");
            }
            // 다음 사람을 위해 다시 대기 모드 진입
            RegisterAccept(args);
        }
    }
}

//여기까지의 진행흐름은 
//메인서버 소켓생성 -> _listenSocket.AcceptAsync(args)비동기 대기진행() -> OnAcceptCompleted실행 -> _onAcceptHandler.Invoke -> 다시 메인으로 가서 OnClientConnect확인 -> 재호출