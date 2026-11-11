using DauChat.Shared.Enums;

namespace DauChat.Shared.Models;

/// <summary>
/// DTO thống kê vận hành và lưu lượng của Socket Server cho Admin Dashboard (MOD_08_01 -> MOD_08_04).
/// </summary>
public sealed class ServerStatsDto
{
    public bool IsRunning { get; set; }
    public string ListenIpAddress { get; set; } = "0.0.0.0";
    public int Port { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public TimeSpan Uptime => IsRunning && StartedAtUtc.HasValue ? DateTime.UtcNow - StartedAtUtc.Value : TimeSpan.Zero;

    public int OnlineClientsCount { get; set; }
    public int PendingQueueCount { get; set; }
    public long TotalPacketsSent { get; set; }
    public long TotalPacketsReceived { get; set; }
    public long TotalBytesSent { get; set; }
    public long TotalBytesReceived { get; set; }

    public List<ClientConnectionDto> ConnectedClients { get; set; } = new();
    public List<ConnectionLogDto> RecentLogs { get; set; } = new();
}

/// <summary>
/// Thông tin một phiên kết nối Client đang active trên Server (MOD_02_03, MOD_08_03).
/// </summary>
public sealed class ClientConnectionDto
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string IpEndPoint { get; set; } = string.Empty;
    public PresenceStatus Status { get; set; } = PresenceStatus.Online;
    public DateTime ConnectedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;
    public long PacketsSent { get; set; }
    public long PacketsReceived { get; set; }
}

/// <summary>
/// Nhật ký kết nối / ngắt kết nối cơ bản của Server (MOD_08_04).
/// </summary>
public sealed class ConnectionLogDto
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string IpEndPoint { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

