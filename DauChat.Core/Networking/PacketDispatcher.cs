using DauChat.Shared.Enums;
using DauChat.Shared.Networking;

namespace DauChat.Core.Networking;

/// <summary>
/// Bộ điều phối gói tin trung tâm (TV2).
/// Đọc PacketType và chuyển tiếp tự động sang dịch vụ của TV3, TV4 hoặc TV5.
/// </summary>
public class PacketDispatcher
{
    // Các sự kiện để TV3, TV4, TV5 "đăng ký" nhận gói tin
    public event Func<string, NetworkPacket, Task>? OnLoginRequest;
    public event Func<string, NetworkPacket, Task>? OnRoomRequest;
    public event Func<string, NetworkPacket, Task>? OnDirectMessage;
    public event Func<string, NetworkPacket, Task>? OnGroupMessage;
    public event Func<string, NetworkPacket, Task>? OnFileTransfer;

    public async Task DispatchAsync(string senderId, NetworkPacket packet)
    {
        try
        {
            switch (packet.Type)
            {
                // --- TV3: Tài khoản & Phòng/Lớp ---
                case PacketType.LoginRequest:
                case PacketType.LogoutRequest:
                case PacketType.GetProfileRequest:
                    if (OnLoginRequest != null) await OnLoginRequest(senderId, packet);
                    break;

                case PacketType.CreateRoomRequest:
                case PacketType.GetRoomMembersRequest:
                    if (OnRoomRequest != null) await OnRoomRequest(senderId, packet);
                    break;

                // --- TV4: Tin nhắn ---
                case PacketType.DirectMessage:
                    if (OnDirectMessage != null) await OnDirectMessage(senderId, packet);
                    break;

                case PacketType.GroupMessage:
                    if (OnGroupMessage != null) await OnGroupMessage(senderId, packet);
                    break;

                // --- TV5: Truyền File ---
                case PacketType.FileTransferStart:
                case PacketType.FileChunk:
                case PacketType.FileTransferComplete:
                    if (OnFileTransfer != null) await OnFileTransfer(senderId, packet);
                    break;

                default:
                    Console.WriteLine($"[Dispatcher] Gói tin chưa được định tuyến: {packet.Type} từ {senderId}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dispatcher] Lỗi khi xử lý gói tin {packet.Type}: {ex.Message}");
        }
    }
}