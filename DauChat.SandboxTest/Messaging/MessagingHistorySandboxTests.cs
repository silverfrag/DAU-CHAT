using DauChat.Shared.Enums;
using DauChat.Shared.Models;

namespace DauChat.SandboxTest.Messaging;

internal static class MessagingHistorySandboxTests
{
    public static async Task RunAsync(SandboxTestContext context)
    {
        var messagingService = context.MessagingService;
        var testUserId = context.TestUserId;
        var testPartnerId = context.TestPartnerId;

        Console.WriteLine("\n--- [TV4] Test AppendMessageToLocalLogAsync (MOD_07_01) ---");

        var msg1 = new MessageDto
        {
            MessageId = Guid.NewGuid().ToString("N"),
            ConversationId = context.ConversationId,
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
            var message = new MessageDto
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

            bool appended = await messagingService.AppendMessageToLocalLogAsync(testUserId, message);
            Console.WriteLine($"[OK] Ghi tin nhắn {i}: {appended}");
        }

        var month = msg1.SentAtClientUtc.ToString("yyyy-MM");
        var messageDirectory = Path.Combine(
            context.TestDataRoot,
            "Messages",
            context.ConversationId);
        var jsonlPath = Path.Combine(messageDirectory, $"{month}.jsonl");
        var indexPath = Path.Combine(messageDirectory, $"{month}.idx");

        bool jsonlExists = File.Exists(jsonlPath);
        bool indexExists = File.Exists(indexPath);
        int lineCount = jsonlExists ? File.ReadAllLines(jsonlPath).Length : 0;
        int indexCount = indexExists ? File.ReadAllLines(indexPath).Length : 0;

        Console.WriteLine($"[OK] File .jsonl tồn tại: {jsonlExists}, số dòng: {lineCount} (kỳ vọng: 5)");
        Console.WriteLine($"[OK] File .idx tồn tại: {indexExists}, số record: {indexCount} (kỳ vọng: 5)");

        Console.WriteLine("\n--- [TV4] Test GetDirectHistoryAsync / Lazy-load phân trang (MOD_03_05, MOD_07_02) ---");

        var page0 = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, pageIndex: 0, pageSize: 3);
        Console.WriteLine($"[OK] Trang 0 (3 tin mới nhất): {page0.Count} tin (kỳ vọng: 3)");
        foreach (var message in page0)
            Console.WriteLine($"     → [{message.SentAtClientUtc:HH:mm:ss}] {message.SenderName}: {message.Content}");

        var page1 = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, pageIndex: 1, pageSize: 3);
        Console.WriteLine($"[OK] Trang 1 (tiếp theo): {page1.Count} tin (kỳ vọng: 2)");
        foreach (var message in page1)
            Console.WriteLine($"     → [{message.SentAtClientUtc:HH:mm:ss}] {message.SenderName}: {message.Content}");

        var pageEmpty = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, pageIndex: 2, pageSize: 3);
        Console.WriteLine($"[OK] Trang 2 (hết dữ liệu): {pageEmpty.Count} tin (kỳ vọng: 0)");

        File.Delete(indexPath);
        var recoveredPage = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, pageIndex: 0, pageSize: 3);
        int recoveredIndexCount = File.ReadAllLines(indexPath).Length;
        Console.WriteLine($"[OK] Phục hồi .idx: {recoveredIndexCount} record, trang đọc được {recoveredPage.Count} tin (kỳ vọng: 5, 3)");

        Console.WriteLine("\n--- [TV4] Test DeleteDirectHistoryAsync (MOD_03_06) ---");
        var deleteResult = await messagingService.DeleteDirectHistoryAsync(testUserId, testPartnerId);
        Console.WriteLine($"[OK] Xóa lịch sử 1-1: {deleteResult.Success} — {deleteResult.Message}");

        bool folderDeleted = !Directory.Exists(messageDirectory);
        Console.WriteLine($"[OK] Thư mục đã bị xóa: {folderDeleted} (kỳ vọng: True)");

        var afterDelete = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, 0, 10);
        Console.WriteLine($"[OK] Lịch sử sau xóa: {afterDelete.Count} tin (kỳ vọng: 0)");
    }
}
