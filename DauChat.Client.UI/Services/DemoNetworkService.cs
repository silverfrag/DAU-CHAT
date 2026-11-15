using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.Client.UI.Services;

internal sealed class DemoNetworkService : INetworkService
{
    private readonly DemoDataStore _store;

    public DemoNetworkService(DemoDataStore store)
    {
        _store = store;
        _store.ExternalSyncApplied += diff =>
        {
            foreach (var log in diff.NewLogs)
            {
                ConnectionLogAdded?.Invoke(this, log);
            }
        };
    }

    public bool IsConnected => _store.IsConnected;
    public bool IsServerRunning => _store.IsServerRunning;
    public string? CurrentUserId { get; private set; }

    public event EventHandler<NetworkPacket>? PacketReceived;
    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<(string UserId, PresenceStatus Status)>? UserPresenceChanged;
    public event EventHandler<ConnectionLogDto>? ConnectionLogAdded;
    public event EventHandler<ServerStatsDto>? ServerStatsUpdated;

    public void SetCurrentUser(string? userId) => CurrentUserId = userId;

    public async Task<OperationResult> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        await Task.Delay(160, cancellationToken);

        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
        {
            return OperationResult.Fail("Địa chỉ IP hoặc cổng TCP Server không hợp lệ.", "INVALID_ENDPOINT");
        }

        _store.CheckDiskUpdates();
        if (!_store.IsServerRunning)
        {
            return OperationResult.Fail(
                $"Không thể kết nối tới TCP Server tại {host}:{port}. Dịch vụ máy chủ hiện đang tắt.",
                "SERVER_UNREACHABLE");
        }

        lock (_store.SyncRoot)
        {
            _store.ConnectedHost = host.Trim();
            _store.ConnectedPort = port;
            _store.IsConnected = true;
        }

        ConnectionStateChanged?.Invoke(this, true);
        return OperationResult.Ok($"Đã nối kênh TCP {host}:{port}");
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            _store.IsConnected = false;
            CurrentUserId = null;
        }

        ConnectionStateChanged?.Invoke(this, false);
        return Task.CompletedTask;
    }

    public Task<OperationResult> SendPacketAsync(NetworkPacket packet, CancellationToken cancellationToken = default)
    {
        _store.MutateAndSave(() =>
        {
            _store.TotalPacketsSent++;
            _store.TotalBytesSent += 512;
        });
        PacketReceived?.Invoke(this, packet);
        return Task.FromResult(OperationResult.Ok());
    }

    public Task<NetworkPacket?> SendAndWaitAsync(NetworkPacket request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => Task.FromResult<NetworkPacket?>(null);

    public Task SendHeartbeatAsync(string userId, CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            var conn = _store.ConnectedClients.FirstOrDefault(c => c.UserId == userId);
            if (conn is not null)
            {
                conn.LastHeartbeatUtc = DateTime.UtcNow;
            }
        }
        return Task.CompletedTask;
    }

    public Task SendAckAsync(string userId, string acknowledgedPacketId, CancellationToken cancellationToken = default)
    {
        _store.MutateAndSave(() =>
        {
            if (_store.PendingQueueCount > 0) _store.PendingQueueCount--;
        });
        return Task.CompletedTask;
    }

    public async Task<OperationResult> StartServerAsync(string listenIp, int port, CancellationToken cancellationToken = default)
    {
        if (port is < 1 or > 65535)
        {
            return OperationResult.Fail("Cổng TCP Server phải nằm trong khoảng 1 – 65535.");
        }

        await Task.Delay(120, cancellationToken);
        var (log, stats) = _store.MutateAndSave(() =>
        {
            _store.IsServerRunning = true;
            _store.ServerListenIp = string.IsNullOrWhiteSpace(listenIp) ? "0.0.0.0" : listenIp.Trim();
            _store.ServerListenPort = port;
            _store.ServerStartedAtUtc = DateTime.UtcNow;
            var newLog = new ConnectionLogDto
            {
                TimestampUtc = DateTime.UtcNow,
                EventType = "SERVER_START",
                IpEndPoint = $"{_store.ServerListenIp}:{_store.ServerListenPort}",
                Description = $"Admin khởi động TCP Socket Listener tại {_store.ServerListenIp}:{_store.ServerListenPort}."
            };
            _store.ConnectionLogs.Insert(0, newLog);
            return (newLog, BuildStatsSnapshot());
        });

        ConnectionLogAdded?.Invoke(this, log);
        ServerStatsUpdated?.Invoke(this, stats);
        return OperationResult.Ok("Đã khởi động Socket Server");
    }

    public async Task<OperationResult> StopServerAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        var (log, stats) = _store.MutateAndSave(() =>
        {
            _store.IsServerRunning = false;
            var newLog = new ConnectionLogDto
            {
                TimestampUtc = DateTime.UtcNow,
                EventType = "SERVER_STOP",
                IpEndPoint = $"{_store.ServerListenIp}:{_store.ServerListenPort}",
                Description = "Admin đã tạm dừng dịch vụ Socket Server."
            };
            _store.ConnectionLogs.Insert(0, newLog);
            return (newLog, BuildStatsSnapshot());
        });

        ConnectionLogAdded?.Invoke(this, log);
        ServerStatsUpdated?.Invoke(this, stats);
        return OperationResult.Ok("Đã dừng Socket Server");
    }

    public Task<ServerStatsDto> GetServerStatsAsync(CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult(BuildStatsSnapshot());
        }
    }

    public Task<IReadOnlyList<ClientConnectionDto>> GetConnectedClientsAsync(CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<ClientConnectionDto>>(_store.ConnectedClients.ToList());
        }
    }

    public Task<IReadOnlyList<ConnectionLogDto>> GetConnectionLogsAsync(int maxEntries = 100, CancellationToken cancellationToken = default)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<ConnectionLogDto>>(_store.ConnectionLogs.Take(maxEntries).ToList());
        }
    }

    public Task<OperationResult> SendToUserAsync(string targetUserId, NetworkPacket packet, CancellationToken cancellationToken = default)
        => Task.FromResult(OperationResult.Ok());

    public Task<OperationResult> BroadcastToUsersAsync(IEnumerable<string> targetUserIds, NetworkPacket packet, CancellationToken cancellationToken = default)
        => Task.FromResult(OperationResult.Ok());

    public Task<OperationResult> KickUserAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        var log = _store.MutateAndSave(() =>
        {
            var removed = _store.ConnectedClients.RemoveAll(c => c.UserId == userId);
            var acc = _store.FindAccountById(userId);
            if (acc is not null)
            {
                acc.User.Presence = PresenceStatus.Offline;
            }
            var finalReason = string.IsNullOrWhiteSpace(reason) ? "Quản trị viên ngắt phiên kết nối" : reason.Trim();
            _store.KickReasonsByUserId[userId] = finalReason;

            var newLog = new ConnectionLogDto
            {
                TimestampUtc = DateTime.UtcNow,
                EventType = "FORCE_DISCONNECT",
                UserId = userId,
                IpEndPoint = "ServerAdmin",
                Description = removed > 0
                    ? $"Ngắt phiên kết nối của {userId}. Lý do: {finalReason}"
                    : $"Đã ghi nhận lệnh ngắt phiên cho {userId}."
            };
            _store.ConnectionLogs.Insert(0, newLog);
            return newLog;
        });

        UserPresenceChanged?.Invoke(this, (userId, PresenceStatus.Offline));
        ConnectionLogAdded?.Invoke(this, log);
        return Task.FromResult(OperationResult.Ok("Đã ngắt kết nối client"));
    }

    public Task<int> CleanupExpiredPendingPacketsAsync(CancellationToken cancellationToken = default)
    {
        var cleared = _store.MutateAndSave(() =>
        {
            var count = _store.PendingQueueCount;
            _store.PendingQueueCount = 0;
            _store.ConnectionLogs.Insert(0, new ConnectionLogDto
            {
                TimestampUtc = DateTime.UtcNow,
                EventType = "QUEUE_CLEANUP",
                IpEndPoint = $"{_store.ServerListenIp}:{_store.ServerListenPort}",
                Description = $"Đã dọn {count} gói tin quá hạn TTL trong PendingQueue."
            });
            return count;
        });
        return Task.FromResult(cleared);
    }

    public void EmitPresence(string userId, PresenceStatus status)
        => UserPresenceChanged?.Invoke(this, (userId, status));

    public void EmitConnectionLog(ConnectionLogDto log)
        => ConnectionLogAdded?.Invoke(this, log);

    private ServerStatsDto BuildStatsSnapshot() => new()
    {
        IsRunning = _store.IsServerRunning,
        ListenIpAddress = _store.ServerListenIp,
        Port = _store.ServerListenPort,
        StartedAtUtc = _store.ServerStartedAtUtc,
        OnlineClientsCount = _store.ConnectedClients.Count,
        PendingQueueCount = _store.PendingQueueCount,
        TotalPacketsSent = _store.TotalPacketsSent,
        TotalPacketsReceived = _store.TotalPacketsReceived,
        TotalBytesSent = _store.TotalBytesSent,
        TotalBytesReceived = _store.TotalBytesReceived,
        ConnectedClients = _store.ConnectedClients.ToList(),
        RecentLogs = _store.ConnectionLogs.Take(30).ToList()
    };
}
