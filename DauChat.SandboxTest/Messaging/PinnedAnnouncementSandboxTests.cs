using DauChat.Shared.Enums;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.SandboxTest.Messaging;

internal static class PinnedAnnouncementSandboxTests
{
    public static async Task RunAsync(SandboxTestContext context)
    {
        Console.WriteLine("\n--- [TV4] Test PinAnnouncementBroadcast (MOD_04_11, MOD_04_14) ---");

        bool pinEventFired = false;
        string? pinnedRoomId = null;
        context.MessagingService.PinnedAnnouncementChanged += (_, eventArgs) =>
        {
            pinEventFired = true;
            pinnedRoomId = eventArgs.RoomId;
            Console.WriteLine($"[EVENT] PinnedAnnouncementChanged: RoomId={eventArgs.RoomId}, " +
                              $"Content=\"{eventArgs.PinnedNotice?.Content}\"");
        };

        var pinNotice = new PinnedNoticeDto
        {
            MessageId = Guid.NewGuid().ToString("N"),
            RoomId = "room_23CT1",
            PinnedByUserId = "gv_001",
            PinnedByName = "Giảng viên Kiến trúc",
            Content = "🔔 Thông báo thi cuối kỳ: 15/01/2027 phòng A101",
            PinnedAtUtc = DateTime.UtcNow
        };

        var pinPacket = NetworkPacket.Create(
            PacketType.PinAnnouncementBroadcast,
            senderId: "gv_001",
            payload: pinNotice,
            targetId: pinNotice.RoomId,
            requiresAck: false);

        await context.MessagingService.ProcessIncomingPacketAsync(context.TestUserId, pinPacket);
        Console.WriteLine($"[OK] Event PinnedAnnouncementChanged bắn: {pinEventFired} (kỳ vọng: True)");
        Console.WriteLine($"[OK] RoomId khớp: {pinnedRoomId == pinNotice.RoomId} (kỳ vọng: True)");
    }
}
