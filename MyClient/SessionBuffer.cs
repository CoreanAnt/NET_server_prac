namespace MyClient
{
    public class SessionBuffer
    {
        // 실제 데이터가 담길 메모리(ArraySegment구조체: 주소, 시작위치(Offset), 길이 포함(Count))
        ArraySegment<byte> _buffer;

        // 유효한 데이터의 시작 위치 (Read 커서)
        int _readPos;
        // 다음 데이터를 받을 시작 위치 (Write 커서)
        int _writePos;

        public SessionBuffer(int bufferSize)
        {
            // bufferSize만큼의 넉넉한 배열 생성 (보통 1024 ~ 65535 사이 사용하라고 함 (1KB-64KB 사이 사용))
            _buffer = new ArraySegment<byte>(new byte[bufferSize], 0, bufferSize);
        }

        // get은 C#에서 생성,호출시 ()를 사용안하기에 복잡한 계산보다는 상태정보등을 가져올때 사용한다.
        // 현재 쌓여있는 유효 데이터의 크기 (Write - Read)
        public int DataSize { get { return _writePos - _readPos; } }

        // 앞으로 채워넣을 수 있는 남은 공간 (전체 - Write)
        public int FreeSize { get { return _buffer.Count - _writePos; } }

        // 유효한 데이터 세그먼트를 리턴(바로 참조해서 사용 가능하지만 구조체이기에 가벼워 새로 만들어도 부하 없다.)
        public ArraySegment<byte> ReadSegment
        {
            get { return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _readPos, DataSize); }
        }

        // 소켓이 데이터를 쓸 수 있는 빈 공간 세그먼트 리턴
        public ArraySegment<byte> WriteSegment
        {
            get { return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _writePos, FreeSize); }
        }
        //동일한 버퍼를 사용하는게 핵심(복사x).


        // 버퍼 청소 (이미 사용한 앞부분 공간을 정리해서 재사용)
        public void Clean()
        {
            int dataSize = DataSize;

            if (dataSize == 0)
            {
                // 남은 데이터가 없으면 그냥 커서를 처음으로 리셋
                _readPos = _writePos = 0;
            }
            else
            {
                // 남은 데이터가 있으면 앞으로 당김
                // Array.Copy -> Buffer.BlockCopy (byte[] 전용 초고속 복사)
                // 매개변수: (원본배열, 원본바이트오프셋, 목적지배열, 목적지바이트오프셋, 복사할바이트수)
                Buffer.BlockCopy(_buffer.Array, _buffer.Offset + _readPos, _buffer.Array, _buffer.Offset, dataSize);
                _readPos = 0;
                _writePos = dataSize;
            }
        }


        // 소켓이 데이터를 받아서(Write) 성공하면, 커서를 뒤로 이동시킴
        public bool OnWrite(int count)
        {
            if (count > FreeSize)
                return false; // 남은 공간보다 많이 쓰려고 함

            _writePos += count;
            return true;
        }

        // Session이 데이터를 처리하고(Read) 나서, 커서를 뒤로 이동시킴
        public bool OnRead(int count)
        {
            if (count > DataSize)
                return false; // 가진 데이터보다 더 많이 읽으려 함

            _readPos += count;
            return true;
        }
    }
}