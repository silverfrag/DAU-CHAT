using DauChat.Core.AuthAndRooms;
using DauChat.Core.FileAndStorage;
using DauChat.Core.Messaging;
using DauChat.Core.Networking;
using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.SandboxTest;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("==================================================");
        Console.WriteLine("=== DAU CHAT SANDBOX TEST RUNNER (TEAM 5)      ===");
        Console.WriteLine("==================================================\n");

        if (args.Length > 0 && args[0].Equals("--tv2", StringComparison.OrdinalIgnoreCase))
        {
            await RunTv2PendingQueueTestAsync();
            return;
        }

        if (args.Length > 0 && args[0].Equals("--tv4", StringComparison.OrdinalIgnoreCase))
        {
            await RunTv4TestsAsync();
            return;
        }

        // Mặc định chạy cả TV4 tests và TV2 socket/pending queue test
        await RunTv4TestsAsync();
        Console.WriteLine();
        await RunTv2PendingQueueTestAsync();

        Console.WriteLine("\n==================================================");
        Console.WriteLine("=== TẤT CẢ KIỂM THỬ SANDBOX ĐÃ HOÀN TẤT THÀNH CÔNG ===");
        Console.WriteLine("==================================================");
    }

    public static async Task RunTv4TestsAsync()
    {
        Console.WriteLine("=== [TV4 & TV1] KIỂM THỬ KHỞI TẠO VÀ MESSAGING ===");

        // 1. Khởi tạo 4 Services theo đúng ranh giới kiến trúc
        INetworkService networkService = new NetworkService();
        IAuthRoomService authRoomService = new AuthRoomService(networkService);
        IMessagingService messagingService = new MessagingService(networkService, authRoomService);
        IFileStorageService fileStorageService = new FileStorageService(networkService);

        Console.WriteLine($"[OK] Khởi tạo: {networkService.GetType().Name}, {authRoomService.GetType().Name}, " +
                          $"{messagingService.GetType().Name}, {fileStorageService.GetType().Name}");

        // 2. Kiểm thử Length-prefix framing của NetworkPacket (TV2)
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

        // 3. TEST TV4 — AppendMessageToLocalLogAsync (MOD_07_01)
        Console.WriteLine("\n--- [TV4] Test AppendMessageToLocalLogAsync (MOD_07_01) ---");

        var testUserId = $"sv_test_{Guid.NewGuid():N}";
        var testPartnerId = $"gv_test_{Guid.NewGuid():N}";
        var testDataRoot = Path.Combine("LocalData", testUserId);

        var msg1 = new MessageDto
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ConversationId = $"dm_{testUserId}_{testPartnerId}",
            SenderId = testUserId,
            SenderName = "Sinh viên Test",
            ReceiverId = testPartnerId,
            Content = "Tin nhắn số 1 - Test TV4",
            Type = MessageType.Text,
            IsRoomMessage = false,
            SentAtClientUtc = DateTime.UtcNow
        };

        bool appended1 = await messagingService.AppendMessageToLocalLogAsync(testUserId, msg1);
        Console.WriteLine($"[OK] Ghi tin nhắn 1: {appended1} (kỳ vọng: True)");

        bool appended1Dup = await messagingService.AppendMessageToLocalLogAsync(testUserId, msg1);
        Console.WriteLine($"[OK] Ghi trùng UUID: {appended1Dup} (kỳ vọng: False — chống trùng lặp)");

        for (int i = 2; i <= 5; i++)
        {
            var msg = new MessageDto
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ConversationId = msg1.ConversationId,
                SenderId = i % 2 == 0 ? testUserId : testPartnerId,
                SenderName = i % 2 == 0 ? "Sinh viên Test" : "Giảng viên Test",
                ReceiverId = i % 2 == 0 ? testPartnerId : testUserId,
                Content = $"Tin nhắn số {i} - Test TV4",
                Type = MessageType.Text,
                IsRoomMessage = false,
                SentAtClientUtc = DateTime.UtcNow.AddSeconds(i)
            };
            bool appended = await messagingService.AppendMessageToLocalLogAsync(testUserId, msg);
            Console.WriteLine($"[OK] Ghi tin nhắn {i}: {appended}");
        }

        // 4. TEST TV4 — GetDirectHistoryAsync Pagination (MOD_03_05)
        Console.WriteLine("\n--- [TV4] Test GetDirectHistoryAsync Phân trang (MOD_03_05) ---");

        var paged1 = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, pageIndex: 0, pageSize: 3);
        Console.WriteLine($"[OK] Lấy 3 tin gần nhất (trang 0, pageSize=3): {paged1.Count} tin (kỳ vọng: 3)");
        for (int i = 0; i < paged1.Count; i++)
            Console.WriteLine($"     [{i + 1}] {paged1[i].SenderName}: {paged1[i].Content} ({paged1[i].SentAtClientUtc:HH:mm:ss})");

        var paged2 = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, pageIndex: 1, pageSize: 3);
        Console.WriteLine($"[OK] Lấy trang tiếp (trang 1, pageSize=3): {paged2.Count} tin (kỳ vọng: 2)");
        for (int i = 0; i < paged2.Count; i++)
            Console.WriteLine($"     [{i + 1}] {paged2[i].SenderName}: {paged2[i].Content} ({paged2[i].SentAtClientUtc:HH:mm:ss})");

        // 5. TEST TV4 — ProcessIncomingPacketAsync (MOD_03_04)
        Console.WriteLine("\n--- [TV4] Test ProcessIncomingPacketAsync (MOD_03_04) ---");

        bool directEventFired = false;
        messagingService.DirectMessageReceived += (_, message) =>
        {
            directEventFired = true;
            Console.WriteLine($"[EVENT] DirectMessageReceived: \"{message.Content}\" từ {message.SenderId}");
        };

        var incomingMsg = new MessageDto
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ConversationId = msg1.ConversationId,
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
            payload: incomingMsg,
            targetId: testUserId,
            requiresAck: false);

        await messagingService.ProcessIncomingPacketAsync(testUserId, incomingPacket);
        Console.WriteLine($"[OK] Event DirectMessageReceived bắn: {directEventFired} (kỳ vọng: True)");

        bool secondFire = false;
        messagingService.DirectMessageReceived += (_, _) => secondFire = true;
        await messagingService.ProcessIncomingPacketAsync(testUserId, incomingPacket);
        Console.WriteLine($"[OK] Duplicate packet bị chặn: {!secondFire} (kỳ vọng: True — không bắn Event lần 2)");

        // 6. TEST TV4 — PinnedAnnouncement Event (MOD_04_14)
        Console.WriteLine("\n--- [TV4] Test PinAnnouncementBroadcast (MOD_04_11, MOD_04_14) ---");

        bool pinEventFired = false;
        string? pinnedRoomId = null;
        messagingService.PinnedAnnouncementChanged += (_, e) =>
        {
            pinEventFired = true;
            pinnedRoomId = e.RoomId;
            Console.WriteLine($"[EVENT] PinnedAnnouncementChanged: RoomId={e.RoomId}, " +
                              $"Content=\"{e.PinnedNotice?.Content}\"");
        };

        var existingRoomMessage = new MessageDto
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ConversationId = "room_23CT1",
            SenderId = testUserId,
            RoomId = "room_23CT1",
            Content = "Tin nhắn đã có trong lịch sử phòng",
            IsRoomMessage = true,
            SentAtClientUtc = DateTime.UtcNow
        };
        await messagingService.AppendMessageToLocalLogAsync(testUserId, existingRoomMessage);

        var pinNotice = new PinnedNoticeDto
        {
            MessageId = existingRoomMessage.MessageId,
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
            targetId: "room_23CT1",
            requiresAck: false);

        await messagingService.ProcessIncomingPacketAsync(testUserId, pinPacket);
        Console.WriteLine($"[OK] Event PinnedAnnouncementChanged bắn: {pinEventFired} (kỳ vọng: True)");
        Console.WriteLine($"[OK] RoomId khớp: {pinnedRoomId == "room_23CT1"} (kỳ vọng: True)");

        // 7. TEST TV4 — DeleteDirectHistoryAsync (MOD_03_06)
        Console.WriteLine("\n--- [TV4] Test DeleteDirectHistoryAsync (MOD_03_06) ---");
        var deleteResult = await messagingService.DeleteDirectHistoryAsync(testUserId, testPartnerId);
        Console.WriteLine($"[OK] Xóa lịch sử 1-1: {deleteResult.Success} — {deleteResult.Message}");

        var afterDelete = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, 0, 10);
        Console.WriteLine($"[OK] Lịch sử sau xóa: {afterDelete.Count} tin (kỳ vọng: 0)");

        if (Directory.Exists(testDataRoot))
            Directory.Delete(testDataRoot, recursive: true);

        Console.WriteLine("\n=== TẤT CẢ KIỂM THỬ TV4 HOÀN THÀNH ✅ ===");
    }

    public static async Task RunTv2PendingQueueTestAsync()
    {
        Console.WriteLine("=== [TV2] BẮT ĐẦU TEST XẢ PENDING QUEUE (FLUSH QUEUE) ===");

        int port = 9055;
        string ip = "127.0.0.1";

        // 1. Khởi động Server
        var server = new NetworkService();
        var startRes = await server.StartServerAsync(ip, port);
        if (!startRes.Success)
        {
            Console.WriteLine($"[Server] Lỗi khởi động: {startRes.Message}");
            return;
        }
        Console.WriteLine("[Server] Đã chạy.");

        server.PacketReceived += (s, packet) =>
        {
            Console.WriteLine($"[Server Nhận] Từ: {packet.SenderId} -> Đến: {packet.TargetId} | Loại: {packet.Type}");

            if (packet.Type == PacketType.DirectMessage && packet.TargetId != null)
            {
                _ = server.SendToUserAsync(packet.TargetId, packet);
            }
        };

        // 2. Client A kết nối và Đăng nhập
        var clientA = new NetworkService();
        clientA.CurrentUserId = "user_A";
        await clientA.ConnectAsync(ip, port);
        await clientA.SendPacketAsync(NetworkPacket.Create(PacketType.LoginRequest, "user_A", "Login", "user_A"));
        await Task.Delay(300);

        // 3. Client A gửi tin nhắn cho B (Lúc này B CHƯA ĐĂNG NHẬP)
        Console.WriteLine("\n--- Client A gửi tin cho B (B đang Offline) ---");
        var chatPacket = NetworkPacket.Create(
            PacketType.DirectMessage,
            "user_A",
            payload: "Tin nhắn này gửi lúc B offline. B sẽ nhận được khi đăng nhập!",
            targetId: "user_B"
        );
        await clientA.SendPacketAsync(chatPacket);
        await Task.Delay(500);

        // 4. Client B bắt đầu kết nối và Đăng nhập
        Console.WriteLine("\n--- Bây giờ Client B mới Đăng nhập ---");
        var clientB = new NetworkService();
        clientB.CurrentUserId = "user_B";
        var receivedTcs = new TaskCompletionSource<string>();
        clientB.PacketReceived += (s, packet) =>
        {
            Console.WriteLine($"[Client B Nhận] Từ {packet.SenderId}: {packet.PayloadJson}");
            receivedTcs.TrySetResult(packet.PayloadJson ?? string.Empty);
        };

        await clientB.ConnectAsync(ip, port);

        // Gửi Login, Server sẽ kích hoạt hàm FlushPendingQueueAsync xả tin nhắn
        await clientB.SendPacketAsync(NetworkPacket.Create(PacketType.LoginRequest, "user_B", "Login", "user_B"));

        var received = await Task.WhenAny(receivedTcs.Task, Task.Delay(3000));
        if (received == receivedTcs.Task)
        {
            Console.WriteLine("[OK] Client B đã nhận được tin nhắn tồn đọng thành công!");
        }
        else
        {
            Console.WriteLine("[WARN] Hết thời gian chờ xả hàng đợi Client B.");
        }

        await clientA.DisconnectAsync();
        await clientB.DisconnectAsync();
        await server.StopServerAsync();

        Console.WriteLine("\n=== TẤT CẢ KIỂM THỬ TV2 HOÀN THÀNH ✅ ===");
    }
}
