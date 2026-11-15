using DauChat.Shared.Enums;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.SandboxTest.Networking;

internal static class NetworkPacketSandboxTests
{
    public static async Task RunAsync()
    {
        Console.WriteLine("\n--- [TV2] Test Length-prefix framing ---");

        var sampleMessage = new MessageDto
        {
            ConversationId = "room_23CT1",
            SenderId = "sv_001",
            SenderName = "Sinh viên DAU",
            RoomId = "room_23CT1",
            IsRoomMessage = true,
            Content = "Xin chào DAU Chat Team 5!"
        };

        var packet = NetworkPacket.Create(
            PacketType.GroupMessage,
            senderId: sampleMessage.SenderId,
            payload: sampleMessage,
            targetId: sampleMessage.RoomId,
            requiresAck: true);

        byte[] framedBytes = packet.ToFramedBytes();
        using var memoryStream = new MemoryStream(framedBytes);
        NetworkPacket? decodedPacket = await NetworkPacket.ReadFramedAsync(memoryStream);
        MessageDto? decodedMessage = decodedPacket?.GetPayload<MessageDto>();

        Console.WriteLine($"[OK] Length-prefix framed bytes: {framedBytes.Length} bytes");
        Console.WriteLine($"[OK] Decoded PacketType: {decodedPacket?.Type}, Content: \"{decodedMessage?.Content}\"");
    }
}
