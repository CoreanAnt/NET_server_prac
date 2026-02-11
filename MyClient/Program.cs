using System.Net;
using System.Net.Sockets;
using MyClient;
using Google.Protobuf;
using MyServer.Protocol;

class Program
{
    static Connector _Connector = new Connector();

    static GSession _GSession = new GSession();


    static void Main()
    {
        IPAddress IP = IPAddress.Parse("127.0.0.1");
        IPEndPoint endPoint = new IPEndPoint(IP, 7777);

        //간단 id 보내느 로직
        Console.WriteLine("please enter id");
        int id = Int32.Parse(Console.ReadLine());

        _Connector.Connect(endPoint, Onconnect);

        void Onconnect(Socket socket)
        {
            _GSession.Init(socket);
            CLoginRequest packet = new CLoginRequest();
            packet.PlayerId = id;
            packet.Name = "Lee";

            byte[] bodyData = packet.ToByteArray();
            ushort size = (ushort)(bodyData.Length + 4);
            ushort packetId = (ushort)PacketId.CLoginRequest;

            byte[] sendBuff = new byte[size];

            Array.Copy(BitConverter.GetBytes(size), 0, sendBuff, 0, 2);
            Array.Copy(BitConverter.GetBytes(packetId), 0, sendBuff, 2, 2);
            Array.Copy(bodyData, 0, sendBuff, 4, bodyData.Length);

            _GSession.Send(sendBuff);
        }

        while (true)
        {
            //여기서 서버에 메시지를 보낸다.
            string msg = Console.ReadLine();
            if (msg != "")
            {
                CChat chat = new CChat();
                chat.Message = msg;
                byte[] bodyMsg = chat.ToByteArray();
                ushort size = (ushort)(bodyMsg.Length + 4);
                ushort pId = (ushort)PacketId.CChat;

                byte[] sendBuff = new byte[size];

                Array.Copy(BitConverter.GetBytes(size), 0, sendBuff, 0, 2);
                Array.Copy(BitConverter.GetBytes(pId), 0, sendBuff, 2, 2);

                Array.Copy(bodyMsg, 0, sendBuff, 4, bodyMsg.Length);
                _GSession.Send(sendBuff);
            }

            Thread.Sleep(100);
        }
    }

}
