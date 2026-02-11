using MyServer.Network;
using MyServer.Protocol;
using Google.Protobuf;

namespace MyServer
{
    class PacketHandler
    {
        // 로그인 요청
        public static async void CLoginRequestHandler(Session session, IMessage packet)
        {
            // IMessage에서 CLoginRequest 꺼내기
            CLoginRequest req = packet as CLoginRequest;

            // Proto에 정의된 필드 출력
            Console.WriteLine($"PlayerId: {req.PlayerId}, Name: {req.Name}");

            // DB 갔다오는 척 (비동기)
            bool success = await FakeDB.CheckLoginUserAsync(req.PlayerId, req.Token);
            if (success)
            {
                // 람다식 안의 코드는 당장 실행되는 게 아니라, 나중에 Room 스레드가 꺼내서 실행함
                Program.Room.Push(() =>
                {
                    //클라이언트 정보 세션에 저장
                    session.PlayerId = (int)req.PlayerId; 
                    session.Name = req.Name;
                    Program.Room.Enter(session);
                    // 클라이언트에게 패킷 보내기
                    SLoginResponse res = new SLoginResponse();
                    res.Success = true;
                    res.MyPlayerId = req.PlayerId;
                    session.Send(res);
                });
            }
        }

        // 채팅 요청
        public static void CChatHandler(Session session, IMessage packet)
        {
            CChat req = packet as CChat;

            Program.Room.Push(() =>
            {
                // SChat 패킷을 만들어서 방송해야 함
                SChat res = new SChat();
                res.PlayerId = session.PlayerId; // 테스트용 ID
                res.Message = req.Message;

                Program.Room.Broadcast(res); // 문자열이 아닌 패킷을 넘김
            });
        }
    }
}