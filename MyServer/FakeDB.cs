namespace MyServer
{
    public class FakeDB
    {
        // 비동기 함수 (async Task)
        public static async Task<bool> CheckLoginUserAsync(long playerId, string token)
        {
            // DB에 다녀오는 척 0.1초(100ms) 대기 (다른것 계속 처리하고 있음)
            await Task.Delay(100);

            return true;
        }
    }
}