using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DauChat.Client.UI.Infrastructure;
using DauChat.Client.UI.Models;
using DauChat.Client.UI.Services;
using DauChat.Shared.Enums;
using DauChat.Shared.Models;
using Microsoft.Win32;

namespace DauChat.Client.UI.ViewModels;

public sealed class ChatWorkspaceViewModel : ObservableObject
{
    private readonly Action<string> _notify;
    private readonly Action _onOpenCreateRoom;
    private readonly Action<RoomDto> _onOpenEditRoom;
    private readonly Action<string> _onOpenAddMember;
    private readonly Action<string, string?> _onOpenPinNotice;
    private readonly Action _onOpenJoinByCode;

    private string _searchQuery = string.Empty;
    private string _messageSearchQuery = string.Empty;
    private bool _showMessageSearch;
    private bool _roomsOnly;
    private bool _filterUnreadOnly;
    private ConversationItemModel? _selectedConversation;
    private string _draftMessage = string.Empty;
    private bool _isInfoDrawerOpen = true;
    private string? _pinnedNoticeContent;
    private string? _pinnedNoticeAuthor;
    private bool _hasPinnedNotice;
    private bool _isRefreshingSync;

    public ChatWorkspaceViewModel(
        Action<string> notify,
        Action onOpenCreateRoom,
        Action<RoomDto> onOpenEditRoom,
        Action<string> onOpenAddMember,
        Action<string, string?> onOpenPinNotice,
        Action onOpenJoinByCode)
    {
        _notify = notify;
        _onOpenCreateRoom = onOpenCreateRoom;
        _onOpenEditRoom = onOpenEditRoom;
        _onOpenAddMember = onOpenAddMember;
        _onOpenPinNotice = onOpenPinNotice;
        _onOpenJoinByCode = onOpenJoinByCode;

        SendMessageCommand = new AsyncRelayCommand(SendTextMessageAsync, () => CanSendMessages && !string.IsNullOrWhiteSpace(DraftMessage) && SelectedConversation is not null);
        AttachFileCommand = new AsyncRelayCommand(AttachAndSendFileAsync, () => CanSendMessages && SelectedConversation is not null);
        ToggleInfoDrawerCommand = new RelayCommand(() => IsInfoDrawerOpen = !IsInfoDrawerOpen);
        ToggleMessageSearchCommand = new RelayCommand(() =>
        {
            ShowMessageSearch = !ShowMessageSearch;
            if (!ShowMessageSearch) MessageSearchQuery = string.Empty;
        });
        ToggleFilterUnreadCommand = new RelayCommand(() =>
        {
            FilterUnreadOnly = !FilterUnreadOnly;
            ApplyConversationFilter();
        });
        SelectConversationCommand = new AsyncRelayCommand(async param =>
        {
            if (param is ConversationItemModel item)
            {
                await SelectConversationAsync(item);
            }
        });
        OpenCreateRoomCommand = new RelayCommand(() => _onOpenCreateRoom(), () => CanManageRooms);
        OpenEditRoomCommand = new RelayCommand(() =>
        {
            if (SelectedConversation?.RoomData is { } room)
            {
                _onOpenEditRoom(room);
            }
        }, () => CanEditOrDeleteSelectedRoom);
        DeleteRoomCommand = new AsyncRelayCommand(DeleteSelectedRoomAsync, () => CanEditOrDeleteSelectedRoom);
        OpenJoinByCodeCommand = new RelayCommand(() => _onOpenJoinByCode(), () => CanJoinByCode);
        CopyRoomJoinCodeCommand = new RelayCommand(CopyRoomJoinCode, () => SelectedConversation?.HasJoinCode == true);

        OpenAddMemberCommand = new RelayCommand(() =>
        {
            if (SelectedConversation is { IsRoom: true })
            {
                _onOpenAddMember(SelectedConversation.Id);
            }
        }, () => CanManageRooms && SelectedConversation is { IsRoom: true });

        RemoveMemberCommand = new AsyncRelayCommand(RemoveMemberFromRoomAsync);

        EditPinNoticeCommand = new RelayCommand(() =>
        {
            if (SelectedConversation is { IsRoom: true })
            {
                _onOpenPinNotice(SelectedConversation.Id, PinnedNoticeContent);
            }
        }, () => CanPinThisRoom);

        UnpinNoticeCommand = new AsyncRelayCommand(UnpinNoticeAsync, () => CanPinThisRoom && HasPinnedNotice);
        OpenFileCommand = new AsyncRelayCommand(async param =>
        {
            var path = param switch
            {
                MessageItemModel msg => msg.LocalFilePath ?? string.Empty,
                FileItemModel f => f.LocalFullPath,
                _ => string.Empty
            };
            var res = await ClientServiceHost.Current.FileStorage.OpenLocalFileAsync(path);
            _notify(res.Message);
        });
        ToggleMuteCommand = new RelayCommand(() =>
        {
            if (SelectedConversation is null) return;
            SelectedConversation.IsMuted = !SelectedConversation.IsMuted;
            _notify(SelectedConversation.IsMuted
                ? $"Đã tắt thông báo cho '{SelectedConversation.Title}'."
                : $"Đã bật lại thông báo cho '{SelectedConversation.Title}'.");
        });
        DeleteLocalHistoryCommand = new AsyncRelayCommand(DeleteLocalHistoryAsync, () => SelectedConversation is not null && CanSendMessages);
        LeaveRoomCommand = new AsyncRelayCommand(LeaveRoomAsync, () => CanLeaveSelectedRoom);

        var host = ClientServiceHost.Current;
        host.FileStorage.TransferProgressChanged += OnTransferProgressChanged;
        host.Messaging.PinnedAnnouncementChanged += OnPinnedAnnouncementChanged;
        host.Messaging.RoomMessageReceived += OnIncomingMessage;
        host.Messaging.DirectMessageReceived += OnIncomingMessage;
        host.AuthRoom.RoomUpdated += (_, _) => _ = RefreshFromExternalSyncAsync();
        host.AuthRoom.RoomDeleted += (_, _) => _ = RefreshFromExternalSyncAsync();
        host.DemoStore.ExternalSyncApplied += diff => { _ = RefreshFromExternalSyncAsync(); };
    }

    private List<ConversationItemModel> _allConversations = new();
    private List<MessageItemModel> _currentThreadMessages = new();

    public ObservableCollection<ConversationItemModel> FilteredConversations { get; } = new();
    public ObservableCollection<MessageItemModel> VisibleMessages { get; } = new();
    public ObservableCollection<FileTransferItemModel> ActiveTransfers { get; } = new();
    public ObservableCollection<RoomMemberItemModel> RoomMembers { get; } = new();
    public ObservableCollection<FileItemModel> RoomSharedFiles { get; } = new();

    public ICommand SendMessageCommand { get; }
    public ICommand AttachFileCommand { get; }
    public ICommand ToggleInfoDrawerCommand { get; }
    public ICommand ToggleMessageSearchCommand { get; }
    public ICommand ToggleFilterUnreadCommand { get; }
    public ICommand SelectConversationCommand { get; }
    public ICommand OpenCreateRoomCommand { get; }
    public ICommand OpenEditRoomCommand { get; }
    public ICommand DeleteRoomCommand { get; }
    public ICommand OpenJoinByCodeCommand { get; }
    public ICommand CopyRoomJoinCodeCommand { get; }
    public ICommand OpenAddMemberCommand { get; }
    public ICommand RemoveMemberCommand { get; }
    public ICommand EditPinNoticeCommand { get; }
    public ICommand UnpinNoticeCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand ToggleMuteCommand { get; }
    public ICommand DeleteLocalHistoryCommand { get; }
    public ICommand LeaveRoomCommand { get; }

    public bool IsAdminMode => ClientServiceHost.Current.AuthRoom.CurrentUser?.Role == UserRole.Admin;
    public bool CanSendMessages => !IsAdminMode;
    public bool CanManageRooms => ClientServiceHost.Current.AuthRoom.CurrentUser?.Role is UserRole.Admin or UserRole.GiangVien;
    public bool CanJoinByCode => ClientServiceHost.Current.AuthRoom.CurrentUser?.Role == UserRole.SinhVien;
    public bool CanPinThisRoom => ClientServiceHost.Current.AuthRoom.CurrentUser?.Role == UserRole.GiangVien
                                  && SelectedConversation?.IsRoom == true;
    public bool CanEditOrDeleteSelectedRoom => CanManageRooms && SelectedConversation?.IsRoom == true;
    public bool CanLeaveSelectedRoom => CanSendMessages
                                        && SelectedConversation is { IsRoom: true }
                                        && SelectedConversation.RoomData?.OwnerId != ClientServiceHost.Current.AuthRoom.CurrentUser?.UserId;

    public bool RoomsOnly
    {
        get => _roomsOnly;
        set
        {
            if (SetProperty(ref _roomsOnly, value))
            {
                OnPropertyChanged(nameof(SidebarHeading));
                ApplyConversationFilter();
            }
        }
    }

    public string SidebarHeading => IsAdminMode ? "Phòng chat" : (RoomsOnly ? "Phòng học" : "Tin nhắn");

    public bool FilterUnreadOnly
    {
        get => _filterUnreadOnly;
        set
        {
            if (SetProperty(ref _filterUnreadOnly, value))
            {
                OnPropertyChanged(nameof(FilterButtonLabel));
            }
        }
    }

    public string FilterButtonLabel => FilterUnreadOnly ? "Chưa đọc" : "Tất cả";

    public string ListCountSummary => RoomsOnly || IsAdminMode
        ? $"{FilteredConversations.Count} phòng hiện có"
        : "Gần đây";

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyConversationFilter();
            }
        }
    }

    public string MessageSearchQuery
    {
        get => _messageSearchQuery;
        set
        {
            if (SetProperty(ref _messageSearchQuery, value))
            {
                ApplyMessageSearchFilter();
            }
        }
    }

    public bool ShowMessageSearch
    {
        get => _showMessageSearch;
        set => SetProperty(ref _showMessageSearch, value);
    }

    public ConversationItemModel? SelectedConversation
    {
        get => _selectedConversation;
        set
        {
            if (SetProperty(ref _selectedConversation, value))
            {
                OnPropertyChanged(nameof(ComposerPlaceholder));
                OnPropertyChanged(nameof(InfoHeaderTitle));
                OnPropertyChanged(nameof(CanPinThisRoom));
                OnPropertyChanged(nameof(CanEditOrDeleteSelectedRoom));
                OnPropertyChanged(nameof(CanLeaveSelectedRoom));
            }
        }
    }

    public string ComposerPlaceholder => SelectedConversation is null
        ? "Chọn một hội thoại để bắt đầu"
        : $"Nhắn tin đến {SelectedConversation.Title}";

    public string InfoHeaderTitle => SelectedConversation?.IsRoom == true
        ? "Thông tin phòng"
        : "Thông tin hội thoại";

    public string DraftMessage
    {
        get => _draftMessage;
        set => SetProperty(ref _draftMessage, value);
    }

    public bool IsInfoDrawerOpen
    {
        get => _isInfoDrawerOpen;
        set => SetProperty(ref _isInfoDrawerOpen, value);
    }

    public bool HasPinnedNotice
    {
        get => _hasPinnedNotice;
        set => SetProperty(ref _hasPinnedNotice, value);
    }

    public string? PinnedNoticeContent
    {
        get => _pinnedNoticeContent;
        set => SetProperty(ref _pinnedNoticeContent, value);
    }

    public string? PinnedNoticeAuthor
    {
        get => _pinnedNoticeAuthor;
        set => SetProperty(ref _pinnedNoticeAuthor, value);
    }

    public int TotalUnreadCount => _allConversations.Sum(c => c.UnreadCount);

    public void RefreshRoleBindings()
    {
        OnPropertyChanged(nameof(IsAdminMode));
        OnPropertyChanged(nameof(CanSendMessages));
        OnPropertyChanged(nameof(CanManageRooms));
        OnPropertyChanged(nameof(CanJoinByCode));
        OnPropertyChanged(nameof(CanPinThisRoom));
        OnPropertyChanged(nameof(CanEditOrDeleteSelectedRoom));
        OnPropertyChanged(nameof(CanLeaveSelectedRoom));
        OnPropertyChanged(nameof(SidebarHeading));
        OnPropertyChanged(nameof(ListCountSummary));
    }

    public async Task LoadAsync(string? preferSelectId = null)
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null) return;

        RefreshRoleBindings();

        var host = ClientServiceHost.Current;
        var roomsRes = await host.Messaging.GetJoinedRoomsAsync(currentUser.UserId);
        var dms = IsAdminMode
            ? Array.Empty<ConversationSummaryDto>()
            : await host.Messaging.GetDirectConversationsAsync(currentUser.UserId);

        var combined = new List<ConversationItemModel>();

        if (roomsRes.Success && roomsRes.Data is not null)
        {
            foreach (var room in roomsRes.Data)
            {
                var msgs = await host.Messaging.GetRoomHistoryAsync(currentUser.UserId, room.RoomId, 0, 1);
                var last = msgs.LastOrDefault();
                var unread = host.DemoStore.UnreadByConversation.GetValueOrDefault(room.RoomId, 0);
                var codeTag = string.IsNullOrWhiteSpace(room.JoinCode) ? "Kênh lớp học" : $"Mã: {room.JoinCode}";
                combined.Add(new ConversationItemModel
                {
                    Id = room.RoomId,
                    Title = room.RoomName,
                    IsRoom = true,
                    Initials = DemoDataStore.GetInitials(room.RoomName),
                    AvatarBgHex = "#FFF1F1",
                    AvatarFgHex = "#B70C16",
                    Subtitle = last is null
                        ? (room.Description ?? "Phòng học nội bộ DAU")
                        : (last.Type == MessageType.FileAttachment ? $"[Tệp] {last.AttachmentFileName}" : $"{ShortFirstName(last.SenderName)}: {last.Content}"),
                    TimeLabel = FormatShortTime(last?.ServerTimestampUtc ?? room.LastActivityAtUtc),
                    UnreadCount = unread,
                    MetaCaption = $"{room.MemberIds.Count} thành viên • {codeTag}",
                    RoomData = room
                });
            }
        }

        var allUsersRes = await host.AuthRoom.GetAllUsersAsync();
        var userMap = allUsersRes.Data?.ToDictionary(u => u.UserId, u => u) ?? new();

        foreach (var dm in dms)
        {
            userMap.TryGetValue(dm.PartnerId, out var partner);
            var (bg, fg) = host.DemoStore.GetAvatarTone(dm.PartnerId);
            combined.Add(new ConversationItemModel
            {
                Id = dm.PartnerId,
                Title = dm.PartnerName,
                IsRoom = false,
                Initials = DemoDataStore.GetInitials(dm.PartnerName),
                AvatarBgHex = bg,
                AvatarFgHex = fg,
                Subtitle = dm.LastMessagePreview,
                TimeLabel = FormatShortTime(dm.LastMessageAtUtc),
                UnreadCount = dm.UnreadCount,
                IsOnline = dm.PartnerPresence == PresenceStatus.Online,
                MetaCaption = partner is null
                    ? "Thành viên DAU"
                    : $"{FormatRole(partner.Role)} • {(string.IsNullOrEmpty(partner.ClassName) ? "ĐH Kiến trúc Đà Nẵng" : partner.ClassName)}",
                PartnerData = partner
            });
        }

        _allConversations = combined;
        ApplyConversationFilter();
        OnPropertyChanged(nameof(TotalUnreadCount));

        var desiredId = preferSelectId ?? SelectedConversation?.Id;
        var target = _allConversations.FirstOrDefault(c => c.Id == desiredId && (!RoomsOnly || c.IsRoom))
                     ?? FilteredConversations.FirstOrDefault()
                     ?? _allConversations.FirstOrDefault();
        if (target is not null)
        {
            await SelectConversationAsync(target);
        }
        else
        {
            SelectedConversation = null;
            _currentThreadMessages.Clear();
            VisibleMessages.Clear();
            RoomMembers.Clear();
            RoomSharedFiles.Clear();
            SetPinnedNotice(null);
        }
    }

    public async Task SelectConversationAsync(ConversationItemModel target)
    {
        foreach (var item in _allConversations)
        {
            item.IsSelected = item.Id == target.Id;
        }

        SelectedConversation = target;
        target.UnreadCount = 0;
        ClientServiceHost.Current.DemoStore.UnreadByConversation[target.Id] = 0;
        OnPropertyChanged(nameof(TotalUnreadCount));

        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null) return;

        var host = ClientServiceHost.Current;
        IReadOnlyList<MessageDto> rawMsgs;
        if (target.IsRoom)
        {
            var entered = await host.Messaging.EnterRoomAsync(currentUser.UserId, target.Id);
            rawMsgs = entered.Success ? entered.Data.RecentMessages : Array.Empty<MessageDto>();
            var notice = entered.Success ? entered.Data.PinnedNotice : null;
            SetPinnedNotice(notice);

            var membersRes = await host.AuthRoom.GetRoomMembersAsync(target.Id);
            var ownerId = target.RoomData?.OwnerId;
            RoomMembers.Clear();
            if (membersRes.Success && membersRes.Data is not null)
            {
                foreach (var m in membersRes.Data)
                {
                    var (bg, fg) = host.DemoStore.GetAvatarTone(m.UserId);
                    RoomMembers.Add(new RoomMemberItemModel
                    {
                        UserId = m.UserId,
                        FullName = m.FullName,
                        Username = m.Username,
                        Initials = DemoDataStore.GetInitials(m.FullName),
                        AvatarBgHex = bg,
                        AvatarFgHex = fg,
                        Role = m.Role,
                        Presence = m.Presence,
                        CanBeRemoved = CanManageRooms && m.UserId != ownerId
                    });
                }
            }
        }
        else
        {
            SetPinnedNotice(null);
            RoomMembers.Clear();
            rawMsgs = await host.Messaging.GetDirectHistoryAsync(currentUser.UserId, target.Id);
        }

        var allUsers = (await host.AuthRoom.GetAllUsersAsync()).Data?.ToDictionary(u => u.UserId, u => u) ?? new();
        _currentThreadMessages = rawMsgs.Select(m => ToMessageItem(m, currentUser.UserId, allUsers)).ToList();
        ApplyMessageSearchFilter();

        var shared = await host.FileStorage.GetConversationFilesAsync(currentUser.UserId, target.Id);
        RoomSharedFiles.Clear();
        foreach (var f in shared)
        {
            RoomSharedFiles.Add(ToFileItem(f, target.Title));
        }
    }

    private void OnIncomingMessage(object? sender, MessageDto msg)
    {
        Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
            if (currentUser is null) return;

            var convId = msg.IsRoomMessage
                ? (msg.RoomId ?? msg.ConversationId)
                : (msg.SenderId == currentUser.UserId ? (msg.ReceiverId ?? string.Empty) : msg.SenderId);

            var conv = _allConversations.FirstOrDefault(c => c.Id == convId);
            if (conv is null)
            {
                await LoadAsync(SelectedConversation?.Id);
                return;
            }

            conv.Subtitle = msg.Type == MessageType.FileAttachment
                ? $"[Tệp] {msg.AttachmentFileName}"
                : (msg.IsRoomMessage
                    ? (msg.SenderId == currentUser.UserId ? $"Bạn: {msg.Content}" : $"{ShortFirstName(msg.SenderName)}: {msg.Content}")
                    : msg.Content);
            conv.TimeLabel = FormatShortTime(msg.ServerTimestampUtc);

            if (SelectedConversation?.Id == convId)
            {
                if (_currentThreadMessages.All(m => m.MessageId != msg.MessageId))
                {
                    var users = (await ClientServiceHost.Current.AuthRoom.GetAllUsersAsync()).Data?.ToDictionary(u => u.UserId, u => u) ?? new();
                    _currentThreadMessages.Add(ToMessageItem(msg, currentUser.UserId, users));
                    ApplyMessageSearchFilter();
                }

                if (msg.Type == MessageType.FileAttachment)
                {
                    var shared = await ClientServiceHost.Current.FileStorage.GetConversationFilesAsync(currentUser.UserId, convId);
                    RoomSharedFiles.Clear();
                    foreach (var f in shared)
                    {
                        RoomSharedFiles.Add(ToFileItem(f, conv.Title));
                    }
                }
            }
            else if (msg.SenderId != currentUser.UserId)
            {
                conv.UnreadCount++;
                ClientServiceHost.Current.DemoStore.UnreadByConversation[convId] = conv.UnreadCount;
                OnPropertyChanged(nameof(TotalUnreadCount));
            }
        });
    }

    private async Task RefreshFromExternalSyncAsync()
    {
        if (_isRefreshingSync || ClientServiceHost.Current.AuthRoom.CurrentUser is null) return;
        _isRefreshingSync = true;
        try
        {
            if (Application.Current?.Dispatcher is not { } dispatcher) return;
            await dispatcher.InvokeAsync(async () =>
            {
                if (ClientServiceHost.Current.AuthRoom.CurrentUser is null) return;
                await LoadAsync(SelectedConversation?.Id);
            }).Task.Unwrap();
        }
        finally
        {
            _isRefreshingSync = false;
        }
    }

    private async Task SendTextMessageAsync()
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || SelectedConversation is null || !CanSendMessages) return;

        var text = DraftMessage.Trim();
        if (string.IsNullOrEmpty(text)) return;
        DraftMessage = string.Empty;

        var host = ClientServiceHost.Current;
        var res = SelectedConversation.IsRoom
            ? await host.Messaging.SendRoomMessageAsync(currentUser.UserId, SelectedConversation.Id, text)
            : await host.Messaging.SendDirectMessageAsync(currentUser.UserId, SelectedConversation.Id, text);

        if (res.Success && res.Data is not null)
        {
            if (_currentThreadMessages.All(m => m.MessageId != res.Data.MessageId))
            {
                var users = (await host.AuthRoom.GetAllUsersAsync()).Data?.ToDictionary(u => u.UserId, u => u) ?? new();
                _currentThreadMessages.Add(ToMessageItem(res.Data, currentUser.UserId, users));
                ApplyMessageSearchFilter();
            }
            SelectedConversation.Subtitle = SelectedConversation.IsRoom ? $"Bạn: {text}" : text;
            SelectedConversation.TimeLabel = "Vừa xong";
        }
    }

    private async Task AttachAndSendFileAsync()
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || SelectedConversation is null || !CanSendMessages) return;

        var dlg = new OpenFileDialog
        {
            Title = "Chọn tài liệu hoặc bản vẽ gửi qua DAU Chat (Chunked 64KB + SHA-256)",
            Filter = "Tất cả tệp (*.*)|*.*|Bản vẽ CAD (*.dwg;*.dxf;*.rvt;*.skp)|*.dwg;*.dxf;*.rvt;*.skp|PDF & Văn phòng (*.pdf;*.docx;*.xlsx)|*.pdf;*.docx;*.xlsx|Tệp nén (*.zip;*.rar)|*.zip;*.rar|Hình ảnh (*.png;*.jpg)|*.png;*.jpg"
        };

        if (dlg.ShowDialog() != true) return;

        var host = ClientServiceHost.Current;
        var res = await host.FileStorage.SendFileAsync(
            currentUser.UserId,
            SelectedConversation.Id,
            SelectedConversation.IsRoom,
            dlg.FileName);

        if (!res.Success || res.Data is null)
        {
            _notify(res.Message);
            return;
        }

        var manifest = res.Data;
        var msgRes = SelectedConversation.IsRoom
            ? await host.Messaging.SendRoomMessageAsync(
                currentUser.UserId,
                SelectedConversation.Id,
                manifest.FileName,
                MessageType.FileAttachment,
                manifest.FileId,
                manifest.FileName,
                manifest.FileSizeBytes)
            : await host.Messaging.SendDirectMessageAsync(
                currentUser.UserId,
                SelectedConversation.Id,
                manifest.FileName,
                MessageType.FileAttachment,
                manifest.FileId,
                manifest.FileName,
                manifest.FileSizeBytes);

        if (msgRes.Success && msgRes.Data is not null)
        {
            msgRes.Data.LocalFilePath = manifest.LocalFullPath;
            if (_currentThreadMessages.All(m => m.MessageId != msgRes.Data.MessageId))
            {
                var users = (await host.AuthRoom.GetAllUsersAsync()).Data?.ToDictionary(u => u.UserId, u => u) ?? new();
                _currentThreadMessages.Add(ToMessageItem(msgRes.Data, currentUser.UserId, users));
                ApplyMessageSearchFilter();
            }
            if (RoomSharedFiles.All(f => f.FileId != manifest.FileId))
            {
                RoomSharedFiles.Insert(0, ToFileItem(manifest, SelectedConversation.Title));
            }
            SelectedConversation.Subtitle = $"[Tệp] {manifest.FileName}";
            SelectedConversation.TimeLabel = "Vừa xong";
            _notify($"Đã gửi tệp '{manifest.FileName}' kèm mã kiểm tra SHA-256.");
        }
    }

    private void CopyRoomJoinCode()
    {
        if (SelectedConversation is null || !SelectedConversation.HasJoinCode) return;
        try
        {
            Clipboard.SetText(SelectedConversation.RoomJoinCode);
            _notify($"Đã sao chép mã tham gia '{SelectedConversation.RoomJoinCode}'.");
        }
        catch
        {
            _notify($"Mã lớp / phòng: {SelectedConversation.RoomJoinCode}");
        }
    }

    private async Task RemoveMemberFromRoomAsync(object? param)
    {
        if (!CanManageRooms || SelectedConversation is not { IsRoom: true } || param is not RoomMemberItemModel member)
        {
            return;
        }

        var res = await ClientServiceHost.Current.AuthRoom.RemoveRoomMemberAsync(SelectedConversation.Id, member.UserId);
        _notify(res.Message);
        if (res.Success)
        {
            await LoadAsync(SelectedConversation.Id);
        }
    }

    private async Task DeleteSelectedRoomAsync()
    {
        if (!CanEditOrDeleteSelectedRoom || SelectedConversation is null) return;
        var roomTitle = SelectedConversation.Title;
        var roomId = SelectedConversation.Id;
        var confirm = MessageBox.Show(
            $"Giải tán và xóa vĩnh viễn phòng '{roomTitle}'?",
            "Xóa phòng chat",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var res = await ClientServiceHost.Current.AuthRoom.DeleteRoomAsync(roomId);
        _notify(res.Message);
        if (res.Success)
        {
            await LoadAsync();
        }
    }

    private async Task UnpinNoticeAsync()
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || SelectedConversation is not { IsRoom: true } || !CanPinThisRoom) return;

        var res = await ClientServiceHost.Current.Messaging.UnpinRoomAnnouncementAsync(SelectedConversation.Id, currentUser.UserId);
        if (res.Success)
        {
            SetPinnedNotice(null);
            _notify("Đã bỏ ghim thông báo.");
        }
    }

    private async Task DeleteLocalHistoryAsync()
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || SelectedConversation is null) return;

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa toàn bộ lịch sử tin nhắn trên máy của \"{SelectedConversation.Title}\"?\n(Chỉ xóa file .jsonl trên máy trạm, không ảnh hưởng máy người khác).",
            "Xác nhận xóa lịch sử Local",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var res = SelectedConversation.IsRoom
            ? await ClientServiceHost.Current.Messaging.DeleteRoomHistoryAsync(currentUser.UserId, SelectedConversation.Id)
            : await ClientServiceHost.Current.Messaging.DeleteDirectHistoryAsync(currentUser.UserId, SelectedConversation.Id);

        if (res.Success)
        {
            _currentThreadMessages.Clear();
            VisibleMessages.Clear();
            SelectedConversation.Subtitle = "Đã xóa lịch sử trên máy";
            _notify(res.Message);
        }
    }

    private async Task LeaveRoomAsync()
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || SelectedConversation is not { IsRoom: true }) return;

        var confirm = MessageBox.Show(
            $"Bạn muốn rời khỏi phòng \"{SelectedConversation.Title}\"?",
            "Rời khỏi phòng chat",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var res = await ClientServiceHost.Current.Messaging.LeaveRoomAsync(currentUser.UserId, SelectedConversation.Id);
        if (res.Success)
        {
            _notify(res.Message);
            await LoadAsync();
        }
    }

    private void OnTransferProgressChanged(object? sender, FileTransferProgressDto e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var existing = ActiveTransfers.FirstOrDefault(t => t.TransferId == e.TransferId);
            if (existing is null)
            {
                existing = new FileTransferItemModel
                {
                    TransferId = e.TransferId,
                    FileName = e.FileName,
                    TotalSizeText = DemoFileStorageService.FormatBytes(e.TotalBytes),
                    Percentage = e.Percentage,
                    StatusText = $"Đang truyền theo luồng 64KB • {DemoFileStorageService.FormatBytes(e.TransferredBytes)} / {DemoFileStorageService.FormatBytes(e.TotalBytes)}"
                };
                ActiveTransfers.Add(existing);
            }
            else
            {
                existing.Percentage = e.Percentage;
                existing.IsCompleted = e.IsCompleted;
                existing.StatusText = e.IsCompleted
                    ? $"Hoàn tất • Khớp SHA-256 ({e.TotalBytes:#,0} B)"
                    : $"Đang truyền theo luồng 64KB • {DemoFileStorageService.FormatBytes(e.TransferredBytes)} / {DemoFileStorageService.FormatBytes(e.TotalBytes)}";
            }

            if (e.IsCompleted)
            {
                _ = Task.Delay(1400).ContinueWith(_ =>
                    Application.Current?.Dispatcher.Invoke(() => ActiveTransfers.Remove(existing)));
            }
        });
    }

    private void OnPinnedAnnouncementChanged(object? sender, (string RoomId, PinnedNoticeDto? PinnedNotice) args)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (SelectedConversation?.Id == args.RoomId)
            {
                SetPinnedNotice(args.PinnedNotice);
            }
        });
    }

    private void SetPinnedNotice(PinnedNoticeDto? notice)
    {
        HasPinnedNotice = notice is not null;
        PinnedNoticeContent = notice?.Content;
        PinnedNoticeAuthor = notice is null ? null : $"Ghim bởi {notice.PinnedByName}";
    }

    private void ApplyConversationFilter()
    {
        FilteredConversations.Clear();
        var q = SearchQuery.Trim();
        foreach (var item in _allConversations)
        {
            if ((RoomsOnly || IsAdminMode) && !item.IsRoom) continue;
            if (FilterUnreadOnly && item.UnreadCount == 0) continue;
            if (!string.IsNullOrEmpty(q)
                && !item.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !item.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !item.RoomJoinCode.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            FilteredConversations.Add(item);
        }

        OnPropertyChanged(nameof(ListCountSummary));
    }

    private void ApplyMessageSearchFilter()
    {
        VisibleMessages.Clear();
        var q = MessageSearchQuery.Trim();
        foreach (var msg in _currentThreadMessages)
        {
            if (!string.IsNullOrEmpty(q) && !msg.Content.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            VisibleMessages.Add(msg);
        }
    }

    private MessageItemModel ToMessageItem(MessageDto dto, string currentUserId, Dictionary<string, UserDto> users)
    {
        var isOwn = dto.SenderId == currentUserId;
        users.TryGetValue(dto.SenderId, out var sender);
        var (bg, fg) = ClientServiceHost.Current.DemoStore.GetAvatarTone(dto.SenderId);
        var ext = Path.GetExtension(dto.AttachmentFileName ?? string.Empty).ToLowerInvariant();
        var (badge, bBg, bFg) = ext switch
        {
            ".pdf" => ("PDF", "#FEF2F2", "#DC2626"),
            ".dwg" or ".dxf" or ".rvt" or ".skp" => ("CAD", "#EFF6FF", "#2563EB"),
            ".zip" or ".rar" or ".7z" => ("ZIP", "#FFFBEB", "#D97706"),
            ".png" or ".jpg" or ".jpeg" => ("ẢNH", "#F5F3FF", "#7C3AED"),
            _ => ("TỆP", "#F1F5F9", "#475569")
        };

        return new MessageItemModel
        {
            MessageId = dto.MessageId,
            SenderId = dto.SenderId,
            SenderName = dto.SenderName,
            SenderInitials = DemoDataStore.GetInitials(dto.SenderName),
            AvatarBgHex = bg,
            AvatarFgHex = fg,
            SenderRoleLabel = sender?.Role == UserRole.GiangVien ? "GIẢNG VIÊN"
                : (sender?.Role == UserRole.Admin ? "QUẢN TRỊ" : string.Empty),
            IsOwnMessage = isOwn,
            IsAttachment = dto.Type == MessageType.FileAttachment,
            IsSystemNotice = dto.Type == MessageType.SystemNotice,
            Content = dto.Content,
            TimeText = dto.ServerTimestampUtc.ToLocalTime().ToString("HH:mm"),
            AttachmentFileId = dto.AttachmentFileId,
            AttachmentFileName = dto.AttachmentFileName,
            AttachmentSizeText = dto.AttachmentFileSize.HasValue
                ? $"{DemoFileStorageService.FormatBytes(dto.AttachmentFileSize.Value)} • {badge}"
                : badge,
            AttachmentBadgeText = badge,
            AttachmentBadgeBgHex = bBg,
            AttachmentBadgeFgHex = bFg,
            LocalFilePath = dto.LocalFilePath,
            IsPinned = dto.IsPinned
        };
    }

    private static FileItemModel ToFileItem(FileManifestDto f, string roomName)
    {
        var (bg, fg) = f.Category switch
        {
            FileCategory.PDF => ("#FEF2F2", "#DC2626"),
            FileCategory.CAD => ("#EFF6FF", "#2563EB"),
            FileCategory.Archive => ("#FFFBEB", "#D97706"),
            FileCategory.Image => ("#F5F3FF", "#7C3AED"),
            _ => ("#ECFDF5", "#047857")
        };

        return new FileItemModel
        {
            FileId = f.FileId,
            FileName = f.FileName,
            Category = f.Category,
            CategoryLabel = f.Category.ToString().ToUpperInvariant(),
            SizeAndTimeText = $"{DemoFileStorageService.FormatBytes(f.FileSizeBytes)} • {f.DownloadedAtUtc.ToLocalTime():dd/MM HH:mm}",
            ConversationName = roomName,
            SenderName = f.SenderName,
            Sha256Short = f.Sha256Checksum.Length >= 10 ? $"SHA-256: {f.Sha256Checksum[..10]}..." : "SHA-256",
            LocalFullPath = f.LocalFullPath,
            IconBgHex = bg,
            IconFgHex = fg
        };
    }

    private static string ShortFirstName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : fullName;
    }

    private static string FormatShortTime(DateTime? utc)
    {
        if (!utc.HasValue) return "";
        var local = utc.Value.ToLocalTime();
        return local.Date == DateTime.Today ? local.ToString("HH:mm") : local.ToString("dd/MM");
    }

    private static string FormatRole(UserRole role) => role switch
    {
        UserRole.Admin => "Quản trị viên",
        UserRole.GiangVien => "Giảng viên",
        _ => "Sinh viên"
    };
}
