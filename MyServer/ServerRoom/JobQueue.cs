namespace MyServer
{
    // 일감을 보관하고 실행하는 큐
    public class JobQueue
    {
        // 처리할 일들 큐
        Queue<Action> _jobQueue = new Queue<Action>();
        // 자물쇠 (참조형객체)
        object _lock = new object();
        // 현재 실행 중인지 체크
        bool _flush = false; 

        // 외부에서 일감을 던져넣는 함수 (모든 스레드가 접근 가능 -> Lock 필요)
        public void Push(Action job)
        {
            // 현재 상태
            bool flush = false;

            // 스레드 하나만 처리가능
            lock (_lock)
            {
                // 큐에 job을 넣음.
                _jobQueue.Enqueue(job);
                
                // 지금 아무도 일감을 처리하고 있지 않다면, 내가 처리 권한을 가져감
                if (_flush == false)
                    flush = _flush = true;
            }

            // 처리 권한을 얻은 사람만 Flush 실행
            if (flush)
                Flush();
        }

        // 쌓인 작업을 실제로 처리하는 함수 (한 번에 한 놈만 실행됨을 보장)
        void Flush()
        {
            while (true)
            {
                Action action = null;

                lock (_lock)
                {
                    if (_jobQueue.Count == 0)
                    {
                        _flush = false; // 큐에 없음.
                        return;
                    }

                    // 큐에서 하나 꺼냄
                    action = _jobQueue.Dequeue();
                }

                // Lock 밖에서 실행! (lock 내부이면 함수 실행동안 다른 스레드가 push를 못함.)
                action.Invoke();
            }
        }
    }
}