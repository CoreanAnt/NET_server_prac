using MyServer.Protocol;
using Google.Protobuf;

namespace MyClient;

public class GSession : Session
{
    public override void OnRecvPacket(ArraySegment<byte> buffer)
    {
        ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + 2);
        switch ((PacketId)id)
        {
            case PacketId.SLoginResponse:
                SLoginResponse response = new SLoginResponse();
                response.MergeFrom(buffer.Array, buffer.Offset + 4, buffer.Count - 4);

                Console.WriteLine($"Recv PacketID: {id}, Size: {buffer.Count} {response}");
                break;
            
            case PacketId.SChat:
                SChat chat = new SChat();
                chat.MergeFrom(buffer.Array, buffer.Offset + 4, buffer.Count - 4);

                Console.WriteLine($"Recv Chat: {chat}");
                break;

        }
    }
}
