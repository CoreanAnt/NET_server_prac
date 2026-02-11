using System.Net;
using System.Net.Sockets;

namespace MyClient
{
    public class Connector
    {
        // 접속 처리가 끝나면 실행될 콜백 함수
        Action<Socket> _OnConnected;

        public void Connect(IPEndPoint endPoint, Action<Socket> action)
        {
            Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _OnConnected = action;

            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.RemoteEndPoint = endPoint;

            args.Completed += OnconnectCompleted;

            RegisterConnect(args, socket);

        }

        void RegisterConnect(SocketAsyncEventArgs args, Socket socket)
        {
            // 비동기 연결 요청
            // 리턴값이 false면 바로 완료된 것이므로 직접 핸들러 호출
            bool pending = socket.ConnectAsync(args);
            if(pending == false)
                OnconnectCompleted(null, args);

        }

        void OnconnectCompleted(object sender, SocketAsyncEventArgs args)
        {
            if(args.SocketError == SocketError.Success)
            {
                Console.WriteLine("Connect Success");
                _OnConnected.Invoke(args.ConnectSocket);
            }
            else
            {
                Console.WriteLine($"Connect Fail {args.SocketError}");
            }
        }
    }
}
