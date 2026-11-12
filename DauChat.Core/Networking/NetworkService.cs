using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using DauChat.Shared.Enums;
using DauChat.Shared.Interfaces;
using DauChat.Shared.Models;
using DauChat.Shared.Networking;

namespace DauChat.Core.Networking;

/// <summary>
/// Thực thi (Implementation) tầng Mạng do TV2 phụ trách.
/// </summary>
public class NetworkService : INetworkService, IAsyncDisposable
{
    // === TRẠNG THÁI CHUNG ===
    public bool IsConnected { get; private set; }
    public bool IsServerRunning { get; private set; }
    public string? CurrentUserId { get; set; }

    // === CLIENT ===
    private TcpClient? _client;
    private NetworkStream? _clientStream;
    private CancellationTokenSource? _clientCts;
    private CancellationTokenSource? _clientHeartbeatCts;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<NetworkPacket>> _pendingRequests = new();

    // === SERVER ===
    private TcpListener? _listener;
    private CancellationTokenSource? _serverCts;
    private CancellationTokenSource? _serverBackgroundCts;

    private string _listenIpAddress = "0.0.0.0";
    private int _port = 9050;
    private DateTime _serverStartTime;

    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<NetworkPacket>> _pendingQueue = new();
    private readonly ConcurrentQueue<ConnectionLogDto> _connectionLogs = new();

    // Bộ đếm lưu lượng toàn Server (MOD_08_04)
    private long _totalPacketsSent = 0;
    private long _totalPacketsReceived = 0;
    private long _totalBytesSent = 0;
    private long _totalBytesReceived = 0;

    // === SỰ KIỆN ===
    public event EventHandler<NetworkPacket>? PacketReceived;
    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<(string UserId, PresenceStatus Status)>? UserPresenceChanged;
    public event EventHandler<ConnectionLogDto>? ConnectionLogAdded;
    public event EventHandler<ServerStatsDto>? ServerStatsUpdated;

    #region CLIENT METHODS

    public async Task<OperationResult> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port, cancellationToken);
            _clientStream = _client.GetStream();

            _clientCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsConnected = true;
            ConnectionStateChanged?.Invoke(this, true);

            _ = Task.Run(() => ClientReceiveLoopAsync(_clientCts.Token), _clientCts.Token);

            _clientHeartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = Task.Run(() => ClientHeartbeatLoopAsync(_clientHeartbeatCts.Token), _clientHeartbeatCts.Token);

            return OperationResult.Ok("Kết nối thành công.");
        }
        catch (Exception ex)
        {
            IsConnected = false;
            return OperationResult.Fail(ex.Message);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _clientHeartbeatCts?.Cancel();
        _clientCts?.Cancel();
        _clientStream?.Close();
        _client?.Close();
        _client = null;
        _clientStream = null;
        IsConnected = false;
        ConnectionStateChanged?.Invoke(this, false);
        await Task.CompletedTask;
    }

    private async Task ClientHeartbeatLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && IsConnected)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
                if (IsConnected && !string.IsNullOrEmpty(CurrentUserId))
                {
                    await SendHeartbeatAsync(CurrentUserId, ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { }
        }
    }

    public async Task<OperationResult> SendPacketAsync(NetworkPacket packet, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _clientStream == null)
            return OperationResult.Fail("Chưa kết nối tới Server.");

        try
        {
            byte[] bytes = packet.ToFramedBytes();
            await _clientStream.WriteAsync(bytes, cancellationToken);
            await _clientStream.FlushAsync(cancellationToken);

            // Track traffic
            Interlocked.Increment(ref _totalPacketsSent);
            Interlocked.Add(ref _totalBytesSent, bytes.Length);

            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public async Task<NetworkPacket?> SendAndWaitAsync(NetworkPacket request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.CorrelationId))
            request.CorrelationId = Guid.NewGuid().ToString("N");

        var tcs = new TaskCompletionSource<NetworkPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[request.CorrelationId] = tcs;

        var sendResult = await SendPacketAsync(request, cancellationToken);
        if (!sendResult.Success)
        {
            _pendingRequests.TryRemove(request.CorrelationId, out _);
            return null;
        }

        var timeoutTask = Task.Delay(timeout ?? TimeSpan.FromSeconds(10), cancellationToken);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        _pendingRequests.TryRemove(request.CorrelationId, out _);

        if (completedTask == timeoutTask) return null;

        return await tcs.Task;
    }

    public async Task SendHeartbeatAsync(string userId, CancellationToken cancellationToken = default)
    {
        var packet = NetworkPacket.Create(PacketType.Heartbeat, userId, "PING");
        await SendPacketAsync(packet, cancellationToken);
    }

    public async Task SendAckAsync(string userId, string acknowledgedPacketId, CancellationToken cancellationToken = default)
    {
        var ackPacket = NetworkPacket.CreateAck(userId, acknowledgedPacketId);
        await SendPacketAsync(ackPacket, cancellationToken);
    }

    private async Task ClientReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _clientStream != null)
            {
                var packet = await NetworkPacket.ReadFramedAsync(_clientStream, ct);
                if (packet == null) break;

                Interlocked.Increment(ref _totalPacketsReceived);
                Interlocked.Add(ref _totalBytesReceived, packet.ToFramedBytes().Length);

                if (!string.IsNullOrEmpty(packet.CorrelationId) && _pendingRequests.TryRemove(packet.CorrelationId, out var tcs))
                {
                    tcs.SetResult(packet);
                    continue;
                }

                PacketReceived?.Invoke(this, packet);
            }
        }
        catch (Exception) { }
        finally
        {
            await DisconnectAsync(ct);
        }
    }

    #endregion

    #region SERVER METHODS

    public Task<OperationResult> StartServerAsync(string listenIp, int port, CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsServerRunning) return Task.FromResult(OperationResult.Fail("Server đang chạy rồi."));

            _listenIpAddress = listenIp;
            _port = port;
            var ip = IPAddress.Parse(listenIp);
            _listener = new TcpListener(ip, port);
            _listener.Start();

            _serverStartTime = DateTime.UtcNow;
            IsServerRunning = true;
            _serverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _serverBackgroundCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _ = Task.Run(() => ServerAcceptLoopAsync(_serverCts.Token), _serverCts.Token);
            _ = Task.Run(() => ServerBackgroundTasksAsync(_serverBackgroundCts.Token), _serverBackgroundCts.Token);

            AddLog("SERVER_START", "Server khởi động thành công.");
            return Task.FromResult(OperationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail(ex.Message));
        }
    }

    public Task<OperationResult> StopServerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _serverBackgroundCts?.Cancel();
            _serverCts?.Cancel();
            _listener?.Stop();
            IsServerRunning = false;

            foreach (var session in _sessions.Values) session.Close();
            _sessions.Clear();
            _pendingQueue.Clear();

            AddLog("SERVER_STOP", "Server đã dừng.");
            return Task.FromResult(OperationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail(ex.Message));
        }
    }

    private async Task ServerBackgroundTasksAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);

                // 1. Quét Heartbeat
                var now = DateTime.UtcNow;
                foreach (var session in _sessions.Values.ToList())
                {
                    if (session.UserId != null && (now - session.LastHeartbeat).TotalSeconds > 45)
                    {
                        AddLog("TIMEOUT", $"User {session.UserId} mất kết nối.", session.UserId, session.RemoteIp);
                        session.Close();
                        _sessions.TryRemove(session.SessionId, out _);
                        _sessions.TryRemove(session.UserId, out _);
                        UserPresenceChanged?.Invoke(this, (session.UserId, PresenceStatus.Offline));
                    }
                }

                // 2. Dọn dẹp Pending Queue
                await CleanupExpiredPendingPacketsAsync(ct);

                // 3. Bắn sự kiện cập nhật thông số cho giao diện Admin mỗi 5 giây
                if (IsServerRunning)
                {
                    var stats = await GetServerStatsAsync(ct);
                    ServerStatsUpdated?.Invoke(this, stats);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { }
        }
    }

    private async Task ServerAcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync(ct);
                var session = new ClientSession(tcpClient);
                _sessions[session.SessionId] = session;
                AddLog("CONNECT", $"Client mới kết nối từ IP: {session.RemoteIp}", null, session.RemoteIp);

                _ = Task.Run(() => ServerHandleClientAsync(session, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { }
        }
    }

    private async Task ServerHandleClientAsync(ClientSession session, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var packet = await NetworkPacket.ReadFramedAsync(session.Stream, ct);
                if (packet == null) break;

                session.LastHeartbeat = DateTime.UtcNow;
                session.PacketsReceived++;

                Interlocked.Increment(ref _totalPacketsReceived);
                Interlocked.Add(ref _totalBytesReceived, packet.ToFramedBytes().Length);

                if (packet.Type == PacketType.LoginRequest && !string.IsNullOrEmpty(packet.SenderId))
                {
                    session.UserId = packet.SenderId;
                    _sessions[session.UserId] = session;
                    UserPresenceChanged?.Invoke(this, (session.UserId, PresenceStatus.Online));
                    AddLog("LOGIN", $"User {session.UserId} đã Online.", session.UserId, session.RemoteIp);
                }

                if (packet.Type == PacketType.Heartbeat)
                {
                    var pong = NetworkPacket.Create(PacketType.Heartbeat, "SERVER", "PONG", packet.SenderId);
                    await session.SendAsync(pong, ct);
                    continue;
                }

                PacketReceived?.Invoke(this, packet);
            }
        }
        catch (Exception) { }
        finally
        {
            session.Close();
            _sessions.TryRemove(session.SessionId, out _);

            if (session.UserId != null)
            {
                _sessions.TryRemove(session.UserId, out _);
                UserPresenceChanged?.Invoke(this, (session.UserId, PresenceStatus.Offline));
                AddLog("DISCONNECT", $"User {session.UserId} đã ngắt kết nối.", session.UserId, session.RemoteIp);
            }
        }
    }

    public async Task<OperationResult> SendToUserAsync(string targetUserId, NetworkPacket packet, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(targetUserId, out var session) && session.IsConnected)
        {
            await session.SendAsync(packet, cancellationToken);

            Interlocked.Increment(ref _totalPacketsSent);
            Interlocked.Add(ref _totalBytesSent, packet.ToFramedBytes().Length);

            return OperationResult.Ok();
        }

        var queue = _pendingQueue.GetOrAdd(targetUserId, _ => new ConcurrentQueue<NetworkPacket>());
        queue.Enqueue(packet);

        return OperationResult.Ok("User offline, đã đưa vào hàng đợi.");
    }

    public async Task<OperationResult> BroadcastToUsersAsync(IEnumerable<string> targetUserIds, NetworkPacket packet, CancellationToken cancellationToken = default)
    {
        var tasks = targetUserIds.Select(userId => SendToUserAsync(userId, packet, cancellationToken));
        await Task.WhenAll(tasks);
        return OperationResult.Ok();
    }

    public Task<OperationResult> KickUserAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(userId, out var session))
        {
            session.Close();
            _sessions.TryRemove(userId, out _);
            UserPresenceChanged?.Invoke(this, (userId, PresenceStatus.Offline));
            AddLog("KICK", $"User {userId} bị đá. Lý do: {reason}", userId, session.RemoteIp);
            return Task.FromResult(OperationResult.Ok());
        }
        return Task.FromResult(OperationResult.Fail("User không online."));
    }

    public Task<int> CleanupExpiredPendingPacketsAsync(CancellationToken cancellationToken = default)
    {
        int count = 0;
        var now = DateTime.UtcNow;

        foreach (var kvp in _pendingQueue)
        {
            var queue = kvp.Value;
            var validPackets = new ConcurrentQueue<NetworkPacket>();

            while (queue.TryDequeue(out var packet))
            {
                if ((now - packet.TimestampUtc).TotalSeconds <= packet.TtlSeconds)
                    validPackets.Enqueue(packet);
                else
                    count++;
            }

            _pendingQueue[kvp.Key] = validPackets;
        }

        if (count > 0) AddLog("QUEUE_CLEANUP", $"Đã dọn dẹp {count} gói tin hết hạn trong PendingQueue.");

        return Task.FromResult(count);
    }

    // === ADMIN STATS (Đã map đúng DTO của TV1) ===
    public Task<ServerStatsDto> GetServerStatsAsync(CancellationToken cancellationToken = default)
    {
        var connectedClients = _sessions.Values
            .Where(s => s.UserId != null)
            .Select(s => new ClientConnectionDto
            {
                SessionId = s.SessionId,
                UserId = s.UserId!,
                Username = s.UserId!, // TV3 sẽ cập nhật Username sau
                IpEndPoint = s.RemoteIp,
                Status = PresenceStatus.Online,
                ConnectedAtUtc = s.ConnectedAt,
                LastHeartbeatUtc = s.LastHeartbeat,
                PacketsSent = s.PacketsSent,
                PacketsReceived = s.PacketsReceived
            }).ToList();

        var stats = new ServerStatsDto
        {
            IsRunning = IsServerRunning,
            ListenIpAddress = _listenIpAddress,
            Port = _port,
            StartedAtUtc = IsServerRunning ? _serverStartTime : null,
            OnlineClientsCount = connectedClients.Count,
            PendingQueueCount = _pendingQueue.Values.Sum(q => q.Count),
            TotalPacketsSent = Interlocked.Read(ref _totalPacketsSent),
            TotalPacketsReceived = Interlocked.Read(ref _totalPacketsReceived),
            TotalBytesSent = Interlocked.Read(ref _totalBytesSent),
            TotalBytesReceived = Interlocked.Read(ref _totalBytesReceived),
            ConnectedClients = connectedClients,
            RecentLogs = _connectionLogs.Take(100).ToList()
        };

        return Task.FromResult(stats);
    }

    public Task<IReadOnlyList<ClientConnectionDto>> GetConnectedClientsAsync(CancellationToken cancellationToken = default)
    {
        var clients = _sessions.Values
            .Where(s => s.UserId != null)
            .Select(s => new ClientConnectionDto
            {
                SessionId = s.SessionId,
                UserId = s.UserId!,
                Username = s.UserId!,
                IpEndPoint = s.RemoteIp,
                Status = PresenceStatus.Online,
                ConnectedAtUtc = s.ConnectedAt,
                LastHeartbeatUtc = s.LastHeartbeat,
                PacketsSent = s.PacketsSent,
                PacketsReceived = s.PacketsReceived
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ClientConnectionDto>>(clients);
    }

    public Task<IReadOnlyList<ConnectionLogDto>> GetConnectionLogsAsync(int maxEntries = 100, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ConnectionLogDto>>(_connectionLogs.Take(maxEntries).ToList());
    }

    private void AddLog(string eventType, string description, string? userId = null, string? ip = null)
    {
        var log = new ConnectionLogDto
        {
            TimestampUtc = DateTime.UtcNow,
            EventType = eventType,
            Description = description,
            UserId = userId,
            IpEndPoint = ip ?? "Unknown"
        };
        _connectionLogs.Enqueue(log);
        ConnectionLogAdded?.Invoke(this, log);
        while (_connectionLogs.Count > 500) _connectionLogs.TryDequeue(out _);
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(Task.WhenAll(StopServerAsync(), DisconnectAsync()));
    }

    #endregion
}

/// <summary>
/// Lớp nội bộ đại diện cho một kết nối Client trên Server
/// </summary>
internal class ClientSession
{
    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    public string? UserId { get; set; }
    public TcpClient TcpClient { get; }
    public NetworkStream Stream { get; }
    public string RemoteIp { get; }
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public long PacketsSent { get; set; } = 0;
    public long PacketsReceived { get; set; } = 0;
    public bool IsConnected => TcpClient.Connected;

    public ClientSession(TcpClient tcpClient)
    {
        TcpClient = tcpClient;
        Stream = tcpClient.GetStream();
        RemoteIp = (tcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "Unknown";
    }

    public async Task SendAsync(NetworkPacket packet, CancellationToken ct)
    {
        if (IsConnected)
        {
            await packet.WriteFramedAsync(Stream, ct);
            PacketsSent++;
        }
    }

    public void Close()
    {
        try { Stream.Close(); } catch { }
        try { TcpClient.Close(); } catch { }
    }
}