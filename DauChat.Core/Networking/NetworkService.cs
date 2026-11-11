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
    private DateTime _serverStartTime;

    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<NetworkPacket>> _pendingQueue = new();
    private readonly ConcurrentQueue<ConnectionLogDto> _connectionLogs = new();

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

            // Bắt đầu đọc dữ liệu
            _ = Task.Run(() => ClientReceiveLoopAsync(_clientCts.Token), _clientCts.Token);

            // Bắt đầu gửi Heartbeat định kỳ (MOD_02_04)
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
                await Task.Delay(TimeSpan.FromSeconds(15), ct); // 15s gửi 1 lần
                if (IsConnected && !string.IsNullOrEmpty(CurrentUserId))
                {
                    await SendHeartbeatAsync(CurrentUserId, ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { /* Bỏ qua lỗi heartbeat */ }
        }
    }

    public async Task<OperationResult> SendPacketAsync(NetworkPacket packet, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _clientStream == null)
            return OperationResult.Fail("Chưa kết nối tới Server.");

        try
        {
            await packet.WriteFramedAsync(_clientStream, cancellationToken);
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

            var ip = IPAddress.Parse(listenIp);
            _listener = new TcpListener(ip, port);
            _listener.Start();

            _serverStartTime = DateTime.UtcNow;
            IsServerRunning = true;
            _serverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _serverBackgroundCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _ = Task.Run(() => ServerAcceptLoopAsync(_serverCts.Token), _serverCts.Token);

            // Chạy ngầm: Tự động check Heartbeat chết & Dọn rác Queue (MOD_02_05, MOD_02_07)
            _ = Task.Run(() => ServerBackgroundTasksAsync(_serverBackgroundCts.Token), _serverBackgroundCts.Token);

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
                await Task.Delay(TimeSpan.FromSeconds(10), ct); // Quét mỗi 10s

                // 1. Quét Heartbeat (Ai quá 45s không gửi là đá)
                var now = DateTime.UtcNow;
                foreach (var session in _sessions.Values.ToList())
                {
                    if (session.UserId != null && (now - session.LastHeartbeat).TotalSeconds > 45)
                    {
                        AddLog($"User {session.UserId} timeout (mất kết nối).");
                        session.Close();
                        _sessions.TryRemove(session.SessionId, out _);
                        _sessions.TryRemove(session.UserId, out _);
                        UserPresenceChanged?.Invoke(this, (session.UserId, PresenceStatus.Offline));
                    }
                }

                // 2. Dọn dẹp Pending Queue hết hạn TTL
                await CleanupExpiredPendingPacketsAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { /* Log */ }
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
                AddLog($"Client mới kết nối từ IP: {session.RemoteIp}");

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

                session.LastHeartbeat = DateTime.UtcNow; // Cập nhật mỗi khi có gói tin gửi đến

                // Map UserId khi Login
                if (packet.Type == PacketType.LoginRequest && !string.IsNullOrEmpty(packet.SenderId))
                {
                    session.UserId = packet.SenderId;
                    _sessions[session.UserId] = session;
                    UserPresenceChanged?.Invoke(this, (session.UserId, PresenceStatus.Online));
                    AddLog($"User {session.UserId} đã Online.");
                }

                // Nếu là Heartbeat, tự động trả lời
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
                AddLog($"User {session.UserId} đã ngắt kết nối.");
            }
        }
    }

    public async Task<OperationResult> SendToUserAsync(string targetUserId, NetworkPacket packet, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(targetUserId, out var session) && session.IsConnected)
        {
            await session.SendAsync(packet, cancellationToken);
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
            AddLog($"User {userId} bị đá. Lý do: {reason}");
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

        if (count > 0) AddLog($"Đã dọn dẹp {count} gói tin hết hạn trong PendingQueue.");

        return Task.FromResult(count);
    }

    // === ADMIN STATS ===
    // BẠN HÃY MỞ CÁC FILE DTO TRONG DauChat.Shared/Models ĐỂ ĐIỀN ĐÚNG TÊN BIẾN
    public Task<ServerStatsDto> GetServerStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new ServerStatsDto
        {
            // Ví dụ: 
            // UptimeSeconds = (long)(DateTime.UtcNow - _serverStartTime).TotalSeconds,
            // TotalOnlineClients = _sessions.Values.Count(s => s.UserId != null)
        };
        return Task.FromResult(stats);
    }

    public Task<IReadOnlyList<ClientConnectionDto>> GetConnectedClientsAsync(CancellationToken cancellationToken = default)
    {
        var clients = _sessions.Values
            .Where(s => s.UserId != null)
            .Select(s => new ClientConnectionDto
            {
                // Ví dụ:
                // UserId = s.UserId,
                // IpAddress = s.RemoteIp
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ClientConnectionDto>>(clients);
    }

    public Task<IReadOnlyList<ConnectionLogDto>> GetConnectionLogsAsync(int maxEntries = 100, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ConnectionLogDto>>(_connectionLogs.Take(maxEntries).ToList());
    }

    private void AddLog(string message)
    {
        var log = new ConnectionLogDto
        {
            // Ví dụ:
            // LogTime = DateTime.UtcNow, 
            // Message = message 
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

internal class ClientSession
{
    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    public string? UserId { get; set; }
    public TcpClient TcpClient { get; }
    public NetworkStream Stream { get; }
    public string RemoteIp { get; }
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow; // THÊM DÒNG NÀY
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
            await packet.WriteFramedAsync(Stream, ct);
    }

    public void Close()
    {
        try { Stream.Close(); } catch { }
        try { TcpClient.Close(); } catch { }
    }
}