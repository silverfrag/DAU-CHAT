using DauChat.Core.AuthAndRooms;
using DauChat.Core.FileAndStorage;
using DauChat.Core.Messaging;
using DauChat.Core.Networking;
using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== DAU CHAT SANDBOX TEST (TV2 - TV5) ===\n");

// ════════════════════════════════════════════════════════════════════════
// 1. Khởi tạo 4 Services theo đúng ranh giới kiến trúc
// ════════════════════════════════════════════════════════════════════════
INetworkService networkService = new NetworkService();
IAuthRoomService authRoomService = new AuthRoomService(networkService);
IMessagingService messagingService = new MessagingService(networkService, authRoomService);
IFileStorageService fileStorageService = new FileStorageService(networkService);

Console.WriteLine($"[OK] Khởi tạo: {networkService.GetType().Name}, {authRoomService.GetType().Name}, " +
                  $"{messagingService.GetType().Name}, {fileStorageService.GetType().Name}");

// ════════════════════════════════════════════════════════════════════════
// 2. Kiểm thử Length-prefix framing của NetworkPacket (TV2)
// ════════════════════════════════════════════════════════════════════════
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

// ════════════════════════════════════════════════════════════════════════
// 3. TEST TV4 — AppendMessageToLocalLogAsync (MOD_07_01)
//    Ghi nối tiếp .jsonl + .idx, kiểm tra chống trùng UUID
// ════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n--- [TV4] Test AppendMessageToLocalLogAsync (MOD_07_01) ---");

const string testUserId = "sv_test_001";
const string testPartnerId = "gv_test_001";

// Dọn dẹp dữ liệu test cũ
var testDataRoot = Path.Combine("LocalData", testUserId);
if (Directory.Exists(testDataRoot))
    Directory.Delete(testDataRoot, recursive: true);

// Tạo một tin nhắn test
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

// Thử ghi lại cùng MessageId — phải trả về false (Deduplication)
bool appended1Dup = await messagingService.AppendMessageToLocalLogAsync(testUserId, msg1);
Console.WriteLine($"[OK] Ghi trùng UUID: {appended1Dup} (kỳ vọng: False — chống trùng lặp)");

// Ghi thêm 4 tin nhắn nữa
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
    bool ok = await messagingService.AppendMessageToLocalLogAsync(testUserId, msg);
    Console.WriteLine($"[OK] Ghi tin nhắn {i}: {ok}");
}

// Kiểm tra file .jsonl và .idx đã được tạo
var convId = $"dm_{new[] { testUserId, testPartnerId }.OrderBy(x => x, StringComparer.Ordinal).First()}_{new[] { testUserId, testPartnerId }.OrderBy(x => x, StringComparer.Ordinal).Last()}";
var monthStr = DateTime.UtcNow.ToString("yyyy-MM");
var jsonlPath = Path.Combine("LocalData", testUserId, "Messages", convId, $"{monthStr}.jsonl");
var idxPath   = Path.Combine("LocalData", testUserId, "Messages", convId, $"{monthStr}.idx");

bool jsonlExists = File.Exists(jsonlPath);
bool idxExists   = File.Exists(idxPath);
int lineCount = jsonlExists ? File.ReadAllLines(jsonlPath).Length : 0;
int idxCount  = idxExists  ? File.ReadAllLines(idxPath).Length  : 0;

Console.WriteLine($"[OK] File .jsonl tồn tại: {jsonlExists}, số dòng: {lineCount} (kỳ vọng: 5)");
Console.WriteLine($"[OK] File .idx tồn tại: {idxExists}, số record: {idxCount} (kỳ vọng: 5)");

// ════════════════════════════════════════════════════════════════════════
// 4. TEST TV4 — GetDirectHistoryAsync (MOD_03_05 / MOD_07_02)
//    Lazy-load phân trang từ dưới lên
// ════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n--- [TV4] Test GetDirectHistoryAsync / Lazy-load phân trang (MOD_03_05, MOD_07_02) ---");

var page0 = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, pageIndex: 0, pageSize: 3);
Console.WriteLine($"[OK] Trang 0 (3 tin mới nhất): {page0.Count} tin (kỳ vọng: 3)");
foreach (var m in page0)
    Console.WriteLine($"     → [{m.SentAtClientUtc:HH:mm:ss}] {m.SenderName}: {m.Content}");

var page1 = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, pageIndex: 1, pageSize: 3);
Console.WriteLine($"[OK] Trang 1 (tiếp theo): {page1.Count} tin (kỳ vọng: 2)");
foreach (var m in page1)
    Console.WriteLine($"     → [{m.SentAtClientUtc:HH:mm:ss}] {m.SenderName}: {m.Content}");

var pageEmpty = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, pageIndex: 2, pageSize: 3);
Console.WriteLine($"[OK] Trang 2 (hết dữ liệu): {pageEmpty.Count} tin (kỳ vọng: 0)");

// ════════════════════════════════════════════════════════════════════════
// 5. TEST TV4 — ProcessIncomingPacketAsync (MOD_03_04)
//    Xử lý gói tin đến, lưu local và bắn Event
// ════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n--- [TV4] Test ProcessIncomingPacketAsync (MOD_03_04) ---");

bool directEventFired = false;
messagingService.DirectMessageReceived += (sender, msg) =>
{
    directEventFired = true;
    Console.WriteLine($"[EVENT] DirectMessageReceived: \"{msg.Content}\" từ {msg.SenderId}");
};

var incomingMsg = new MessageDto
{
    MessageId = Guid.NewGuid().ToString("N"),
    ConversationId = $"dm_{testUserId}_{testPartnerId}",
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
    requiresAck: false); // requiresAck=false tránh gọi SendAckAsync lúc không có kết nối

await messagingService.ProcessIncomingPacketAsync(testUserId, incomingPacket);
Console.WriteLine($"[OK] Event DirectMessageReceived bắn: {directEventFired} (kỳ vọng: True)");

// Thử gửi lại cùng packet — phải không bắn Event lần 2 (Deduplication)
bool secondFire = false;
messagingService.DirectMessageReceived += (_, _) => secondFire = true;
await messagingService.ProcessIncomingPacketAsync(testUserId, incomingPacket);
Console.WriteLine($"[OK] Duplicate packet bị chặn: {!secondFire} (kỳ vọng: True — không bắn Event lần 2)");

// ════════════════════════════════════════════════════════════════════════
// 6. TEST TV4 — PinnedAnnouncement Event (MOD_04_14)
//    Xử lý gói ghim thông báo đến
// ════════════════════════════════════════════════════════════════════════
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
    targetId: "room_23CT1",
    requiresAck: false);

await messagingService.ProcessIncomingPacketAsync(testUserId, pinPacket);
Console.WriteLine($"[OK] Event PinnedAnnouncementChanged bắn: {pinEventFired} (kỳ vọng: True)");
Console.WriteLine($"[OK] RoomId khớp: {pinnedRoomId == "room_23CT1"} (kỳ vọng: True)");

// ════════════════════════════════════════════════════════════════════════
// 7. TEST TV4 — DeleteDirectHistoryAsync (MOD_03_06)
// ════════════════════════════════════════════════════════════════════════
Console.WriteLine("\n--- [TV4] Test DeleteDirectHistoryAsync (MOD_03_06) ---");
var deleteResult = await messagingService.DeleteDirectHistoryAsync(testUserId, testPartnerId);
Console.WriteLine($"[OK] Xóa lịch sử 1-1: {deleteResult.Success} — {deleteResult.Message}");

// Kiểm tra file đã bị xóa
bool folderDeleted = !Directory.Exists(Path.Combine("LocalData", testUserId, "Messages", convId));
Console.WriteLine($"[OK] Thư mục đã bị xóa: {folderDeleted} (kỳ vọng: True)");

// Đọc lịch sử sau khi xóa phải trả về rỗng
var afterDelete = await messagingService.GetDirectHistoryAsync(testUserId, testPartnerId, 0, 10);
Console.WriteLine($"[OK] Lịch sử sau xóa: {afterDelete.Count} tin (kỳ vọng: 0)");

// ════════════════════════════════════════════════════════════════════════
// Dọn dẹp dữ liệu test
// ════════════════════════════════════════════════════════════════════════
if (Directory.Exists("LocalData"))
    Directory.Delete("LocalData", recursive: true);

Console.WriteLine("\n=== TẤT CẢ KIỂM THỬ TV4 HOÀN THÀNH ✅ ===");
