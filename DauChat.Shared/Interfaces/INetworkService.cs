using DauChat.Shared.Enums;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.Shared.Interfaces;

/// <summary>
/// Hợp đồng tầng Mạng &amp; Socket Server Core do TV2 (Phan Trung Nghĩa) phụ trách.
/// Bao phủ Phân hệ 2 (MOD_02_01 -> MOD_02_07) và Phân hệ 8 (MOD_08_01 -> MOD_08_04).
/// TV3, TV4, TV5 BẮT BUỘC gửi/nhận gói tin thông qua Interface này, không tự tạo TcpClient/TcpListener riêng.
/// </summary>
public interface INetworkService
{
    #region Thuộc tính trạng thái
    bool IsConnected { get; }
    bool IsServerRunning { get; }
    string? CurrentUserId { get; }
    #endregion

    #region Client Socket & Truyền gói tin (MOD_02_01 -> MOD_02_04, MOD_02_07)
    /// <summary>MOD_02_01: Kết nối Client tới Socket Server theo chuẩn Length-prefix.</summary>
    Task<OperationResult> ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    /// <summary>MOD_02_02: Ngắt kết nối Client chủ động và giải phóng tài nguyên phiên.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>MOD_02_01, MOD_02_03: Gửi một NetworkPacket bất đồng bộ qua kết nối hiện tại.</summary>
    Task<OperationResult> SendPacketAsync(NetworkPacket packet, CancellationToken cancellationToken = default);

    /// <summary>MOD_02_03: Gửi Request packet và chờ Response khớp CorrelationId.</summary>
    Task<NetworkPacket?> SendAndWaitAsync(
        NetworkPacket request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_02_04: Gửi tín hiệu Heartbeat duy trì trạng thái Online.</summary>
    Task SendHeartbeatAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>MOD_02_07: Gửi gói ACK báo đã nhận thành công để Server xóa khỏi PendingQueue.</summary>
    Task SendAckAsync(string userId, string acknowledgedPacketId, CancellationToken cancellationToken = default);
    #endregion

    #region Server Core, Transient Relay & Quản trị Server (MOD_02_03 -> MOD_02_07, MOD_08_01 -> MOD_08_04)
    /// <summary>MOD_08_01: Khởi động dịch vụ TCP Socket Server tại IP/Port chỉ định.</summary>
    Task<OperationResult> StartServerAsync(string listenIp, int port, CancellationToken cancellationToken = default);

    /// <summary>MOD_08_01: Dừng dịch vụ Socket Server và đóng toàn bộ phiên Client.</summary>
    Task<OperationResult> StopServerAsync(CancellationToken cancellationToken = default);

    /// <summary>MOD_08_02: Lấy thông số vận hành hiện tại của Server (IP, Port, Uptime, lưu lượng).</summary>
    Task<ServerStatsDto> GetServerStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>MOD_08_03: Lấy danh sách Client đang kết nối trực tuyến kèm IP và trạng thái.</summary>
    Task<IReadOnlyList<ClientConnectionDto>> GetConnectedClientsAsync(CancellationToken cancellationToken = default);

    /// <summary>MOD_08_04: Lấy danh sách nhật ký kết nối / ngắt kết nối cơ bản.</summary>
    Task<IReadOnlyList<ConnectionLogDto>> GetConnectionLogsAsync(
        int maxEntries = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// MOD_02_03, MOD_02_06: Chuyển tiếp gói tin tới User đích (RAM-to-RAM nếu Online, hoặc xếp PendingQueue FIFO+TTL nếu Offline).
    /// </summary>
    Task<OperationResult> SendToUserAsync(
        string targetUserId,
        NetworkPacket packet,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_02_03, MOD_02_06: Phát tán gói tin tới danh sách User trong phòng.</summary>
    Task<OperationResult> BroadcastToUsersAsync(
        IEnumerable<string> targetUserIds,
        NetworkPacket packet,
        CancellationToken cancellationToken = default);

    /// <summary>MOD_01_08, MOD_02_02: Ngắt cưỡng bức phiên kết nối của User (khi bị Admin khóa tài khoản).</summary>
    Task<OperationResult> KickUserAsync(string userId, string reason, CancellationToken cancellationToken = default);

    /// <summary>MOD_02_07: Dọn các gói tin đã quá hạn TTL trong PendingQueue.</summary>
    Task<int> CleanupExpiredPendingPacketsAsync(CancellationToken cancellationToken = default);
    #endregion

    #region Sự kiện bất đồng bộ (Event-driven cho TV1, TV3, TV4, TV5)
    /// <summary>MOD_02_01, MOD_02_03: Phát sự kiện khi PacketDispatcher nhận được gói tin mới.</summary>
    event EventHandler<NetworkPacket>? PacketReceived;

    /// <summary>MOD_02_01, MOD_02_02: Phát sự kiện khi trạng thái kết nối Client <-> Server thay đổi.</summary>
    event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>MOD_02_04, MOD_02_05: Phát sự kiện khi trạng thái Online/Offline của một User thay đổi.</summary>
    event EventHandler<(string UserId, PresenceStatus Status)>? UserPresenceChanged;

    /// <summary>MOD_08_04: Phát sự kiện khi có nhật ký kết nối/ngắt kết nối mới trên Server.</summary>
    event EventHandler<ConnectionLogDto>? ConnectionLogAdded;

    /// <summary>MOD_08_02, MOD_08_04: Phát sự kiện khi bộ đếm lưu lượng / trạng thái Server cập nhật.</summary>
    event EventHandler<ServerStatsDto>? ServerStatsUpdated;
    #endregion
}

