using System;
using System.IO;
using System.Text.RegularExpressions;

namespace PacketGenerator
{
    class Program
    {
        static string genFile = "PacketManager.cs";
        static string protoFile = "MyServer.proto"; // 읽을 대상

        // 패킷 매니저 껍데기
        static string fileFormat =
@"using Google.Protobuf;
using MyServer.Protocol;
using MyServer.Network;
using MyServer;
using System.Buffers.Binary; // BinaryPrimitives 사용

public class PacketManager
{{
    const ushort HeaderSize = 4;

    // 싱글톤 패턴(static 선언 Instace로 붙여서 호출해야함.)
    public static PacketManager Instance {{ get; }} = new PacketManager();

    // 딕셔너리<키,값>, Action
    Dictionary<ushort, Action<Session, ArraySegment<byte>, ushort>> _onRecv = new Dictionary<ushort, Action<Session, ArraySegment<byte>, ushort>>();
    Dictionary<ushort, Action<Session, IMessage>> _handler = new Dictionary<ushort, Action<Session, IMessage>>();
    
    // 패킷 종류(Type)를 주면 ID(ushort)를 알려주는 딕셔너리
    Dictionary<Type, ushort> _msgId = new Dictionary<Type, ushort>();

    public void Register()
    {{
        {0}
    }}

    // 패킷 종류로 ID 찾기
    public ushort GetPacketId(IMessage packet)
    {{
        if (_msgId.TryGetValue(packet.GetType(), out ushort id))
            return id;
        return 0;
    }}

    //세션과 버퍼가 들어오면 헤더읽고, 해당 Action 호출.
    public void OnRecvPacket(Session session, ArraySegment<byte> buffer)
    {{
        // Span 사용으로 메모리 안전성 및 성능 확보
        ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(buffer.Array, buffer.Offset, buffer.Count);

        // 리틀 엔디안 명시 (서버간 통신이나 플랫폼 차이 고려)
        ushort count = 0;
        ushort size = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(count, 2));
        count += 2;
        ushort id = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(count, 2));
        count += 2;

        // 패킷 처리 함수를 담을 변수 선언
        Action<Session, ArraySegment<byte>, ushort> action = null;

        // 딕셔너리에서 id에 해당하는 함수를 찾아서(TryGetValue), action에 할당(out) (MakePacket의 형식과 action이 같음)
        if (_onRecv.TryGetValue(id, out action))
            action.Invoke(session, buffer, id);
    }}

    //여기가 하나의 함수로 여러 종류 패킷 처리 가능하게 하는 함수
    void MakePacket<T>(Session session, ArraySegment<byte> buffer, ushort id) where T : IMessage, new()
    {{
        T pkt = new T();
        // 보낼 때부터 프로토버프(Protobuf) 규격으로 포장해서 보냈기 때문에, 받는 쪽에서도 프로토버프(MergeFrom)로 풀 수 있음
        // HeaderSize(4) 만큼 건너뛰고 본문만 파싱
        pkt.MergeFrom(buffer.Array, buffer.Offset + HeaderSize, buffer.Count - HeaderSize);
        // 객체 조립 완료, 이제 핸들러로 넘기면 (pkt.이름)처럼 꺼내 쓸 수 있음

        if (_handler.TryGetValue(id, out var action))
            action.Invoke(session, pkt);
    }}
}}
";

        static void Main(string[] args)
        {
            string registerCode = "";

            // 파일 읽기
            if (!File.Exists(protoFile))
            {
                Console.WriteLine($"[에러] {protoFile} 파일이 없습니다!");
                return;
            }

            string[] lines = File.ReadAllLines(protoFile);

            foreach (string line in lines)
            {
                // message 파싱
                if (line.Trim().StartsWith("message"))
                {
                    string packetName = Regex.Match(line, @"message\s+(\w+)").Groups[1].Value;

                    // "C"로 시작하는 패킷 자동 등록 (서버 입장에서 받을 것들)
                    if (packetName.StartsWith("C"))
                    {
                        // 예시: 구글 Protobuf가 C# 코드를 만들 때 "CLoginRequest"로 만들어주므로,
                        string enumName = packetName;

                        // ({0}: Enum 이름, {1}: 패킷 클래스 이름)
                        // 예시 CLoginRequest -> PacketId.CLoginRequest 로 매칭
                        Console.WriteLine($"[등록] {packetName} -> PacketId.{enumName}");
                        registerCode += string.Format(@"_onRecv.Add((ushort)PacketId.{0}, MakePacket<{1}>);
                                                        _handler.Add((ushort)PacketId.{0}, PacketHandler.{1}Handler);", enumName, packetName);
                        registerCode += Environment.NewLine;
                    }
                    // "S"로 시작하는 패킷 자동 등록 (서버에서 보내는 패킷)
                    else if (packetName.StartsWith("S"))
                    {
                        string enumName = packetName;
                        // 예시 SLoginResponse 타입은 PacketId.SLoginResponse 로 매칭
                        Console.WriteLine($"[등록] {packetName} -> PacketId.{enumName}");
                        registerCode += string.Format("_msgId.Add(typeof({1}), (ushort)PacketId.{0});", enumName, packetName);
                        registerCode += Environment.NewLine;
                    }
                }
            }

            string fileContent = string.Format(fileFormat, registerCode);
            File.WriteAllText(genFile, fileContent);
            Console.WriteLine($"[완료] {genFile} 생성됨.");
        }
    }
}