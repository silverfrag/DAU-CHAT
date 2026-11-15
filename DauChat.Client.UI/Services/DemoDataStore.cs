using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using DauChat.Shared.Constants;
using DauChat.Shared.Enums;
using DauChat.Shared.Models;

namespace DauChat.Client.UI.Services;

/// <summary>
/// Kho dữ liệu Demo mẫu dùng cho đồ án (không chứa dữ liệu thật của trường).
/// Hỗ trợ đồng bộ đa cửa sổ (SV / GV / Admin) qua tệp JSON nguyên tử + Mutex trong %LocalAppData%\DauChat\DemoSync
/// để minh họa live workflow trong khi Backend TV2–TV5 đang hoàn thiện.
/// </summary>
internal sealed class DemoDataStore
{
    public sealed class DemoAccountRecord
    {
        public UserDto User { get; set; } = new();
        public string Password { get; set; } = "123456";
        public string AvatarBgHex { get; set; } = "#E2E8F0";
        public string AvatarFgHex { get; set; } = "#334155";
        public bool MustChangePassword { get; set; }
    }

    public sealed class DemoSyncSnapshot
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public long Revision { get; set; } = 1;
        public int LastModifiedByPid { get; set; }
        public bool IsServerRunning { get; set; } = true;
        public string ServerListenIp { get; set; } = "127.0.0.1";
        public int ServerListenPort { get; set; } = ProtocolConstants.DefaultServerPort;
        public DateTime? ServerStartedAtUtc { get; set; }
        public int PendingQueueCount { get; set; } = 6;
        public long TotalPacketsSent { get; set; } = 24810;
        public long TotalPacketsReceived { get; set; } = 23940;
        public long TotalBytesSent { get; set; } = 642_500_000;
        public long TotalBytesReceived { get; set; } = 598_200_000;
        public List<DemoAccountRecord> Accounts { get; set; } = new();
        public List<ClassDto> Classes { get; set; } = new();
        public List<RoomDto> Rooms { get; set; } = new();
        public Dictionary<string, List<MessageDto>> MessagesByConversation { get; set; } = new();
        public Dictionary<string, Dictionary<string, DateTime>> ClearedBeforeUtc { get; set; } = new();
        public Dictionary<string, List<string>> HiddenFileIdsByUser { get; set; } = new();
        public Dictionary<string, List<string>> HiddenMessageIdsByUser { get; set; } = new();
        public List<string> RecalledMessageIds { get; set; } = new();
        public Dictionary<string, List<string>> MutedConversationsByUser { get; set; } = new();
        public Dictionary<string, bool> AutoCleanEnabledByUser { get; set; } = new();
        public Dictionary<string, int> AutoCleanDaysByUser { get; set; } = new();
        public List<FileManifestDto> LocalFiles { get; set; } = new();
        public List<ClientConnectionDto> ConnectedClients { get; set; } = new();
        public List<ConnectionLogDto> ConnectionLogs { get; set; } = new();
        public Dictionary<string, string> KickReasonsByUserId { get; set; } = new();
    }

    public sealed class ExternalSyncDiff
    {
        public List<MessageDto> NewMessages { get; init; } = new();
        public List<string> NewlyRecalledMessageIds { get; init; } = new();
        public List<(string RoomId, PinnedNoticeDto? Notice)> PinnedChanges { get; init; } = new();
        public List<RoomDto> UpdatedRooms { get; init; } = new();
        public List<string> DeletedRoomIds { get; init; } = new();
        public List<ConnectionLogDto> NewLogs { get; init; } = new();
        public bool HasChanges { get; init; }
    }

    private const int CurrentSchemaVersion = 3;
    private const string JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string SyncDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DauChat",
        "DemoSync");

    private static readonly string SyncFilePath = Path.Combine(SyncDirectory, "shared_state.json");
    private static readonly Mutex SyncMutex = new(false, @"Local\DauChatTeam5DemoSyncMutex");

    public readonly object SyncRoot = new();

    private long _appliedRevision;
    private DateTime _lastSeenFileWriteUtc = DateTime.MinValue;
    private long _lastSeenFileLength = -1;
    private readonly HashSet<string> _knownMessageIds = new();
    private readonly HashSet<string> _knownRecalledIds = new();
    private Dictionary<string, string?> _knownPinnedSignatureByRoom = new();
    private Dictionary<string, int> _knownRoomMemberHashById = new();
    private int _knownLogsCount;
    private DispatcherTimer? _pollTimer;

    public bool IsConnected { get; set; }
    public string ConnectedHost { get; set; } = ProtocolConstants.DefaultServerHost;
    public int ConnectedPort { get; set; } = ProtocolConstants.DefaultServerPort;

    public bool IsServerRunning { get; set; } = true;
    public string ServerListenIp { get; set; } = "127.0.0.1";
    public int ServerListenPort { get; set; } = ProtocolConstants.DefaultServerPort;
    public DateTime? ServerStartedAtUtc { get; set; } = DateTime.UtcNow.AddDays(-3).AddHours(-7);
    public int PendingQueueCount { get; set; } = 6;
    public long TotalPacketsSent { get; set; } = 24810;
    public long TotalPacketsReceived { get; set; } = 23940;
    public long TotalBytesSent { get; set; } = 642_500_000;
    public long TotalBytesReceived { get; set; } = 598_200_000;

    public List<DemoAccountRecord> Accounts { get; } = new();
    public List<ClassDto> Classes { get; } = new();
    public List<RoomDto> Rooms { get; } = new();
    public Dictionary<string, List<MessageDto>> MessagesByConversation { get; } = new();
    public Dictionary<string, int> UnreadByConversation { get; } = new();
    public Dictionary<string, Dictionary<string, DateTime>> ClearedBeforeUtc { get; } = new();
    public Dictionary<string, List<string>> HiddenFileIdsByUser { get; } = new();
    public Dictionary<string, List<string>> HiddenMessageIdsByUser { get; } = new();
    public HashSet<string> RecalledMessageIds { get; } = new();
    public Dictionary<string, List<string>> MutedConversationsByUser { get; } = new();
    public Dictionary<string, bool> AutoCleanEnabledByUser { get; } = new();
    public Dictionary<string, int> AutoCleanDaysByUser { get; } = new();
    public List<FileManifestDto> LocalFiles { get; } = new();
    public List<ClientConnectionDto> ConnectedClients { get; } = new();
    public List<ConnectionLogDto> ConnectionLogs { get; } = new();
    public Dictionary<string, string> KickReasonsByUserId { get; } = new();

    public event Action<ExternalSyncDiff>? ExternalSyncApplied;

    public DemoDataStore()
    {
        InitializeFromDiskOrSeed();
    }

    public static string GetDirectConversationKey(string userIdA, string userIdB)
        => string.CompareOrdinal(userIdA, userIdB) <= 0
            ? $"dm:{userIdA}::{userIdB}"
            : $"dm:{userIdB}::{userIdA}";

    public static string GenerateJoinCode(string? classCode, Func<string, bool>? isTaken = null)
    {
        var raw = new string((classCode ?? "DAU")
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
        var prefix = string.IsNullOrEmpty(raw) ? "DAU" : raw[..Math.Min(6, raw.Length)];

        for (var attempt = 0; attempt < 24; attempt++)
        {
            var chars = new char[4];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = JoinCodeAlphabet[Random.Shared.Next(JoinCodeAlphabet.Length)];
            }

            var candidate = $"{prefix}-{new string(chars)}";
            if (isTaken is null || !isTaken(candidate))
            {
                return candidate;
            }
        }

        return $"{prefix}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
    }

    public void EnsurePollingStarted()
    {
        if (_pollTimer is not null) return;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        dispatcher.Invoke(() =>
        {
            if (_pollTimer is not null) return;
            _pollTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            _pollTimer.Tick += (_, _) => CheckDiskUpdates();
            _pollTimer.Start();
        });
    }

    public void CheckDiskUpdates()
    {
        try
        {
            if (!File.Exists(SyncFilePath)) return;
            var fi = new FileInfo(SyncFilePath);
            if (fi.LastWriteTimeUtc == _lastSeenFileWriteUtc && fi.Length == _lastSeenFileLength)
            {
                return;
            }

            ExternalSyncDiff? externalDiff = null;
            WithCrossProcessMutex(() =>
            {
                lock (SyncRoot)
                {
                    var snap = ReadSnapshotLocked();
                    if (snap is null || snap.SchemaVersion != CurrentSchemaVersion || snap.Revision <= _appliedRevision)
                    {
                        if (File.Exists(SyncFilePath))
                        {
                            var updatedFi = new FileInfo(SyncFilePath);
                            _lastSeenFileWriteUtc = updatedFi.LastWriteTimeUtc;
                            _lastSeenFileLength = updatedFi.Length;
                        }
                        return;
                    }

                    externalDiff = ApplySnapshotAndComputeDiffLocked(snap);
                }
            });

            if (externalDiff is { HasChanges: true })
            {
                ExternalSyncApplied?.Invoke(externalDiff);
            }
        }
        catch
        {
            // Ignore transient polling IO errors so UI remains responsive
        }
    }

    public void MutateAndSave(Action mutation)
    {
        MutateAndSave(() =>
        {
            mutation();
            return true;
        });
    }

    public T MutateAndSave<T>(Func<T> mutation)
    {
        ExternalSyncDiff? externalDiff = null;
        T result = default!;

        WithCrossProcessMutex(() =>
        {
            lock (SyncRoot)
            {
                var snap = ReadSnapshotLocked();
                if (snap is not null && snap.SchemaVersion == CurrentSchemaVersion && snap.Revision > _appliedRevision)
                {
                    externalDiff = ApplySnapshotAndComputeDiffLocked(snap);
                }

                result = mutation();

                var nextSnap = BuildSnapshotLocked(_appliedRevision + 1);
                WriteSnapshotAtomicLocked(nextSnap);
                _appliedRevision = nextSnap.Revision;
                CaptureBaselineLocked();
            }
        });

        if (externalDiff is { HasChanges: true })
        {
            ExternalSyncApplied?.Invoke(externalDiff);
        }

        return result;
    }

    public DateTime GetClearedCutoffUtc(string userId, string conversationKey)
    {
        if (ClearedBeforeUtc.TryGetValue(userId, out var map)
            && map.TryGetValue(conversationKey, out var cutoff))
        {
            return cutoff;
        }
        return DateTime.MinValue;
    }

    public bool IsFileHiddenForUser(string userId, string fileId)
        => HiddenFileIdsByUser.TryGetValue(userId, out var ids) && ids.Contains(fileId);

    public bool IsMessageHiddenForUser(string userId, string messageId)
        => HiddenMessageIdsByUser.TryGetValue(userId, out var ids) && ids.Contains(messageId);

    public bool IsMessageRecalled(string messageId)
    {
        lock (SyncRoot)
        {
            return RecalledMessageIds.Contains(messageId);
        }
    }

    public void HideMessageForUser(string userId, string messageId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(messageId)) return;
        MutateAndSave(() =>
        {
            if (!HiddenMessageIdsByUser.TryGetValue(userId, out var ids))
            {
                ids = new List<string>();
                HiddenMessageIdsByUser[userId] = ids;
            }
            if (!ids.Contains(messageId)) ids.Add(messageId);
        });
    }

    public bool IsConversationMuted(string userId, string conversationId)
        => MutedConversationsByUser.TryGetValue(userId, out var ids) && ids.Contains(conversationId);

    public void SetConversationMuted(string userId, string conversationId, bool isMuted)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(conversationId)) return;
        MutateAndSave(() =>
        {
            if (!MutedConversationsByUser.TryGetValue(userId, out var ids))
            {
                ids = new List<string>();
                MutedConversationsByUser[userId] = ids;
            }

            if (isMuted)
            {
                if (!ids.Contains(conversationId)) ids.Add(conversationId);
            }
            else
            {
                ids.Remove(conversationId);
            }
        });
    }

    public (bool Enabled, int Days) GetAutoCleanConfig(string userId)
    {
        lock (SyncRoot)
        {
            var enabled = AutoCleanEnabledByUser.TryGetValue(userId, out var e) && e;
            var days = AutoCleanDaysByUser.TryGetValue(userId, out var d) && d > 0 ? d : 90;
            return (enabled, days);
        }
    }

    public void SetAutoCleanConfig(string userId, bool enabled, int days)
    {
        if (string.IsNullOrEmpty(userId)) return;
        MutateAndSave(() =>
        {
            AutoCleanEnabledByUser[userId] = enabled;
            AutoCleanDaysByUser[userId] = days > 0 ? days : 90;
        });
    }

    public DemoAccountRecord? FindAccountById(string userId)
        => Accounts.FirstOrDefault(a => a.User.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase));

    public DemoAccountRecord? FindAccountByUsername(string username)
        => Accounts.FirstOrDefault(a => a.User.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

    public static string GetInitials(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "DA";

        var cleaned = System.Text.RegularExpressions.Regex.Replace(fullName, @"\([^)]*\)", string.Empty);
        var parts = cleaned
            .Split(new[] { ' ', '•', '-', '/', '·' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 0 && char.IsLetterOrDigit(p[0]))
            .ToList();

        if (parts.Count == 0) return "DA";
        if (parts.Count == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();

        if (cleaned.Contains('•') || parts.Any(p => p.Any(char.IsDigit)))
        {
            return string.Concat(parts[0][0], parts[1][0]).ToUpperInvariant();
        }

        return string.Concat(parts[^2][0], parts[^1][0]).ToUpperInvariant();
    }

    public (string BgHex, string FgHex) GetAvatarTone(string userId)
    {
        var acc = FindAccountById(userId);
        return acc is not null ? (acc.AvatarBgHex, acc.AvatarFgHex) : ("#F1F5F9", "#334155");
    }

    private void InitializeFromDiskOrSeed()
    {
        WithCrossProcessMutex(() =>
        {
            lock (SyncRoot)
            {
                var snap = ReadSnapshotLocked();
                if (snap is not null && snap.SchemaVersion == CurrentSchemaVersion && snap.Accounts.Count > 0)
                {
                    ApplySnapshotLocked(snap);
                    CaptureBaselineLocked();
                    return;
                }

                SeedAccountsAndClasses();
                SeedRoomsAndConversations();
                SeedFilesAndTelemetry();

                var initial = BuildSnapshotLocked(1);
                WriteSnapshotAtomicLocked(initial);
                _appliedRevision = initial.Revision;
                CaptureBaselineLocked();
            }
        });
    }

    public void ResetToCleanTestState()
    {
        WithCrossProcessMutex(() =>
        {
            lock (SyncRoot)
            {
                Accounts.Clear();
                Classes.Clear();
                Rooms.Clear();
                MessagesByConversation.Clear();
                UnreadByConversation.Clear();
                ClearedBeforeUtc.Clear();
                HiddenFileIdsByUser.Clear();
                HiddenMessageIdsByUser.Clear();
                RecalledMessageIds.Clear();
                MutedConversationsByUser.Clear();
                AutoCleanEnabledByUser.Clear();
                AutoCleanDaysByUser.Clear();
                LocalFiles.Clear();
                ConnectedClients.Clear();
                ConnectionLogs.Clear();
                KickReasonsByUserId.Clear();

                TotalPacketsSent = 0;
                TotalPacketsReceived = 0;
                TotalBytesSent = 0;
                TotalBytesReceived = 0;
                PendingQueueCount = 0;

                SeedAccountsAndClasses();
                SeedRoomsAndConversations();
                SeedFilesAndTelemetry();

                var initial = BuildSnapshotLocked(1);
                WriteSnapshotAtomicLocked(initial);
                _appliedRevision = initial.Revision;
                CaptureBaselineLocked();
            }
        });
    }

    private static void WithCrossProcessMutex(Action action)
    {
        var acquired = false;
        try
        {
            try
            {
                acquired = SyncMutex.WaitOne(TimeSpan.FromSeconds(3));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            action();
        }
        finally
        {
            if (acquired)
            {
                try
                {
                    SyncMutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore release mismatch if mutex was abandoned on shutdown
                }
            }
        }
    }

    private DemoSyncSnapshot? ReadSnapshotLocked()
    {
        try
        {
            if (!File.Exists(SyncFilePath)) return null;
            var json = File.ReadAllText(SyncFilePath);
            if (string.IsNullOrWhiteSpace(json)) return null;
            var snap = JsonSerializer.Deserialize<DemoSyncSnapshot>(json, JsonOptions);
            var fi = new FileInfo(SyncFilePath);
            _lastSeenFileWriteUtc = fi.LastWriteTimeUtc;
            _lastSeenFileLength = fi.Length;
            return snap;
        }
        catch
        {
            return null;
        }
    }

    private void WriteSnapshotAtomicLocked(DemoSyncSnapshot snap)
    {
        Directory.CreateDirectory(SyncDirectory);
        var tempPath = Path.Combine(SyncDirectory, $"shared_state.{Environment.ProcessId}.tmp");
        var json = JsonSerializer.Serialize(snap, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SyncFilePath, overwrite: true);
        var fi = new FileInfo(SyncFilePath);
        _lastSeenFileWriteUtc = fi.LastWriteTimeUtc;
        _lastSeenFileLength = fi.Length;
    }

    private DemoSyncSnapshot BuildSnapshotLocked(long revision) => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Revision = revision,
        LastModifiedByPid = Environment.ProcessId,
        IsServerRunning = IsServerRunning,
        ServerListenIp = ServerListenIp,
        ServerListenPort = ServerListenPort,
        ServerStartedAtUtc = ServerStartedAtUtc,
        PendingQueueCount = PendingQueueCount,
        TotalPacketsSent = TotalPacketsSent,
        TotalPacketsReceived = TotalPacketsReceived,
        TotalBytesSent = TotalBytesSent,
        TotalBytesReceived = TotalBytesReceived,
        Accounts = Accounts,
        Classes = Classes,
        Rooms = Rooms,
        MessagesByConversation = MessagesByConversation,
        ClearedBeforeUtc = ClearedBeforeUtc,
        HiddenFileIdsByUser = HiddenFileIdsByUser,
        HiddenMessageIdsByUser = HiddenMessageIdsByUser,
        RecalledMessageIds = RecalledMessageIds.ToList(),
        MutedConversationsByUser = MutedConversationsByUser,
        AutoCleanEnabledByUser = AutoCleanEnabledByUser,
        AutoCleanDaysByUser = AutoCleanDaysByUser,
        LocalFiles = LocalFiles,
        ConnectedClients = ConnectedClients,
        ConnectionLogs = ConnectionLogs,
        KickReasonsByUserId = KickReasonsByUserId
    };

    private ExternalSyncDiff ApplySnapshotAndComputeDiffLocked(DemoSyncSnapshot snap)
    {
        var previousRoomIds = Rooms.Select(r => r.RoomId).ToHashSet();
        var previousPins = new Dictionary<string, string?>(_knownPinnedSignatureByRoom);
        var previousRoomHashes = new Dictionary<string, int>(_knownRoomMemberHashById);
        var previousMsgIds = new HashSet<string>(_knownMessageIds);
        var previousRecalledIds = new HashSet<string>(_knownRecalledIds);
        var previousLogCount = _knownLogsCount;

        ApplySnapshotLocked(snap);

        var newMessages = MessagesByConversation.Values
            .SelectMany(list => list)
            .Where(m => !previousMsgIds.Contains(m.MessageId))
            .OrderBy(m => m.ServerTimestampUtc)
            .ToList();

        var newlyRecalled = RecalledMessageIds
            .Where(id => !previousRecalledIds.Contains(id))
            .ToList();

        var pinnedChanges = new List<(string RoomId, PinnedNoticeDto? Notice)>();
        var updatedRooms = new List<RoomDto>();
        foreach (var room in Rooms)
        {
            var newSig = BuildPinnedSignature(room.PinnedNotice);
            previousPins.TryGetValue(room.RoomId, out var oldSig);
            if (!string.Equals(newSig, oldSig, StringComparison.Ordinal))
            {
                pinnedChanges.Add((room.RoomId, room.PinnedNotice));
            }

            var newHash = ComputeRoomHash(room);
            if (!previousRoomHashes.TryGetValue(room.RoomId, out var oldHash) || oldHash != newHash)
            {
                updatedRooms.Add(room);
            }
        }

        var currentRoomIds = Rooms.Select(r => r.RoomId).ToHashSet();
        var deletedRoomIds = previousRoomIds.Where(id => !currentRoomIds.Contains(id)).ToList();

        var newLogs = ConnectionLogs.Count > previousLogCount
            ? ConnectionLogs.Take(Math.Min(10, ConnectionLogs.Count - previousLogCount)).ToList()
            : new List<ConnectionLogDto>();

        CaptureBaselineLocked();

        return new ExternalSyncDiff
        {
            HasChanges = true,
            NewMessages = newMessages,
            NewlyRecalledMessageIds = newlyRecalled,
            PinnedChanges = pinnedChanges,
            UpdatedRooms = updatedRooms,
            DeletedRoomIds = deletedRoomIds,
            NewLogs = newLogs
        };
    }

    private void ApplySnapshotLocked(DemoSyncSnapshot snap)
    {
        _appliedRevision = snap.Revision;
        IsServerRunning = snap.IsServerRunning;
        ServerListenIp = snap.ServerListenIp;
        ServerListenPort = snap.ServerListenPort;
        ServerStartedAtUtc = snap.ServerStartedAtUtc;
        PendingQueueCount = snap.PendingQueueCount;
        TotalPacketsSent = snap.TotalPacketsSent;
        TotalPacketsReceived = snap.TotalPacketsReceived;
        TotalBytesSent = snap.TotalBytesSent;
        TotalBytesReceived = snap.TotalBytesReceived;

        Accounts.Clear();
        Accounts.AddRange(snap.Accounts);

        Classes.Clear();
        Classes.AddRange(snap.Classes);

        Rooms.Clear();
        Rooms.AddRange(snap.Rooms);

        MessagesByConversation.Clear();
        foreach (var kvp in snap.MessagesByConversation)
        {
            MessagesByConversation[kvp.Key] = kvp.Value;
        }

        ClearedBeforeUtc.Clear();
        foreach (var kvp in snap.ClearedBeforeUtc)
        {
            ClearedBeforeUtc[kvp.Key] = kvp.Value;
        }

        HiddenFileIdsByUser.Clear();
        foreach (var kvp in snap.HiddenFileIdsByUser ?? new Dictionary<string, List<string>>())
        {
            HiddenFileIdsByUser[kvp.Key] = kvp.Value;
        }

        HiddenMessageIdsByUser.Clear();
        foreach (var kvp in snap.HiddenMessageIdsByUser ?? new Dictionary<string, List<string>>())
        {
            HiddenMessageIdsByUser[kvp.Key] = kvp.Value;
        }

        RecalledMessageIds.Clear();
        foreach (var id in snap.RecalledMessageIds ?? new List<string>())
        {
            RecalledMessageIds.Add(id);
        }

        MutedConversationsByUser.Clear();
        foreach (var kvp in snap.MutedConversationsByUser ?? new Dictionary<string, List<string>>())
        {
            MutedConversationsByUser[kvp.Key] = kvp.Value;
        }

        AutoCleanEnabledByUser.Clear();
        foreach (var kvp in snap.AutoCleanEnabledByUser ?? new Dictionary<string, bool>())
        {
            AutoCleanEnabledByUser[kvp.Key] = kvp.Value;
        }

        AutoCleanDaysByUser.Clear();
        foreach (var kvp in snap.AutoCleanDaysByUser ?? new Dictionary<string, int>())
        {
            AutoCleanDaysByUser[kvp.Key] = kvp.Value;
        }

        LocalFiles.Clear();
        LocalFiles.AddRange(snap.LocalFiles);

        ConnectedClients.Clear();
        ConnectedClients.AddRange(snap.ConnectedClients);

        ConnectionLogs.Clear();
        ConnectionLogs.AddRange(snap.ConnectionLogs);

        KickReasonsByUserId.Clear();
        foreach (var kvp in snap.KickReasonsByUserId)
        {
            KickReasonsByUserId[kvp.Key] = kvp.Value;
        }
    }

    private void CaptureBaselineLocked()
    {
        _knownMessageIds.Clear();
        foreach (var list in MessagesByConversation.Values)
        {
            foreach (var msg in list)
            {
                _knownMessageIds.Add(msg.MessageId);
            }
        }

        _knownRecalledIds.Clear();
        foreach (var id in RecalledMessageIds)
        {
            _knownRecalledIds.Add(id);
        }

        _knownPinnedSignatureByRoom = Rooms.ToDictionary(r => r.RoomId, r => BuildPinnedSignature(r.PinnedNotice));
        _knownRoomMemberHashById = Rooms.ToDictionary(r => r.RoomId, ComputeRoomHash);
        _knownLogsCount = ConnectionLogs.Count;
    }

    private static string? BuildPinnedSignature(PinnedNoticeDto? notice)
        => notice is null ? null : $"{notice.MessageId}|{notice.Content}|{notice.PinnedAtUtc.Ticks}";

    private static int ComputeRoomHash(RoomDto r)
    {
        var hc = new HashCode();
        hc.Add(r.RoomName);
        hc.Add(r.Description);
        hc.Add(r.JoinCode);
        hc.Add(r.MemberIds.Count);
        foreach (var id in r.MemberIds) hc.Add(id);
        return hc.ToHashCode();
    }

    private void SeedAccountsAndClasses()
    {
        Accounts.AddRange(new[]
        {
            new DemoAccountRecord
            {
                Password = "123456",
                AvatarBgHex = "#FEF2F2",
                AvatarFgHex = "#CB0914",
                User = new UserDto
                {
                    UserId = "u_admin_demo",
                    Username = "admin",
                    FullName = "Trần Quang Dũng (Demo)",
                    Email = "admin.demo@dau.edu.vn",
                    Role = UserRole.Admin,
                    Status = AccountStatus.Active,
                    Presence = PresenceStatus.Online,
                    LastSeenAtUtc = DateTime.UtcNow
                }
            },
            new DemoAccountRecord
            {
                Password = "123456",
                AvatarBgHex = "#1E293B",
                AvatarFgHex = "#FFFFFF",
                User = new UserDto
                {
                    UserId = "u_gv_demo01",
                    Username = "gv_demo01",
                    FullName = "ThS. Nguyễn Văn Minh",
                    Email = "minh.gv.demo@dau.edu.vn",
                    Role = UserRole.GiangVien,
                    Status = AccountStatus.Active,
                    Presence = PresenceStatus.Online,
                    LastSeenAtUtc = DateTime.UtcNow
                }
            },
            new DemoAccountRecord
            {
                Password = "123456",
                AvatarBgHex = "#DBEAFE",
                AvatarFgHex = "#1D4ED8",
                User = new UserDto
                {
                    UserId = "u_sv_230001",
                    Username = "sv230001",
                    FullName = "Bùi Trần Nhật Hưng",
                    Email = "hung.sv.demo@dau.edu.vn",
                    Role = UserRole.SinhVien,
                    Status = AccountStatus.Active,
                    Presence = PresenceStatus.Online,
                    ClassId = "cls_23ct1",
                    ClassName = "23CT1 • Lập trình mạng",
                    LastSeenAtUtc = DateTime.UtcNow
                }
            },
            new DemoAccountRecord
            {
                Password = "123456",
                AvatarBgHex = "#FEF3C7",
                AvatarFgHex = "#B45309",
                User = new UserDto
                {
                    UserId = "u_sv_230002",
                    Username = "sv230002",
                    FullName = "Lê Thị Thu Hà",
                    Email = "ha.sv.demo@dau.edu.vn",
                    Role = UserRole.SinhVien,
                    Status = AccountStatus.Active,
                    Presence = PresenceStatus.Online,
                    ClassId = null,
                    ClassName = "Chưa gán lớp",
                    LastSeenAtUtc = DateTime.UtcNow
                }
            }
        });

        Classes.AddRange(new[]
        {
            new ClassDto
            {
                ClassId = "cls_23ct1",
                ClassCode = "23CT1",
                ClassName = "Lập trình mạng • 23CT1",
                Department = "Khoa Công nghệ Thông tin (DAU)",
                AcademicYear = "2023 - 2027",
                LecturerId = "u_gv_demo01",
                LecturerName = "ThS. Nguyễn Văn Minh",
                LinkedRoomId = "room_ltm",
                JoinCode = "23CT1-LTM5",
                AllowStudentJoinByCode = true,
                MemberCount = 2,
                MemberIds = new List<string> { "u_gv_demo01", "u_sv_230001" }
            }
        });
    }

    private void SeedRoomsAndConversations()
    {
        Rooms.AddRange(new[]
        {
            new RoomDto
            {
                RoomId = "room_ltm",
                RoomName = "Lập trình mạng • 23CT1",
                Description = "Phòng học phần Lập trình mạng - ĐH Kiến trúc Đà Nẵng.",
                RoomType = RoomType.ClassRoom,
                ClassId = "cls_23ct1",
                JoinCode = "23CT1-LTM5",
                OwnerId = "u_gv_demo01",
                MemberIds = new List<string> { "u_gv_demo01", "u_sv_230001" },
                LastActivityAtUtc = DateTime.UtcNow,
                PinnedNotice = null
            }
        });

        UnreadByConversation["room_ltm"] = 0;

        var initTs = DateTime.UtcNow.AddMinutes(-5);
        MessagesByConversation["room_ltm"] = new List<MessageDto>
        {
            new()
            {
                MessageId = "m_ltm_init",
                ConversationId = "room_ltm",
                RoomId = "room_ltm",
                IsRoomMessage = true,
                SenderId = "u_gv_demo01",
                SenderName = "ThS. Nguyễn Văn Minh",
                Content = "Chào mừng các bạn sinh viên đến với phòng học Lập trình mạng. Các bạn có thể trao đổi, gửi tệp và kiểm thử tương tác tại đây.",
                Type = MessageType.Text,
                SentAtClientUtc = initTs,
                ServerTimestampUtc = initTs
            }
        };
    }

    private void SeedFilesAndTelemetry()
    {
        LocalFiles.Clear();

        ConnectedClients.AddRange(new[]
        {
            new ClientConnectionDto
            {
                SessionId = "sess_01",
                UserId = "u_gv_demo01",
                Username = "gv_demo01",
                IpEndPoint = "127.0.0.1:54112",
                Status = PresenceStatus.Online,
                ConnectedAtUtc = DateTime.UtcNow.AddHours(-1),
                LastHeartbeatUtc = DateTime.UtcNow.AddSeconds(-2),
                PacketsSent = 0,
                PacketsReceived = 0
            },
            new ClientConnectionDto
            {
                SessionId = "sess_02",
                UserId = "u_sv_230001",
                Username = "sv230001",
                IpEndPoint = "127.0.0.1:54880",
                Status = PresenceStatus.Online,
                ConnectedAtUtc = DateTime.UtcNow.AddHours(-1),
                LastHeartbeatUtc = DateTime.UtcNow.AddSeconds(-1),
                PacketsSent = 0,
                PacketsReceived = 0
            }
        });

        ConnectionLogs.AddRange(new[]
        {
            new ConnectionLogDto
            {
                TimestampUtc = DateTime.UtcNow,
                EventType = "SERVER_START",
                IpEndPoint = "127.0.0.1:9050",
                Description = "TCP Socket Server DAU Chat khởi động thành công (Bộ kiểm thử liên kết 3 vai trò)."
            }
        });
    }
}
