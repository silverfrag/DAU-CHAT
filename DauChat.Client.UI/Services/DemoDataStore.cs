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
        public List<FileManifestDto> LocalFiles { get; set; } = new();
        public List<ClientConnectionDto> ConnectedClients { get; set; } = new();
        public List<ConnectionLogDto> ConnectionLogs { get; set; } = new();
        public Dictionary<string, string> KickReasonsByUserId { get; set; } = new();
    }

    public sealed class ExternalSyncDiff
    {
        public List<MessageDto> NewMessages { get; init; } = new();
        public List<(string RoomId, PinnedNoticeDto? Notice)> PinnedChanges { get; init; } = new();
        public List<RoomDto> UpdatedRooms { get; init; } = new();
        public List<string> DeletedRoomIds { get; init; } = new();
        public List<ConnectionLogDto> NewLogs { get; init; } = new();
        public bool HasChanges { get; init; }
    }

    private const int CurrentSchemaVersion = 2;
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
        var previousLogCount = _knownLogsCount;

        ApplySnapshotLocked(snap);

        var newMessages = MessagesByConversation.Values
            .SelectMany(list => list)
            .Where(m => !previousMsgIds.Contains(m.MessageId))
            .OrderBy(m => m.ServerTimestampUtc)
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
        foreach (var kvp in snap.HiddenFileIdsByUser)
        {
            HiddenFileIdsByUser[kvp.Key] = kvp.Value;
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
                AvatarBgHex = "#0F766E",
                AvatarFgHex = "#FFFFFF",
                User = new UserDto
                {
                    UserId = "u_gv_demo02",
                    Username = "gv_demo02",
                    FullName = "TS. Lê Hoàng Nam",
                    Email = "nam.gv.demo@dau.edu.vn",
                    Role = UserRole.GiangVien,
                    Status = AccountStatus.Active,
                    Presence = PresenceStatus.Online,
                    LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-8)
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
                    ClassName = "23CT1 • Công nghệ thông tin",
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
                    FullName = "Nguyễn Lan Anh",
                    Email = "anh.sv.demo@dau.edu.vn",
                    Role = UserRole.SinhVien,
                    Status = AccountStatus.Active,
                    Presence = PresenceStatus.Online,
                    ClassId = "cls_23kt1",
                    ClassName = "23KT1 • Kiến trúc công trình",
                    LastSeenAtUtc = DateTime.UtcNow
                }
            },
            new DemoAccountRecord
            {
                Password = "123456",
                AvatarBgHex = "#EDE9FE",
                AvatarFgHex = "#6D28D9",
                User = new UserDto
                {
                    UserId = "u_sv_230003",
                    Username = "sv230003",
                    FullName = "Trần Gia Huy",
                    Email = "huy.sv.demo@dau.edu.vn",
                    Role = UserRole.SinhVien,
                    Status = AccountStatus.Locked,
                    Presence = PresenceStatus.Offline,
                    ClassId = "cls_23ct1",
                    ClassName = "23CT1 • Công nghệ thông tin",
                    LockReason = "Tài khoản mẫu minh họa trạng thái bị Quản trị viên tạm khóa.",
                    LastSeenAtUtc = DateTime.UtcNow.AddHours(-6)
                }
            },
            new DemoAccountRecord
            {
                Password = "123456",
                AvatarBgHex = "#FCE7F3",
                AvatarFgHex = "#BE185D",
                User = new UserDto
                {
                    UserId = "u_sv_230004",
                    Username = "sv230004",
                    FullName = "Lê Khánh Linh",
                    Email = "linh.sv.demo@dau.edu.vn",
                    Role = UserRole.SinhVien,
                    Status = AccountStatus.Active,
                    Presence = PresenceStatus.Busy,
                    ClassId = "cls_23xd1",
                    ClassName = "23XD1 • Kỹ thuật xây dựng",
                    LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-12)
                }
            },
            new DemoAccountRecord
            {
                Password = "123456",
                AvatarBgHex = "#ECFDF5",
                AvatarFgHex = "#047857",
                User = new UserDto
                {
                    UserId = "u_sv_230005",
                    Username = "sv230005",
                    FullName = "Võ Minh Khôi",
                    Email = "khoi.sv.demo@dau.edu.vn",
                    Role = UserRole.SinhVien,
                    Status = AccountStatus.Active,
                    Presence = PresenceStatus.Online,
                    ClassId = "cls_23ct1",
                    ClassName = "23CT1 • Công nghệ thông tin",
                    LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-4)
                }
            },
            new DemoAccountRecord
            {
                Password = "123456",
                AvatarBgHex = "#EFF6FF",
                AvatarFgHex = "#1E40AF",
                User = new UserDto
                {
                    UserId = "u_sv_230006",
                    Username = "sv230006",
                    FullName = "Phạm Thảo Vy",
                    Email = "vy.sv.demo@dau.edu.vn",
                    Role = UserRole.SinhVien,
                    Status = AccountStatus.Active,
                    Presence = PresenceStatus.Online,
                    ClassId = "cls_23kt1",
                    ClassName = "23KT1 • Kiến trúc công trình",
                    LastSeenAtUtc = DateTime.UtcNow.AddMinutes(-15)
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
                MemberCount = 5,
                MemberIds = new List<string> { "u_gv_demo01", "u_sv_230001", "u_sv_230002", "u_sv_230003", "u_sv_230005" }
            },
            new ClassDto
            {
                ClassId = "cls_23kt1",
                ClassCode = "23KT1",
                ClassName = "Đồ án Kiến trúc 4",
                Department = "Khoa Kiến trúc (DAU)",
                AcademicYear = "2023 - 2028",
                LecturerId = "u_gv_demo01",
                LecturerName = "ThS. Nguyễn Văn Minh",
                LinkedRoomId = "room_kt4",
                JoinCode = "23KT1-KT4A",
                AllowStudentJoinByCode = true,
                MemberCount = 4,
                MemberIds = new List<string> { "u_gv_demo01", "u_sv_230001", "u_sv_230002", "u_sv_230006" }
            },
            new ClassDto
            {
                ClassId = "cls_23xd1",
                ClassCode = "23XD1",
                ClassName = "Kết cấu công trình",
                Department = "Khoa Xây dựng (DAU)",
                AcademicYear = "2023 - 2027",
                LecturerId = "u_gv_demo01",
                LecturerName = "ThS. Nguyễn Văn Minh",
                LinkedRoomId = "room_kcc",
                JoinCode = "23XD1-KC8B",
                AllowStudentJoinByCode = true,
                MemberCount = 3,
                MemberIds = new List<string> { "u_gv_demo01", "u_sv_230004", "u_sv_230006" }
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
                Description = "Phòng trao đổi học phần Lập trình mạng, nộp báo cáo đồ án TCP Socket & Local-First.",
                RoomType = RoomType.ClassRoom,
                ClassId = "cls_23ct1",
                JoinCode = "23CT1-LTM5",
                OwnerId = "u_gv_demo01",
                MemberIds = new List<string> { "u_gv_demo01", "u_sv_230001", "u_sv_230002", "u_sv_230005" },
                LastActivityAtUtc = DateTime.UtcNow.AddMinutes(-5),
                PinnedNotice = new PinnedNoticeDto
                {
                    MessageId = "pin_ltm_1",
                    RoomId = "room_ltm",
                    PinnedByUserId = "u_gv_demo01",
                    PinnedByName = "ThS. Nguyễn Văn Minh",
                    Content = "Hạn nộp báo cáo cuối kỳ: 17:00, thứ Sáu tuần này. Các nhóm kiểm tra SHA-256 trước khi gửi file .zip.",
                    PinnedAtUtc = DateTime.UtcNow.AddHours(-3)
                }
            },
            new RoomDto
            {
                RoomId = "room_kt4",
                RoomName = "Đồ án Kiến trúc 4",
                Description = "Kênh duyệt bản vẽ mặt bằng (.dwg/.pdf) và phối cảnh đồ án studio tầng 4.",
                RoomType = RoomType.ClassRoom,
                ClassId = "cls_23kt1",
                JoinCode = "23KT1-KT4A",
                OwnerId = "u_gv_demo01",
                MemberIds = new List<string> { "u_gv_demo01", "u_sv_230001", "u_sv_230002", "u_sv_230006" },
                LastActivityAtUtc = DateTime.UtcNow.AddMinutes(-42),
                PinnedNotice = new PinnedNoticeDto
                {
                    MessageId = "pin_kt4_1",
                    RoomId = "room_kt4",
                    PinnedByUserId = "u_gv_demo01",
                    PinnedByName = "ThS. Nguyễn Văn Minh",
                    Content = "Thông báo sửa đồ án đợt 2: Nộp file Mặt bằng CAD và bản PDF thuyết minh tại studio A302.",
                    PinnedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            },
            new RoomDto
            {
                RoomId = "room_kcc",
                RoomName = "Kết cấu công trình",
                Description = "Chia sẻ tài liệu tính toán dầm sàn, hồ sơ kết cấu bê tông cốt thép.",
                RoomType = RoomType.StudyGroup,
                ClassId = "cls_23xd1",
                JoinCode = "23XD1-KC8B",
                OwnerId = "u_gv_demo01",
                MemberIds = new List<string> { "u_gv_demo01", "u_sv_230004", "u_sv_230006" },
                LastActivityAtUtc = DateTime.UtcNow.AddDays(-1)
            }
        });

        UnreadByConversation["room_ltm"] = 2;
        UnreadByConversation["room_kt4"] = 1;

        var todayBase = DateTime.Today.AddHours(9).AddMinutes(42).ToUniversalTime();
        MessagesByConversation["room_ltm"] = new List<MessageDto>
        {
            new()
            {
                MessageId = "m_ltm_1",
                ConversationId = "room_ltm",
                RoomId = "room_ltm",
                IsRoomMessage = true,
                SenderId = "u_gv_demo01",
                SenderName = "ThS. Nguyễn Văn Minh",
                Content = "Chào cả lớp, thầy đã cập nhật biểu mẫu báo cáo cuối kỳ. Các nhóm tải về và hoàn thiện đúng cấu trúc kiến trúc Local-First nhé.",
                Type = MessageType.Text,
                SentAtClientUtc = todayBase,
                ServerTimestampUtc = todayBase
            },
            new()
            {
                MessageId = "m_ltm_2",
                ConversationId = "room_ltm",
                RoomId = "room_ltm",
                IsRoomMessage = true,
                SenderId = "u_gv_demo01",
                SenderName = "ThS. Nguyễn Văn Minh",
                Content = "Mau-bao-cao-do-an-cuoi-ky.pdf",
                Type = MessageType.FileAttachment,
                AttachmentFileId = "file_001",
                AttachmentFileName = "Mau-bao-cao-do-an-cuoi-ky.pdf",
                AttachmentFileSize = 2_516_582,
                LocalFilePath = "LocalData\\Demo\\Files\\room_ltm\\Mau-bao-cao-do-an-cuoi-ky.pdf",
                SentAtClientUtc = todayBase.AddMinutes(2),
                ServerTimestampUtc = todayBase.AddMinutes(2)
            },
            new()
            {
                MessageId = "m_ltm_3",
                ConversationId = "room_ltm",
                RoomId = "room_ltm",
                IsRoomMessage = true,
                SenderId = "u_sv_230001",
                SenderName = "Bùi Trần Nhật Hưng",
                Content = "Dạ vâng thầy. Nhóm 5 đã chốt xong giao thức Length-prefix 4 byte và tách tầng LocalStorage xong rồi ạ.",
                Type = MessageType.Text,
                SentAtClientUtc = todayBase.AddMinutes(23),
                ServerTimestampUtc = todayBase.AddMinutes(23)
            },
            new()
            {
                MessageId = "m_ltm_4",
                ConversationId = "room_ltm",
                RoomId = "room_ltm",
                IsRoomMessage = true,
                SenderId = "u_sv_230001",
                SenderName = "Bùi Trần Nhật Hưng",
                Content = "DAU-Chat-Team5-KienTruc.zip",
                Type = MessageType.FileAttachment,
                AttachmentFileId = "file_003",
                AttachmentFileName = "DAU-Chat-Team5-KienTruc.zip",
                AttachmentFileSize = 132_120_576,
                LocalFilePath = "LocalData\\Demo\\Files\\room_ltm\\DAU-Chat-Team5-KienTruc.zip",
                SentAtClientUtc = todayBase.AddMinutes(26),
                ServerTimestampUtc = todayBase.AddMinutes(26)
            },
            new()
            {
                MessageId = "m_ltm_5",
                ConversationId = "room_ltm",
                RoomId = "room_ltm",
                IsRoomMessage = true,
                SenderId = "u_sv_230002",
                SenderName = "Nguyễn Lan Anh",
                Content = "Mọi người kiểm tra lại luồng PendingQueue + PacketAck khi tài khoản đang Offline nhé, mình đã đối chiếu mã nguồn xong.",
                Type = MessageType.Text,
                SentAtClientUtc = todayBase.AddMinutes(36),
                ServerTimestampUtc = todayBase.AddMinutes(36)
            }
        };

        MessagesByConversation["room_kt4"] = new List<MessageDto>
        {
            new()
            {
                MessageId = "m_kt4_1",
                ConversationId = "room_kt4",
                RoomId = "room_kt4",
                IsRoomMessage = true,
                SenderId = "u_sv_230002",
                SenderName = "Nguyễn Lan Anh",
                Content = "Mình vừa gửi bản vẽ mặt bằng tầng 1 dạng .dwg qua luồng chunk 64KB, mọi người kiểm tra khớp SHA-256 nhé.",
                Type = MessageType.Text,
                SentAtClientUtc = todayBase.AddHours(-2),
                ServerTimestampUtc = todayBase.AddHours(-2)
            },
            new()
            {
                MessageId = "m_kt4_2",
                ConversationId = "room_kt4",
                RoomId = "room_kt4",
                IsRoomMessage = true,
                SenderId = "u_sv_230002",
                SenderName = "Nguyễn Lan Anh",
                Content = "So-do-mat-bang-tang-01.dwg",
                Type = MessageType.FileAttachment,
                AttachmentFileId = "file_002",
                AttachmentFileName = "So-do-mat-bang-tang-01.dwg",
                AttachmentFileSize = 19_608_371,
                SentAtClientUtc = todayBase.AddHours(-2).AddMinutes(1),
                ServerTimestampUtc = todayBase.AddHours(-2).AddMinutes(1)
            }
        };

        MessagesByConversation["room_kcc"] = new List<MessageDto>
        {
            new()
            {
                MessageId = "m_kcc_1",
                ConversationId = "room_kcc",
                RoomId = "room_kcc",
                IsRoomMessage = true,
                SenderId = "u_sv_230004",
                SenderName = "Lê Khánh Linh",
                Content = "Đã đồng bộ file Excel khảo sát địa chất nền móng về kho tài liệu máy trạm.",
                Type = MessageType.Text,
                SentAtClientUtc = todayBase.AddDays(-1),
                ServerTimestampUtc = todayBase.AddDays(-1)
            }
        };

        var dmKey12 = GetDirectConversationKey("u_sv_230001", "u_sv_230002");
        MessagesByConversation[dmKey12] = new List<MessageDto>
        {
            new()
            {
                MessageId = "m_dm_1",
                ConversationId = dmKey12,
                IsRoomMessage = false,
                SenderId = "u_sv_230002",
                SenderName = "Nguyễn Lan Anh",
                ReceiverId = "u_sv_230001",
                Content = "Mình đã gửi bản vẽ mặt bằng qua nhóm rồi nhé, có gì xem giúp mình phần chú thích trục.",
                Type = MessageType.Text,
                SentAtClientUtc = todayBase.AddMinutes(-20),
                ServerTimestampUtc = todayBase.AddMinutes(-20)
            }
        };

        var dmKey14 = GetDirectConversationKey("u_sv_230001", "u_sv_230004");
        MessagesByConversation[dmKey14] = new List<MessageDto>
        {
            new()
            {
                MessageId = "m_dm_2",
                ConversationId = dmKey14,
                IsRoomMessage = false,
                SenderId = "u_sv_230004",
                SenderName = "Lê Khánh Linh",
                ReceiverId = "u_sv_230001",
                Content = "Chiều nay nhóm mình họp kiểm tra tiến độ UI lúc 14:30 ở thư viện DAU nhé!",
                Type = MessageType.Text,
                SentAtClientUtc = todayBase.AddDays(-1).AddHours(3),
                ServerTimestampUtc = todayBase.AddDays(-1).AddHours(3)
            }
        };

        var dmKeyGv1 = GetDirectConversationKey("u_gv_demo01", "u_sv_230001");
        MessagesByConversation[dmKeyGv1] = new List<MessageDto>
        {
            new()
            {
                MessageId = "m_dm_3",
                ConversationId = dmKeyGv1,
                IsRoomMessage = false,
                SenderId = "u_gv_demo01",
                SenderName = "ThS. Nguyễn Văn Minh",
                ReceiverId = "u_sv_230001",
                Content = "Em chuẩn bị phần demo truyền file qua TCP và kiểm thử 2 tài khoản cùng lúc để buổi bảo vệ chạy mượt nhé.",
                Type = MessageType.Text,
                SentAtClientUtc = todayBase.AddHours(-3),
                ServerTimestampUtc = todayBase.AddHours(-3)
            }
        };
    }

    private void SeedFilesAndTelemetry()
    {
        LocalFiles.AddRange(new[]
        {
            new FileManifestDto
            {
                FileId = "file_001",
                FileName = "Mau-bao-cao-do-an-cuoi-ky.pdf",
                Extension = ".pdf",
                Category = FileCategory.PDF,
                RoomOrPartnerId = "room_ltm",
                IsRoom = true,
                SenderId = "u_gv_demo01",
                SenderName = "ThS. Nguyễn Văn Minh",
                FileSizeBytes = 2_516_582,
                Sha256Checksum = "a9f4c821e7b09d43e1120c7f43a2104b9f7e38d16e54b2c1a0f8e12d44c9b201",
                LocalRelativePath = "Files/room_ltm/Mau-bao-cao-do-an-cuoi-ky.pdf",
                LocalFullPath = string.Empty,
                DownloadedAtUtc = DateTime.UtcNow.AddHours(-3),
                LastAccessedAtUtc = DateTime.UtcNow.AddMinutes(-20)
            },
            new FileManifestDto
            {
                FileId = "file_002",
                FileName = "So-do-mat-bang-tang-01.dwg",
                Extension = ".dwg",
                Category = FileCategory.CAD,
                RoomOrPartnerId = "room_kt4",
                IsRoom = true,
                SenderId = "u_sv_230002",
                SenderName = "Nguyễn Lan Anh",
                FileSizeBytes = 19_608_371,
                Sha256Checksum = "7e21a094f56c3d8b10e4821a774f2d11c8a9e33b2d1f77001122aaccbbdd8844",
                LocalRelativePath = "Files/room_kt4/So-do-mat-bang-tang-01.dwg",
                LocalFullPath = string.Empty,
                DownloadedAtUtc = DateTime.UtcNow.AddDays(-1),
                LastAccessedAtUtc = DateTime.UtcNow.AddHours(-5)
            },
            new FileManifestDto
            {
                FileId = "file_003",
                FileName = "DAU-Chat-Team5-KienTruc.zip",
                Extension = ".zip",
                Category = FileCategory.Archive,
                RoomOrPartnerId = "room_ltm",
                IsRoom = true,
                SenderId = "u_sv_230001",
                SenderName = "Bùi Trần Nhật Hưng",
                FileSizeBytes = 132_120_576,
                Sha256Checksum = "3c91d4587ab2190fe0981234abcd5678ef0123456789abcdef11223344556677",
                LocalRelativePath = "Files/room_ltm/DAU-Chat-Team5-KienTruc.zip",
                LocalFullPath = string.Empty,
                DownloadedAtUtc = DateTime.UtcNow.AddDays(-4),
                LastAccessedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            new FileManifestDto
            {
                FileId = "file_004",
                FileName = "Phoi-canh-mat-dung-DAU.png",
                Extension = ".png",
                Category = FileCategory.Image,
                RoomOrPartnerId = "room_kt4",
                IsRoom = true,
                SenderId = "u_sv_230002",
                SenderName = "Nguyễn Lan Anh",
                FileSizeBytes = 8_493_465,
                Sha256Checksum = "5b12fe98a76d4431c90218736455acbd12340987fedcba654321001122334455",
                LocalRelativePath = "Files/room_kt4/Phoi-canh-mat-dung-DAU.png",
                LocalFullPath = string.Empty,
                DownloadedAtUtc = DateTime.UtcNow.AddDays(-2),
                LastAccessedAtUtc = DateTime.UtcNow.AddDays(-1)
            },
            new FileManifestDto
            {
                FileId = "file_005",
                FileName = "Du-lieu-khao-sat-cong-trinh.xlsx",
                Extension = ".xlsx",
                Category = FileCategory.Document,
                RoomOrPartnerId = "room_kcc",
                IsRoom = true,
                SenderId = "u_sv_230004",
                SenderName = "Lê Khánh Linh",
                FileSizeBytes = 5_033_164,
                Sha256Checksum = "88a1c3f91e04b77d21349087bcde6543a1209876bcdf54321098abcdef123456",
                LocalRelativePath = "Files/room_kcc/Du-lieu-khao-sat-cong-trinh.xlsx",
                LocalFullPath = string.Empty,
                DownloadedAtUtc = DateTime.UtcNow.AddDays(-110),
                LastAccessedAtUtc = DateTime.UtcNow.AddDays(-98)
            }
        });

        ConnectedClients.AddRange(new[]
        {
            new ClientConnectionDto
            {
                SessionId = "sess_01",
                UserId = "u_gv_demo01",
                Username = "gv_demo01",
                IpEndPoint = "192.168.10.24:54112",
                Status = PresenceStatus.Online,
                ConnectedAtUtc = DateTime.UtcNow.AddHours(-2),
                LastHeartbeatUtc = DateTime.UtcNow.AddSeconds(-4),
                PacketsSent = 412,
                PacketsReceived = 388
            },
            new ClientConnectionDto
            {
                SessionId = "sess_02",
                UserId = "u_sv_230001",
                Username = "sv230001",
                IpEndPoint = "127.0.0.1:54880",
                Status = PresenceStatus.Online,
                ConnectedAtUtc = DateTime.UtcNow.AddMinutes(-45),
                LastHeartbeatUtc = DateTime.UtcNow.AddSeconds(-2),
                PacketsSent = 185,
                PacketsReceived = 219
            },
            new ClientConnectionDto
            {
                SessionId = "sess_03",
                UserId = "u_sv_230002",
                Username = "sv230002",
                IpEndPoint = "192.168.10.41:55019",
                Status = PresenceStatus.Online,
                ConnectedAtUtc = DateTime.UtcNow.AddMinutes(-28),
                LastHeartbeatUtc = DateTime.UtcNow.AddSeconds(-7),
                PacketsSent = 96,
                PacketsReceived = 110
            },
            new ClientConnectionDto
            {
                SessionId = "sess_04",
                UserId = "u_sv_230004",
                Username = "sv230004",
                IpEndPoint = "192.168.10.58:55104",
                Status = PresenceStatus.Busy,
                ConnectedAtUtc = DateTime.UtcNow.AddMinutes(-18),
                LastHeartbeatUtc = DateTime.UtcNow.AddSeconds(-11),
                PacketsSent = 64,
                PacketsReceived = 72
            }
        });

        ConnectionLogs.AddRange(new[]
        {
            new ConnectionLogDto
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(-35),
                EventType = "SERVER_START",
                IpEndPoint = "127.0.0.1:9050",
                Description = "TCP Socket Listener khởi động theo chuẩn Length-prefix 4 byte."
            },
            new ConnectionLogDto
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(-28),
                EventType = "AUTH_LOGIN",
                UserId = "u_sv_230002",
                IpEndPoint = "192.168.10.41:55019",
                Description = "sv230002 đăng nhập thành công; đồng bộ 2 gói tin từ PendingQueue."
            },
            new ConnectionLogDto
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(-18),
                EventType = "PACKET_ACK",
                UserId = "u_gv_demo01",
                IpEndPoint = "192.168.10.24:54112",
                Description = "Đã nhận PacketAck xác thực SHA-256 tệp Mau-bao-cao-do-an-cuoi-ky.pdf."
            },
            new ConnectionLogDto
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(-6),
                EventType = "AUTH_DENIED",
                UserId = "u_sv_230003",
                IpEndPoint = "192.168.10.77:55203",
                Description = "Từ chối kết nối sv230003: Tài khoản đang ở trạng thái Locked."
            }
        });
    }
}
