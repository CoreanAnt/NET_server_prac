using System.Net;
using System.Net.Sockets;
using MyServer.Network;
using MyServer.Room;

class Program
{
    // 외부 클래스 가져와서 객체로 만듦
    static Listener _listener = new Listener();
    // 외부에서 Program.Room으로 접근할 수 있게 public static으로 변경하고 { get; } 읽기 전용으로 구현
    public static Room Room { get; } = new Room();

    static void Main(string[] args)
    {
        // string host = Dns.GetHostName(); // 장치이름 얻기
        // IPHostEntry ipHost = Dns.GetHostEntry(host); // IP주소들 얻기
        // // IPv4로 특정 (AddressFamily:주소체계)
        // IPAddress ipAddr = null;
        // foreach (var addr in ipHost.AddressList)
        // {
        //     if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        //     {
        //         ipAddr = addr;
        //         break;
        //     }
        // }
        ///////////////////IP주소 얻는 방식으로 하려다가 말음(로컬이면 어차피 127)////////////////////////////////
        
        // 먼저 패킷 초기화.
        PacketManager.Instance.Register();

        IPAddress ipAddr = IPAddress.Parse("127.0.0.1");
        //포트 설정 (엔드포인트 주소 설정)
        IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777);
        
        // 연결시 로직
        void OnClientConnect(Socket clientSocket)
        {
            try
            {
                Session session = new Session();
                session.Start(clientSocket);

                Console.WriteLine($"OnClientConnect: {clientSocket.RemoteEndPoint}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"OnClientConnect Error: {e}");
            }
        }

        // 서버 초기화
        _listener.Init(endPoint, OnClientConnect);
        Console.WriteLine("Listening");

        while (true)
        {
            // 메인 스레드는 죽지 않게 유지 (비동기 방식이니)
            Thread.Sleep(100);
        }
    }
}
