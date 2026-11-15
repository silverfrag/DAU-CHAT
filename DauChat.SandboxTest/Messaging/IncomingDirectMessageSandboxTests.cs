using DauChat.Shared.Enums;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.SandboxTest.Messaging;

internal static class IncomingDirectMessageSandboxTests
{
    public static async Task RunAsync(SandboxTestContext context)
    {
        var messagingService = context.MessagingService;
        var testUserId = context.TestUserId;
        var testPartnerId = context.TestPartnerId;

        Console.WriteLine("\n--- [TV4] Test ProcessIncomingPacketAsync (MOD_03_04) ---");

        bool directEventFired = false;
        messagingService.DirectMessageReceived += (_, message) =>
        {
            directEventFired = true;
            Console.WriteLine($"[EVENT] DirectMessageReceived: \"{message.Content}\" từ {message.SenderId}");
        };

        var incomingMessage = new MessageDto
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ConversationId = context.ConversationId,
            SenderId = testPartnerId,
            SenderName = "Giảng viên Test",
            ReceiverId = testUserId,
            Content = "Tin nhắn đến từ giảng viên!",
            Type = MessageType.Text,
            IsRoomMessage = false,
            SentAtClientUtc = DateTime.UtcNow
        };

        var incomingPacket = NetworkPacket.Create(
            PacketType.DirectMessage,
            senderId: testPartnerId,
            payload: incomingMessage,
            targetId: testUserId,
            requiresAck: false);

        await messagingService.ProcessIncomingPacketAsync(testUserId, incomingPacket);
        Console.WriteLine($"[OK] Event DirectMessageReceived bắn: {directEventFired} (kỳ vọng: True)");

        bool secondFire = false;
        messagingService.DirectMessageReceived += (_, _) => secondFire = true;
        await messagingService.ProcessIncomingPacketAsync(testUserId, incomingPacket);
        Console.WriteLine($"[OK] Duplicate packet bị chặn: {!secondFire} (kỳ vọng: True — không bắn Event lần 2)");
    }
}
