using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DauChat.Client.UI.Infrastructure;
using DauChat.Client.UI.Models;
using DauChat.Client.UI.Services;
using DauChat.Shared.Enums;
using DauChat.Shared.Models;

namespace DauChat.Client.UI.ViewModels;

public sealed class AdminCenterViewModel : ObservableObject
{
    private readonly Action<string> _notify;
    private readonly Action _onOpenCreateAccount;
    private readonly Action _onOpenBatchCreateAccounts;
    private readonly Action<AccountItemModel> _onOpenEditAccount;
    private readonly Action<AccountItemModel> _onOpenLockAccount;
    private readonly Action _onOpenCreateClass;
    private readonly Action<ClassItemModel> _onOpenEditClass;
    private readonly Action<string, string> _onOpenManageClassMembers;
    private readonly Action _onOpenCreateRoom;
    private readonly Action<RoomDto> _onOpenEditRoom;
    private readonly Action<string, string> _onOpenManageRoomMembers;
    private readonly Func<string, Task> _onOpenRoomInChatAsync;

    private AdminSubTab _activeTab = AdminSubTab.OverviewAndUsers;
    private string _accountSearchQuery = string.Empty;
    private AccountFilterOption _selectedAccountFilter = AccountFilterOption.All;
    private string _classSearchQuery = string.Empty;
    private string _roomSearchQuery = string.Empty;

    private int _totalUsersCount;
    private int _totalClassesCount;
    private int _totalRoomsCount;
    private int _onlineClientsCount;
    private string _packetsTodayText = "24.8K";
    private string _totalBytesText = "1.15 GB";
    private string _bytesBreakdownText = "↑ 0 B  •  ↓ 0 B";

    private bool _isServerRunning;
    private string _serverListenIp = "127.0.0.1";
    private string _serverListenPortText = "9050";
    private int _serverListenPort = 9050;
    private string _liveListenIp = "127.0.0.1";
    private int _liveListenPort = 9050;
    private bool _isServerConfigDirty;
    private string _serverUptimeText = "03d 07h";
    private int _pendingQueueCount;

    private List<AccountItemModel> _allUsers = new();
    private List<ClassItemModel> _allClasses = new();
    private List<RoomItemModel> _allRooms = new();

    public AdminCenterViewModel(
        Action<string> notify,
        Action onOpenCreateAccount,
        Action onOpenBatchCreateAccounts,
        Action<AccountItemModel> onOpenEditAccount,
        Action<AccountItemModel> onOpenLockAccount,
        Action onOpenCreateClass,
        Action<ClassItemModel> onOpenEditClass,
        Action<string, string> onOpenManageClassMembers,
        Action onOpenCreateRoom,
        Action<RoomDto> onOpenEditRoom,
        Action<string, string> onOpenManageRoomMembers,
        Func<string, Task> onOpenRoomInChatAsync)
    {
        _notify = notify;
        _onOpenCreateAccount = onOpenCreateAccount;
        _onOpenBatchCreateAccounts = onOpenBatchCreateAccounts;
        _onOpenEditAccount = onOpenEditAccount;
        _onOpenLockAccount = onOpenLockAccount;
        _onOpenCreateClass = onOpenCreateClass;
        _onOpenEditClass = onOpenEditClass;
        _onOpenManageClassMembers = onOpenManageClassMembers;
        _onOpenCreateRoom = onOpenCreateRoom;
        _onOpenEditRoom = onOpenEditRoom;
        _onOpenManageRoomMembers = onOpenManageRoomMembers;
        _onOpenRoomInChatAsync = onOpenRoomInChatAsync;

        SwitchTabCommand = new RelayCommand(param =>
        {
            if (param is string tabStr && Enum.TryParse<AdminSubTab>(tabStr, out var tab))
            {
                ActiveTab = tab;
            }
        });

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);

        // Tab 1: Users & RBAC
        OpenCreateAccountCommand = new RelayCommand(() => _onOpenCreateAccount());
        OpenBatchCreateAccountsCommand = new RelayCommand(() => _onOpenBatchCreateAccounts());
        SelectAccountFilterCommand = new RelayCommand(param =>
        {
            if (param is string fStr && Enum.TryParse<AccountFilterOption>(fStr, out var opt))
            {
                SelectedAccountFilter = opt;
            }
        });
        OpenEditAccountCommand = new RelayCommand(param =>
        {
            if (param is AccountItemModel account) _onOpenEditAccount(account);
        });
        ToggleLockAccountCommand = new AsyncRelayCommand(ToggleLockAccountAsync);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync);
        CycleRoleCommand = new AsyncRelayCommand(CycleRoleAsync);

        // Tab 2: Classes
        OpenCreateClassCommand = new RelayCommand(() => _onOpenCreateClass());
        OpenEditClassCommand = new RelayCommand(param =>
        {
            if (param is ClassItemModel cls) _onOpenEditClass(cls);
        });
        OpenManageClassMembersCommand = new RelayCommand(param =>
        {
            if (param is ClassItemModel cls)
            {
                _onOpenManageClassMembers(cls.ClassId, $"{cls.ClassCode} — {cls.ClassName}");
            }
        });
        LinkClassRoomCommand = new AsyncRelayCommand(LinkClassRoomAsync);
        DeleteClassCommand = new AsyncRelayCommand(DeleteClassAsync);

        // Tab 3: Rooms
        OpenCreateRoomCommand = new RelayCommand(() => _onOpenCreateRoom());
        OpenEditRoomCommand = new RelayCommand(param =>
        {
            if (param is RoomItemModel room) _onOpenEditRoom(room.RawRoom);
        });
        OpenManageRoomMembersCommand = new RelayCommand(param =>
        {
            if (param is RoomItemModel room)
            {
                _onOpenManageRoomMembers(room.RoomId, room.RoomName);
            }
        });
        OpenRoomInChatCommand = new AsyncRelayCommand(async param =>
        {
            if (param is RoomItemModel room)
            {
                await _onOpenRoomInChatAsync(room.RoomId);
            }
        });
        DeleteRoomCommand = new AsyncRelayCommand(DeleteRoomAsync);

        // Tab 4: Socket Server & Monitoring
        ToggleServerCommand = new AsyncRelayCommand(ToggleServerAsync);
        ApplyServerConfigCommand = new AsyncRelayCommand(ApplyServerConfigAsync);
        CleanupPendingQueueCommand = new AsyncRelayCommand(CleanupPendingQueueAsync);
        KickClientCommand = new AsyncRelayCommand(KickClientAsync);

        ClientServiceHost.Current.Network.ConnectionLogAdded += (_, _) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() => _ = RefreshTelemetryAsync());
        };
    }

    public ObservableCollection<AccountItemModel> FilteredUsers { get; } = new();
    public ObservableCollection<ClassItemModel> FilteredClasses { get; } = new();
    public ObservableCollection<RoomItemModel> FilteredRooms { get; } = new();
    public ObservableCollection<ClientConnectionDto> ConnectedClients { get; } = new();
    public ObservableCollection<ConnectionLogDto> ConnectionLogs { get; } = new();

    public ICommand SwitchTabCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenCreateAccountCommand { get; }
    public ICommand OpenBatchCreateAccountsCommand { get; }
    public ICommand SelectAccountFilterCommand { get; }
    public ICommand OpenEditAccountCommand { get; }
    public ICommand ToggleLockAccountCommand { get; }
    public ICommand ResetPasswordCommand { get; }
    public ICommand CycleRoleCommand { get; }

    public ICommand OpenCreateClassCommand { get; }
    public ICommand OpenEditClassCommand { get; }
    public ICommand OpenManageClassMembersCommand { get; }
    public ICommand LinkClassRoomCommand { get; }
    public ICommand DeleteClassCommand { get; }

    public ICommand OpenCreateRoomCommand { get; }
    public ICommand OpenEditRoomCommand { get; }
    public ICommand OpenManageRoomMembersCommand { get; }
    public ICommand OpenRoomInChatCommand { get; }
    public ICommand DeleteRoomCommand { get; }

    public ICommand ToggleServerCommand { get; }
    public ICommand ApplyServerConfigCommand { get; }
    public ICommand CleanupPendingQueueCommand { get; }
    public ICommand KickClientCommand { get; }

    public AdminSubTab ActiveTab
    {
        get => _activeTab;
        set => SetProperty(ref _activeTab, value);
    }

    public string AccountSearchQuery
    {
        get => _accountSearchQuery;
        set
        {
            if (SetProperty(ref _accountSearchQuery, value))
            {
                ApplyUserFilter();
            }
        }
    }

    public AccountFilterOption SelectedAccountFilter
    {
        get => _selectedAccountFilter;
        set
        {
            if (SetProperty(ref _selectedAccountFilter, value))
            {
                ApplyUserFilter();
            }
        }
    }

    public string FilteredUsersSummary => $"{FilteredUsers.Count}/{TotalUsersCount} tài khoản";

    public string ClassSearchQuery
    {
        get => _classSearchQuery;
        set
        {
            if (SetProperty(ref _classSearchQuery, value))
            {
                ApplyClassFilter();
            }
        }
    }

    public string RoomSearchQuery
    {
        get => _roomSearchQuery;
        set
        {
            if (SetProperty(ref _roomSearchQuery, value))
            {
                ApplyRoomFilter();
            }
        }
    }

    public int TotalUsersCount { get => _totalUsersCount; set => SetProperty(ref _totalUsersCount, value); }
    public int TotalClassesCount
    {
        get => _totalClassesCount;
        set
        {
            if (SetProperty(ref _totalClassesCount, value))
            {
                OnPropertyChanged(nameof(ClassesAndRoomsSummary));
            }
        }
    }
    public int TotalRoomsCount
    {
        get => _totalRoomsCount;
        set
        {
            if (SetProperty(ref _totalRoomsCount, value))
            {
                OnPropertyChanged(nameof(ClassesAndRoomsSummary));
            }
        }
    }
    public string ClassesAndRoomsSummary => $"{TotalRoomsCount} phòng chat đang hoạt động";
    public int OnlineClientsCount { get => _onlineClientsCount; set => SetProperty(ref _onlineClientsCount, value); }
    public string PacketsTodayText { get => _packetsTodayText; set => SetProperty(ref _packetsTodayText, value); }
    public string TotalBytesText { get => _totalBytesText; set => SetProperty(ref _totalBytesText, value); }
    public string BytesBreakdownText { get => _bytesBreakdownText; set => SetProperty(ref _bytesBreakdownText, value); }

    public bool IsServerRunning
    {
        get => _isServerRunning;
        set
        {
            if (SetProperty(ref _isServerRunning, value))
            {
                OnPropertyChanged(nameof(ServerStatusSummary));
            }
        }
    }

    public string ServerStatusSummary => IsServerRunning ? "Đang lắng nghe kết nối" : "Dịch vụ đã tạm dừng";

    public string ServerListenIp
    {
        get => _serverListenIp;
        set
        {
            if (SetProperty(ref _serverListenIp, value))
            {
                _isServerConfigDirty = true;
            }
        }
    }

    public string ServerListenPortText
    {
        get => _serverListenPortText;
        set
        {
            if (SetProperty(ref _serverListenPortText, value))
            {
                _isServerConfigDirty = true;
                if (int.TryParse(value.Trim(), out var parsed) && parsed is >= 1 and <= 65535)
                {
                    ServerListenPort = parsed;
                }
            }
        }
    }

    public int ServerListenPort { get => _serverListenPort; set => SetProperty(ref _serverListenPort, value); }
    public string LiveListenIp { get => _liveListenIp; set => SetProperty(ref _liveListenIp, value); }
    public int LiveListenPort { get => _liveListenPort; set => SetProperty(ref _liveListenPort, value); }
    public string ServerUptimeText { get => _serverUptimeText; set => SetProperty(ref _serverUptimeText, value); }
    public int PendingQueueCount { get => _pendingQueueCount; set => SetProperty(ref _pendingQueueCount, value); }

    public async Task RefreshAsync()
    {
        var host = ClientServiceHost.Current;
        var currentUser = host.AuthRoom.CurrentUser;

        var usersRes = await host.AuthRoom.GetAllUsersAsync();
        var userMap = new Dictionary<string, UserDto>();
        if (usersRes.Success && usersRes.Data is not null)
        {
            userMap = usersRes.Data.ToDictionary(u => u.UserId, u => u);
            _allUsers = usersRes.Data.Select(u =>
            {
                var (bg, fg) = host.DemoStore.GetAvatarTone(u.UserId);
                return new AccountItemModel
                {
                    RawUser = u,
                    Initials = DemoDataStore.GetInitials(u.FullName),
                    AvatarBgHex = bg,
                    AvatarFgHex = fg,
                    CanToggleLock = u.UserId != currentUser?.UserId
                };
            }).ToList();
            TotalUsersCount = _allUsers.Count;
            ApplyUserFilter();
        }

        var classesRes = await host.AuthRoom.GetClassesAsync();
        if (classesRes.Success && classesRes.Data is not null)
        {
            _allClasses = classesRes.Data.Select(c => new ClassItemModel { RawClass = c }).ToList();
            TotalClassesCount = _allClasses.Count;
            ApplyClassFilter();
        }

        if (currentUser is not null)
        {
            var roomsRes = await host.Messaging.GetJoinedRoomsAsync(currentUser.UserId);
            if (roomsRes.Success && roomsRes.Data is not null)
            {
                _allRooms = roomsRes.Data.Select(r => new RoomItemModel
                {
                    RawRoom = r,
                    OwnerName = userMap.TryGetValue(r.OwnerId, out var owner) ? owner.FullName : r.OwnerId
                }).ToList();
                TotalRoomsCount = _allRooms.Count;
                ApplyRoomFilter();
            }
        }

        await RefreshTelemetryAsync();
    }

    private async Task RefreshTelemetryAsync()
    {
        var net = ClientServiceHost.Current.Network;
        var stats = await net.GetServerStatsAsync();
        IsServerRunning = stats.IsRunning;
        LiveListenIp = stats.ListenIpAddress;
        LiveListenPort = stats.Port;
        if (!_isServerConfigDirty)
        {
            _serverListenIp = stats.ListenIpAddress;
            _serverListenPort = stats.Port;
            _serverListenPortText = stats.Port.ToString();
            OnPropertyChanged(nameof(ServerListenIp));
            OnPropertyChanged(nameof(ServerListenPort));
            OnPropertyChanged(nameof(ServerListenPortText));
        }

        PendingQueueCount = stats.PendingQueueCount;
        OnlineClientsCount = stats.OnlineClientsCount;
        PacketsTodayText = $"{(stats.TotalPacketsSent + stats.TotalPacketsReceived) / 1000d:0.0}K";
        TotalBytesText = DemoFileStorageService.FormatBytes(stats.TotalBytesSent + stats.TotalBytesReceived);
        BytesBreakdownText = $"↑ {DemoFileStorageService.FormatBytes(stats.TotalBytesSent)}  •  ↓ {DemoFileStorageService.FormatBytes(stats.TotalBytesReceived)}";
        ServerUptimeText = stats.IsRunning && stats.Uptime.TotalMinutes >= 1
            ? $"{(int)stats.Uptime.TotalDays:00}d {stats.Uptime.Hours:00}h"
            : "00d 00h";

        ConnectedClients.Clear();
        foreach (var c in await net.GetConnectedClientsAsync())
        {
            ConnectedClients.Add(c);
        }

        ConnectionLogs.Clear();
        foreach (var l in await net.GetConnectionLogsAsync(50))
        {
            ConnectionLogs.Add(l);
        }
    }

    private void ApplyUserFilter()
    {
        FilteredUsers.Clear();
        var q = AccountSearchQuery.Trim();
        foreach (var u in _allUsers)
        {
            var matchesFilter = SelectedAccountFilter switch
            {
                AccountFilterOption.Students => u.Role == UserRole.SinhVien,
                AccountFilterOption.Lecturers => u.Role == UserRole.GiangVien,
                AccountFilterOption.Admins => u.Role == UserRole.Admin,
                AccountFilterOption.Locked => u.IsLocked,
                _ => true
            };
            if (!matchesFilter)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(q)
                && !u.Username.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !u.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !u.Email.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            FilteredUsers.Add(u);
        }

        OnPropertyChanged(nameof(FilteredUsersSummary));
    }

    private void ApplyClassFilter()
    {
        FilteredClasses.Clear();
        var q = ClassSearchQuery.Trim();
        foreach (var c in _allClasses)
        {
            if (!string.IsNullOrEmpty(q)
                && !c.ClassCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !c.ClassName.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !c.Department.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !c.LecturerName.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !c.JoinCode.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            FilteredClasses.Add(c);
        }
    }

    private void ApplyRoomFilter()
    {
        FilteredRooms.Clear();
        var q = RoomSearchQuery.Trim();
        foreach (var r in _allRooms)
        {
            if (!string.IsNullOrEmpty(q)
                && !r.RoomName.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !r.OwnerName.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !r.JoinCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !r.RoomTypeLabel.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            FilteredRooms.Add(r);
        }
    }

    private async Task ToggleServerAsync()
    {
        var net = ClientServiceHost.Current.Network;
        if (IsServerRunning)
        {
            var res = await net.StopServerAsync();
            _notify(res.Message);
        }
        else
        {
            if (!TryParseServerEndpoint(out var ip, out var port)) return;
            var res = await net.StartServerAsync(ip, port);
            if (res.Success) _isServerConfigDirty = false;
            _notify(res.Message);
        }
        await RefreshTelemetryAsync();
    }

    private async Task ApplyServerConfigAsync()
    {
        if (!TryParseServerEndpoint(out var ip, out var port)) return;
        var net = ClientServiceHost.Current.Network;
        if (IsServerRunning)
        {
            await net.StopServerAsync();
        }
        var res = await net.StartServerAsync(ip, port);
        if (res.Success)
        {
            _isServerConfigDirty = false;
            _notify($"Đã áp dụng cấu hình Socket Server tại {ip}:{port}.");
        }
        else
        {
            _notify(res.Message);
        }
        await RefreshTelemetryAsync();
    }

    private bool TryParseServerEndpoint(out string ip, out int port)
    {
        ip = string.IsNullOrWhiteSpace(ServerListenIp) ? "127.0.0.1" : ServerListenIp.Trim();
        if (!int.TryParse(ServerListenPortText.Trim(), out port) || port is < 1 or > 65535)
        {
            _notify("Cổng TCP Server phải nằm trong khoảng 1 – 65535.");
            return false;
        }
        return true;
    }

    private async Task CleanupPendingQueueAsync()
    {
        var count = await ClientServiceHost.Current.Network.CleanupExpiredPendingPacketsAsync();
        _notify($"Đã dọn {count} gói tin chờ trong PendingQueue.");
        await RefreshTelemetryAsync();
    }

    private async Task ToggleLockAccountAsync(object? param)
    {
        if (param is not AccountItemModel account) return;
        if (account.UserId == ClientServiceHost.Current.AuthRoom.CurrentUser?.UserId)
        {
            _notify("Không thể tự khóa tài khoản Quản trị đang đăng nhập.");
            return;
        }

        if (account.IsLocked)
        {
            var res = await ClientServiceHost.Current.AuthRoom.UnlockUserAsync(account.UserId);
            _notify(res.Message);
            await RefreshAsync();
        }
        else
        {
            _onOpenLockAccount(account);
        }
    }

    private async Task ResetPasswordAsync(object? param)
    {
        if (param is not AccountItemModel account) return;
        var res = await ClientServiceHost.Current.AuthRoom.ResetPasswordAsync(account.UserId);
        _notify(res.Message);
    }

    private async Task CycleRoleAsync(object? param)
    {
        if (param is not AccountItemModel account) return;
        if (account.UserId == ClientServiceHost.Current.AuthRoom.CurrentUser?.UserId)
        {
            _notify("Không thể thay đổi vai trò của chính tài khoản Quản trị đang đăng nhập.");
            return;
        }

        var nextRole = account.Role switch
        {
            UserRole.SinhVien => UserRole.GiangVien,
            UserRole.GiangVien => UserRole.Admin,
            _ => UserRole.SinhVien
        };

        var res = await ClientServiceHost.Current.AuthRoom.AssignRoleAsync(account.UserId, nextRole);
        _notify($"{res.Message} ({account.Username} → {nextRole})");
        await RefreshAsync();
    }

    private async Task LinkClassRoomAsync(object? param)
    {
        if (param is not ClassItemModel cls) return;
        var res = await ClientServiceHost.Current.AuthRoom.CreateOrLinkClassRoomAsync(cls.ClassId, cls.ClassName);
        _notify(res.Message);
        await RefreshAsync();
    }

    private async Task DeleteClassAsync(object? param)
    {
        if (param is not ClassItemModel cls) return;
        var confirm = MessageBox.Show(
            $"Xóa lớp '{cls.ClassCode} - {cls.ClassName}' khỏi hệ thống?",
            "Xóa lớp học",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var res = await ClientServiceHost.Current.AuthRoom.DeleteClassAsync(cls.ClassId);
        _notify(res.Message);
        await RefreshAsync();
    }

    private async Task DeleteRoomAsync(object? param)
    {
        if (param is not RoomItemModel room) return;
        var confirm = MessageBox.Show(
            $"Giải tán và xóa vĩnh viễn phòng chat '{room.RoomName}'?",
            "Xóa phòng chat",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var res = await ClientServiceHost.Current.AuthRoom.DeleteRoomAsync(room.RoomId);
        _notify(res.Message);
        await RefreshAsync();
    }

    private async Task KickClientAsync(object? param)
    {
        if (param is not ClientConnectionDto client) return;
        if (client.UserId == ClientServiceHost.Current.AuthRoom.CurrentUser?.UserId)
        {
            _notify("Không thể tự ngắt phiên của chính Quản trị viên đang thao tác.");
            return;
        }

        var res = await ClientServiceHost.Current.Network.KickUserAsync(client.UserId, "Quản trị viên ngắt phiên từ Dashboard");
        _notify(res.Message);
        await RefreshTelemetryAsync();
    }
}
