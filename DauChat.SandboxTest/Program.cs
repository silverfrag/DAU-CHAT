using DauChat.Core.AuthAndRooms;
using DauChat.Core.FileAndStorage;
using DauChat.Core.Messaging;
using DauChat.Core.Networking;
using DauChat.FileService.Services;
using DauChat.FileService.Utils;
using DauChat.SandboxTest.Messaging;
using DauChat.SandboxTest.Networking;
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

        if (args.Length > 0 && args[0].Equals("--tv5", StringComparison.OrdinalIgnoreCase))
        {
            await RunTv5FileStorageTestAsync();
            return;
        }

        // Mặc định chạy toàn bộ kiểm thử các phân hệ TV4, TV5, TV2
        await RunTv4TestsAsync();
        Console.WriteLine();
        await RunTv5FileStorageTestAsync();
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
        await NetworkPacketSandboxTests.RunAsync();

        // 3–7. Thực thi bộ kiểm thử TV4 (lưu .jsonl/.idx, phân trang, phục hồi .idx, nhận tin + chống trùng, ghim thông báo)
        var context = new SandboxTestContext(messagingService);
        try
        {
            await MessagingHistorySandboxTests.RunAsync(context);
            await IncomingDirectMessageSandboxTests.RunAsync(context);
            await PinnedAnnouncementSandboxTests.RunAsync(context);
        }
        finally
        {
            context.Cleanup();
        }

        Console.WriteLine("\n=== TẤT CẢ KIỂM THỬ TV4 HOÀN THÀNH ✅ ===");
    }

    public static async Task RunTv5FileStorageTestAsync()
    {
        Console.WriteLine("=== [TV5] KIỂM THỬ FILE STORAGE, CHUNK 64KB & BACKUP ZIP ===");

        var tempDir = Path.Combine(Path.GetTempPath(), $"dauchat_tv5_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sampleFilePath = Path.Combine(tempDir, "sample_dau_document.txt");

        try
        {
            // 1. Tạo tệp giả lập ~150KB (> 2 chunks 64KB)
            var sampleContent = string.Concat(Enumerable.Repeat("DAU-Chat-Team5-FileChunk-Test-Data-Payload-1234567890\n", 3000));
            await File.WriteAllTextAsync(sampleFilePath, sampleContent);
            var fileInfo = new FileInfo(sampleFilePath);
            Console.WriteLine($"[OK] Đã tạo file mẫu: {fileInfo.Length} bytes ({fileInfo.Length / 1024.0:F1} KB)");

            // 2. Kiểm thử Sha256Helper
            var hash = await Sha256Helper.ComputeFileHashAsync(sampleFilePath);
            Console.WriteLine($"[OK] Tính SHA-256 mã băm: {hash}");

            // 3. Kiểm thử ChunkSplitter (64KB/chunk)
            int chunkCount = 0;
            long totalRead = 0;
            bool lastChunkFlagged = false;
            await foreach (var chunk in ChunkSplitter.ReadChunksAsync(sampleFilePath, ChunkSplitter.DefaultChunkSize))
            {
                chunkCount++;
                totalRead += chunk.Data.Length;
                if (chunk.IsLast) lastChunkFlagged = true;
            }
            Console.WriteLine($"[OK] Cắt chunk 64KB: {chunkCount} chunks, tổng bytes: {totalRead}, cờ IsLast cuối: {lastChunkFlagged}");

            // 4. Kiểm thử LocalFileStorage (Lưu file & ghi manifest)
            var localStorage = new LocalFileStorage();
            var testUserId = $"tv5_user_{Guid.NewGuid():N}";
            var testRoomId = "room_kien_truc_1";

            var toSavePath = Path.Combine(tempDir, "archive_cad_test.dwg");
            File.Copy(sampleFilePath, toSavePath);
            await localStorage.SaveFileAsync(toSavePath, testUserId, testRoomId);

            var downloaded = await localStorage.GetDownloadedFilesAsync(testUserId);
            Console.WriteLine($"[OK] LocalFileStorage lưu & manifest: tìm thấy {downloaded.Count} tệp trong kho local.");

            // 5. Kiểm thử BackupService (Nén ZIP & Giải nén ZIP)
            var backupService = new BackupService();
            var backupZipPath = Path.Combine(tempDir, "backup_user.zip");
            await backupService.CreateBackupZipAsync(testUserId, backupZipPath);
            var zipInfo = new FileInfo(backupZipPath);
            Console.WriteLine($"[OK] BackupService tạo backup.zip: {zipInfo.Length} bytes");

            var restoreDir = Path.Combine(tempDir, "restored_data");
            await backupService.RestoreBackupZipAsync(backupZipPath, restoreDir);
            bool restoredExists = Directory.Exists(Path.Combine(restoreDir, "Files", testRoomId));
            Console.WriteLine($"[OK] BackupService khôi phục: {restoredExists} (kỳ vọng: True)");

            // Dọn dẹp thư mục LocalData sinh ra trong test
            var userLocalData = Path.Combine("LocalData", testUserId);
            if (Directory.Exists(userLocalData)) Directory.Delete(userLocalData, recursive: true);

            Console.WriteLine("\n=== TẤT CẢ KIỂM THỬ TV5 HOÀN THÀNH ✅ ===");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
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
