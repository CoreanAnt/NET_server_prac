using MyServer.Network;

namespace MyServer.Room
{
    public class Room
    {
        // 들어온 유저들 목록
        List<Session> _sessions = new List<Session>();
        
        JobQueue _jobQueue = new JobQueue(); 

        // 외부(패킷핸들러)에서는 이 함수를 사용하여 작업.(캡슐화)
        public void Push(Action job)
        {
            _jobQueue.Push(job);
        }

        public void Enter(Session session)
        {
            _sessions.Add(session);
            Console.WriteLine($"User Entered. Total: {_sessions.Count}");
        }

        public void Leave(Session session)
        {
            _sessions.Remove(session);
        }

        public void Broadcast(Google.Protobuf.IMessage packet)
        {
            foreach (Session s in _sessions)
            {
                s.Send(packet);
            }
        }
    }
}