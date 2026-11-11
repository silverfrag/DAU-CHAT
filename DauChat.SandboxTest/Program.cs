using DauChat.Core.AuthAndRooms;
using DauChat.Core.FileAndStorage;
using DauChat.Core.Messaging;
using DauChat.Core.Networking;
using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

Console.WriteLine("=== DAU CHAT SANDBOX TEST (TV2 - TV5) ===");

// 1. Kiểm thử nhanh khởi tạo 4 Services theo đúng ranh giới kiến trúc
INetworkService networkService = new NetworkService();
IAuthRoomService authRoomService = new AuthRoomService(networkService);
IMessagingService messagingService = new MessagingService(networkService, authRoomService);
IFileStorageService fileStorageService = new FileStorageService(networkService);

Console.WriteLine($"[OK] Đã khởi tạo: {networkService.GetType().Name}, {authRoomService.GetType().Name}, {messagingService.GetType().Name}, {fileStorageService.GetType().Name}");

// 2. Kiểm thử Length-prefix framing của NetworkPacket (4 byte length + payload)
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
Console.WriteLine($"[OK] Decoded PacketType: {decodedPacket?.Type}, MessageId: {decodedMessage?.MessageId}, Content: \"{decodedMessage?.Content}\"");
