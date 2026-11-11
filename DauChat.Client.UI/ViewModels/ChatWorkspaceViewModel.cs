using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
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
    private MessageItemModel? _replyingToMessage;
    private int _unseenScrollCount;

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
        OpenCreateRoomCommand = new RelayCommand(() => _onOpenCreateRoom(), () => CanCreateRoom);
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
        }, () => CanManageSelectedRoomMembers);

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
            var path = string.Empty;
            if (param is MessageItemModel msg)
            {
                path = ResolveAttachmentLocalPath(msg.LocalFilePath, msg.AttachmentFileId) ?? string.Empty;
            }
            else if (param is FileItemModel f)
            {
                path = f.LocalFullPath;
            }

            var res = await ClientServiceHost.Current.FileStorage.OpenLocalFileAsync(path);
            _notify(res.Message);
        });
        ToggleMuteCommand = new RelayCommand(() =>
        {
            if (SelectedConversation is null || ClientServiceHost.Current.AuthRoom.CurrentUser is not { } cu) return;
            var nextMuted = !SelectedConversation.IsMuted;
            SelectedConversation.IsMuted = nextMuted;
            ClientServiceHost.Current.DemoStore.SetConversationMuted(cu.UserId, SelectedConversation.Id, nextMuted);
            OnPropertyChanged(nameof(IsSelectedConversationMuted));
            OnPropertyChanged(nameof(SelectedConversationMuteLabel));
            _notify(nextMuted
                ? $"Đã tắt thông báo cho '{SelectedConversation.Title}'."
                : $"Đã bật lại thông báo cho '{SelectedConversation.Title}'.");
        });
        DeleteLocalHistoryCommand = new AsyncRelayCommand(DeleteLocalHistoryAsync, () => SelectedConversation is not null && CanSendMessages);
        LeaveRoomCommand = new AsyncRelayCommand(LeaveRoomAsync, () => CanLeaveSelectedRoom);

        ReplyToMessageCommand = new RelayCommand(param =>
        {
            if (param is MessageItemModel msg && !msg.IsSystemNotice && !msg.IsRecalled && CanSendMessages)
            {
                ReplyingToMessage = msg;
                FocusComposerRequested?.Invoke();
            }
        });
        CancelReplyCommand = new RelayCommand(() => ReplyingToMessage = null);
        CopyMessageCommand = new RelayCommand(param =>
        {
            if (param is not MessageItemModel msg || msg.IsRecalled || msg.IsSystemNotice) return;
            var copyText = msg.IsAttachment ? (msg.AttachmentFileName ?? msg.BodyContent) : msg.BodyContent;
            if (string.IsNullOrWhiteSpace(copyText)) return;
            try
            {
                Clipboard.SetText(copyText);
                _notify("Đã sao chép nội dung tin nhắn.");
            }
            catch
            {
                _notify("Không thể truy cập Clipboard của hệ thống.");
            }
        });
        PinMessageCommand = new AsyncRelayCommand(PinSingleMessageAsync);
        RecallMessageCommand = new AsyncRelayCommand(RecallMessageAsync);
        DeleteSingleMessageLocalCommand = new RelayCommand(DeleteSingleMessageLocal);
        JumpToLatestCommand = new RelayCommand(() =>
        {
            ClearUnseenScrollIndicator();
            ScrollToEndRequested?.Invoke();
        });

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
    public ICommand ReplyToMessageCommand { get; }
    public ICommand CancelReplyCommand { get; }
    public ICommand CopyMessageCommand { get; }
    public ICommand PinMessageCommand { get; }
    public ICommand RecallMessageCommand { get; }
    public ICommand DeleteSingleMessageLocalCommand { get; }
    public ICommand JumpToLatestCommand { get; }

    public event Action? ScrollToEndRequested;
    public event Action<bool>? ThreadMessageAppended;
    public event Action? FocusComposerRequested;

    public bool IsAdminMode => ClientServiceHost.Current.AuthRoom.CurrentUser?.Role == UserRole.Admin;
    public bool CanSendMessages => !IsAdminMode;
    public bool CanManageRooms => ClientServiceHost.Current.AuthRoom.CurrentUser?.Role is UserRole.Admin or UserRole.GiangVien;
    public bool CanCreateRoom => ClientServiceHost.Current.AuthRoom.CurrentUser is not null;
    public bool CanJoinByCode => ClientServiceHost.Current.AuthRoom.CurrentUser?.Role == UserRole.SinhVien;
    public string CreateRoomTooltip => IsAdminMode || CanManageRooms ? "Tạo phòng học mới" : "Tạo nhóm học tập mới";
    public bool CanPinThisRoom => ClientServiceHost.Current.AuthRoom.CurrentUser?.Role == UserRole.GiangVien
                                  && SelectedConversation?.IsRoom == true;
    public bool CanManageSelectedRoomMembers => SelectedConversation is { IsRoom: true }
                                                && (CanManageRooms
                                                    || (SelectedConversation.RoomData?.RoomType == RoomType.StudyGroup
                                                        && SelectedConversation.RoomData?.OwnerId == ClientServiceHost.Current.AuthRoom.CurrentUser?.UserId));
    public bool CanEditOrDeleteSelectedRoom => CanManageSelectedRoomMembers;
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
                OnPropertyChanged(nameof(CanManageSelectedRoomMembers));
                OnPropertyChanged(nameof(CanEditOrDeleteSelectedRoom));
                OnPropertyChanged(nameof(CanLeaveSelectedRoom));
                OnPropertyChanged(nameof(IsSelectedConversationMuted));
                OnPropertyChanged(nameof(SelectedConversationMuteLabel));
            }
        }
    }

    public bool IsSelectedConversationMuted => SelectedConversation?.IsMuted == true;
    public string SelectedConversationMuteLabel => SelectedConversation?.IsMuted == true ? "Đã tắt tiếng" : "Thông báo";

    public MessageItemModel? ReplyingToMessage
    {
        get => _replyingToMessage;
        set
        {
            if (SetProperty(ref _replyingToMessage, value))
            {
                OnPropertyChanged(nameof(HasReplyingTo));
                OnPropertyChanged(nameof(ReplyingToAuthor));
                OnPropertyChanged(nameof(ReplyingToSnippet));
            }
        }
    }

    public bool HasReplyingTo => ReplyingToMessage is not null;
    public string ReplyingToAuthor => ReplyingToMessage?.SenderName ?? string.Empty;
    public string ReplyingToSnippet
    {
        get
        {
            if (ReplyingToMessage is null) return string.Empty;
            var txt = ReplyingToMessage.IsAttachment
                ? $"[Tệp] {ReplyingToMessage.AttachmentFileName}"
                : ReplyingToMessage.BodyContent.Replace("\r", " ").Replace("\n", " ").Trim();
            return txt.Length > 75 ? txt[..75] + "..." : txt;
        }
    }

    public bool HasUnseenScrollMessages => _unseenScrollCount > 0;
    public string UnseenScrollPillText => _unseenScrollCount > 1 ? $"{_unseenScrollCount} tin nhắn mới" : "Tin nhắn mới";

    public void NotifyUnseenScrollMessageArrived()
    {
        _unseenScrollCount++;
        OnPropertyChanged(nameof(HasUnseenScrollMessages));
        OnPropertyChanged(nameof(UnseenScrollPillText));
    }

    public void ClearUnseenScrollIndicator()
    {
        if (_unseenScrollCount == 0) return;
        _unseenScrollCount = 0;
        OnPropertyChanged(nameof(HasUnseenScrollMessages));
        OnPropertyChanged(nameof(UnseenScrollPillText));
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
        OnPropertyChanged(nameof(CanCreateRoom));
        OnPropertyChanged(nameof(CreateRoomTooltip));
        OnPropertyChanged(nameof(CanJoinByCode));
        OnPropertyChanged(nameof(CanPinThisRoom));
        OnPropertyChanged(nameof(CanManageSelectedRoomMembers));
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
                        : (last.Type == MessageType.FileAttachment
                            ? $"[Tệp] {last.AttachmentFileName}"
                            : $"{(last.SenderId == currentUser.UserId ? "Bạn" : ShortFirstName(last.SenderName))}: {MessageQuoteCodec.FormatSidebarPreview(last.Content)}"),
                    TimeLabel = FormatShortTime(last?.ServerTimestampUtc ?? room.LastActivityAtUtc),
                    UnreadCount = unread,
                    IsMuted = host.DemoStore.IsConversationMuted(currentUser.UserId, room.RoomId),
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
                IsMuted = host.DemoStore.IsConversationMuted(currentUser.UserId, dm.PartnerId),
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
        var isSameConv = _selectedConversation?.Id == target.Id;

        foreach (var item in _allConversations)
        {
            item.IsSelected = item.Id == target.Id;
        }

        SelectedConversation = target;
        if (!isSameConv)
        {
            ReplyingToMessage = null;
            ClearUnseenScrollIndicator();
        }
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
            var canRemoveThisRoomMembers = CanManageSelectedRoomMembers;
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
                        CanBeRemoved = canRemoveThisRoomMembers && m.UserId != ownerId
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
        if (ReplyingToMessage is not null)
        {
            var activeReply = _currentThreadMessages.FirstOrDefault(m => m.MessageId == ReplyingToMessage.MessageId);
            if (activeReply is null || activeReply.IsRecalled)
            {
                ReplyingToMessage = null;
            }
        }
        ApplyMessageSearchFilter();
        if (!isSameConv)
        {
            ScrollToEndRequested?.Invoke();
        }

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

            var previewText = MessageQuoteCodec.FormatSidebarPreview(msg.Content);
            conv.Subtitle = msg.Type == MessageType.FileAttachment
                ? $"[Tệp] {msg.AttachmentFileName}"
                : (msg.IsRoomMessage
                    ? (msg.SenderId == currentUser.UserId ? $"Bạn: {previewText}" : $"{ShortFirstName(msg.SenderName)}: {previewText}")
                    : previewText);
            conv.TimeLabel = FormatShortTime(msg.ServerTimestampUtc);

            if (SelectedConversation?.Id == convId)
            {
                if (_currentThreadMessages.All(m => m.MessageId != msg.MessageId))
                {
                    var users = (await ClientServiceHost.Current.AuthRoom.GetAllUsersAsync()).Data?.ToDictionary(u => u.UserId, u => u) ?? new();
                    var item = ToMessageItem(msg, currentUser.UserId, users);
                    _currentThreadMessages.Add(item);
                    AppendVisibleMessageIfMatchesFilter(item);
                    ThreadMessageAppended?.Invoke(msg.SenderId == currentUser.UserId);
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
            else if (msg.SenderId != currentUser.UserId && !conv.IsMuted)
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

        var payload = ReplyingToMessage is not null
            ? MessageQuoteCodec.Encode(
                ReplyingToMessage.SenderName,
                ReplyingToMessage.IsAttachment ? $"[Tệp] {ReplyingToMessage.AttachmentFileName}" : ReplyingToMessage.BodyContent,
                text)
            : text;

        DraftMessage = string.Empty;
        ReplyingToMessage = null;

        var host = ClientServiceHost.Current;
        var res = SelectedConversation.IsRoom
            ? await host.Messaging.SendRoomMessageAsync(currentUser.UserId, SelectedConversation.Id, payload)
            : await host.Messaging.SendDirectMessageAsync(currentUser.UserId, SelectedConversation.Id, payload);

        if (res.Success && res.Data is not null)
        {
            if (_currentThreadMessages.All(m => m.MessageId != res.Data.MessageId))
            {
                var users = (await host.AuthRoom.GetAllUsersAsync()).Data?.ToDictionary(u => u.UserId, u => u) ?? new();
                var item = ToMessageItem(res.Data, currentUser.UserId, users);
                _currentThreadMessages.Add(item);
                AppendVisibleMessageIfMatchesFilter(item);
            }
            var preview = MessageQuoteCodec.FormatSidebarPreview(payload);
            SelectedConversation.Subtitle = SelectedConversation.IsRoom ? $"Bạn: {preview}" : preview;
            SelectedConversation.TimeLabel = "Vừa xong";
            ScrollToEndRequested?.Invoke();
        }
    }

    private async Task AttachAndSendFileAsync()
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || SelectedConversation is null || !CanSendMessages) return;

        var dlg = new OpenFileDialog
        {
            Title = "Chọn tài liệu hoặc bản vẽ gửi qua DAU Chat (Chunked 64KB + SHA-256)",
            Multiselect = true,
            Filter = "Tất cả tệp (*.*)|*.*|Hình ảnh (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Bản vẽ CAD (*.dwg;*.dxf;*.rvt;*.skp)|*.dwg;*.dxf;*.rvt;*.skp|PDF & Văn phòng (*.pdf;*.docx;*.xlsx)|*.pdf;*.docx;*.xlsx|Tệp nén (*.zip;*.rar)|*.zip;*.rar"
        };

        if (dlg.ShowDialog() != true) return;
        await SendFilesFromPathsAsync(dlg.FileNames);
    }

    public async Task SendClipboardImageAsync(BitmapSource image)
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || SelectedConversation is null || !CanSendMessages) return;

        try
        {
            var mediaDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DauChat",
                "PastedMedia");
            Directory.CreateDirectory(mediaDir);

            var pngPath = Path.Combine(
                mediaDir,
                $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..4]}.png");

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            await using (var fs = File.Create(pngPath))
            {
                encoder.Save(fs);
            }

            await SendFilesFromPathsAsync(new[] { pngPath });
        }
        catch (Exception ex)
        {
            _notify($"Không thể dán ảnh từ Clipboard: {ex.Message}");
        }
    }

    public async Task SendFilesFromPathsAsync(IEnumerable<string> filePaths)
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || SelectedConversation is null || !CanSendMessages) return;

        var host = ClientServiceHost.Current;
        var sentCount = 0;

        foreach (var filePath in filePaths.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)))
        {
            var res = await host.FileStorage.SendFileAsync(
                currentUser.UserId,
                SelectedConversation.Id,
                SelectedConversation.IsRoom,
                filePath);

            if (!res.Success || res.Data is null)
            {
                _notify(res.Message);
                continue;
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
                    var item = ToMessageItem(msgRes.Data, currentUser.UserId, users);
                    _currentThreadMessages.Add(item);
                    AppendVisibleMessageIfMatchesFilter(item);
                }
                if (RoomSharedFiles.All(f => f.FileId != manifest.FileId))
                {
                    RoomSharedFiles.Insert(0, ToFileItem(manifest, SelectedConversation.Title));
                }
                SelectedConversation.Subtitle = $"[Tệp] {manifest.FileName}";
                SelectedConversation.TimeLabel = "Vừa xong";
                sentCount++;
            }
        }

        if (sentCount > 0)
        {
            ScrollToEndRequested?.Invoke();
            _notify(sentCount == 1
                ? $"Đã gửi tệp '{SelectedConversation.Subtitle[6..]}' kèm mã kiểm tra SHA-256."
                : $"Đã gửi thành công {sentCount} tệp kèm xác thực SHA-256.");
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

    private async Task PinSingleMessageAsync(object? param)
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || SelectedConversation is not { IsRoom: true } || !CanPinThisRoom || param is not MessageItemModel msg || msg.IsRecalled || msg.IsSystemNotice)
        {
            return;
        }

        var res = await ClientServiceHost.Current.Messaging.PinRoomAnnouncementAsync(
            SelectedConversation.Id,
            currentUser.UserId,
            msg.MessageId,
            isMessageId: true);
        _notify(res.Message);
    }

    private async Task RecallMessageAsync(object? param)
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || SelectedConversation is null || !CanSendMessages || param is not MessageItemModel msg)
        {
            return;
        }

        if (!msg.IsOwnMessage || msg.IsSystemNotice || msg.IsRecalled)
        {
            _notify("Chỉ có thể thu hồi tin nhắn chưa thu hồi do chính bạn gửi.");
            return;
        }

        var conv = SelectedConversation;
        var res = await ClientServiceHost.Current.DemoMessaging.RecallMessageAsync(
            currentUser.UserId,
            conv.Id,
            conv.IsRoom,
            msg.MessageId);

        if (!res.Success)
        {
            _notify(res.Message);
            return;
        }

        if (ReplyingToMessage?.MessageId == msg.MessageId)
        {
            ReplyingToMessage = null;
        }

        var utcTs = DateTime.SpecifyKind(msg.LocalTimestamp, DateTimeKind.Local).ToUniversalTime();
        var recalledDto = new MessageDto
        {
            MessageId = msg.MessageId,
            ConversationId = conv.IsRoom ? conv.Id : DemoDataStore.GetDirectConversationKey(currentUser.UserId, conv.Id),
            RoomId = conv.IsRoom ? conv.Id : null,
            ReceiverId = conv.IsRoom ? null : conv.Id,
            SenderId = msg.SenderId,
            SenderName = msg.SenderName,
            Content = MessageQuoteCodec.RecalledMarker,
            Type = MessageType.Text,
            IsRoomMessage = conv.IsRoom,
            IsPinned = false,
            SentAtClientUtc = utcTs,
            ServerTimestampUtc = utcTs
        };

        var users = (await ClientServiceHost.Current.AuthRoom.GetAllUsersAsync()).Data?.ToDictionary(u => u.UserId, u => u) ?? new();
        var idx = _currentThreadMessages.FindIndex(m => m.MessageId == msg.MessageId);
        if (idx >= 0)
        {
            _currentThreadMessages[idx] = ToMessageItem(recalledDto, currentUser.UserId, users);
            ApplyMessageSearchFilter();
        }

        var last = _currentThreadMessages.LastOrDefault();
        if (last is not null && last.MessageId == msg.MessageId)
        {
            conv.Subtitle = conv.IsRoom ? "Bạn: Tin nhắn đã được thu hồi" : "Tin nhắn đã được thu hồi";
        }

        if (res.Data.PinnedNoticeCleared)
        {
            SetPinnedNotice(null);
        }

        if (res.Data.AttachmentRemoved)
        {
            var shared = await ClientServiceHost.Current.FileStorage.GetConversationFilesAsync(currentUser.UserId, conv.Id);
            RoomSharedFiles.Clear();
            foreach (var f in shared)
            {
                RoomSharedFiles.Add(ToFileItem(f, conv.Title));
            }
        }

        _notify("Đã thu hồi tin nhắn với mọi người.");
    }

    private void DeleteSingleMessageLocal(object? param)
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || SelectedConversation is null || !CanSendMessages || param is not MessageItemModel msg || msg.IsSystemNotice)
        {
            return;
        }

        if (ReplyingToMessage?.MessageId == msg.MessageId)
        {
            ReplyingToMessage = null;
        }

        ClientServiceHost.Current.DemoStore.HideMessageForUser(currentUser.UserId, msg.MessageId);
        _currentThreadMessages.RemoveAll(m => m.MessageId == msg.MessageId);
        ApplyMessageSearchFilter();

        var last = _currentThreadMessages.LastOrDefault();
        if (last is null)
        {
            SelectedConversation.Subtitle = "Chưa có tin nhắn hiển thị";
        }
        else if (last.IsAttachment)
        {
            SelectedConversation.Subtitle = $"[Tệp] {last.AttachmentFileName}";
        }
        else
        {
            var preview = MessageQuoteCodec.FormatSidebarPreview(last.Content);
            SelectedConversation.Subtitle = SelectedConversation.IsRoom
                ? $"{(last.IsOwnMessage ? "Bạn" : ShortFirstName(last.SenderName))}: {preview}"
                : preview;
        }

        _notify("Đã xóa tin nhắn ở phía bạn (người khác vẫn nhìn thấy).");
    }

    private async Task RemoveMemberFromRoomAsync(object? param)
    {
        if (!CanManageSelectedRoomMembers || SelectedConversation is not { IsRoom: true } || param is not RoomMemberItemModel member)
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
        DateTime? lastDate = null;
        foreach (var msg in _currentThreadMessages)
        {
            if (!string.IsNullOrEmpty(q)
                && !msg.BodyContent.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !msg.Content.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !msg.SenderName.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (lastDate is null || msg.LocalDate != lastDate.Value)
            {
                msg.ShowDateDivider = true;
                msg.DateDividerLabel = FormatDateDivider(msg.LocalDate);
                lastDate = msg.LocalDate;
            }
            else
            {
                msg.ShowDateDivider = false;
                msg.DateDividerLabel = string.Empty;
            }

            VisibleMessages.Add(msg);
        }
    }

    private void AppendVisibleMessageIfMatchesFilter(MessageItemModel msg)
    {
        var q = MessageSearchQuery.Trim();
        if (!string.IsNullOrEmpty(q)
            && !msg.BodyContent.Contains(q, StringComparison.OrdinalIgnoreCase)
            && !msg.Content.Contains(q, StringComparison.OrdinalIgnoreCase)
            && !msg.SenderName.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var prevDate = VisibleMessages.LastOrDefault()?.LocalDate;
        if (prevDate is null || msg.LocalDate != prevDate.Value)
        {
            msg.ShowDateDivider = true;
            msg.DateDividerLabel = FormatDateDivider(msg.LocalDate);
        }
        else
        {
            msg.ShowDateDivider = false;
            msg.DateDividerLabel = string.Empty;
        }

        VisibleMessages.Add(msg);
    }

    private MessageItemModel ToMessageItem(MessageDto dto, string currentUserId, Dictionary<string, UserDto> users)
    {
        var isOwn = dto.SenderId == currentUserId;
        var isRecalled = dto.Type != MessageType.SystemNotice
                         && (ClientServiceHost.Current.DemoStore.IsMessageRecalled(dto.MessageId)
                             || MessageQuoteCodec.IsRecalledRaw(dto.Content));

        users.TryGetValue(dto.SenderId, out var sender);
        var (bg, fg) = ClientServiceHost.Current.DemoStore.GetAvatarTone(dto.SenderId);
        var localTs = dto.ServerTimestampUtc.ToLocalTime();

        if (isRecalled)
        {
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
                CanSendFromClient = CanSendMessages,
                IsRecalled = true,
                IsAttachment = false,
                IsSystemNotice = false,
                CanPinThisMessage = false,
                LocalTimestamp = localTs,
                Content = MessageQuoteCodec.RecalledMarker,
                BodyContent = isOwn ? "Bạn đã thu hồi tin nhắn" : "Tin nhắn đã được thu hồi",
                HasReplyQuote = false,
                TimeText = localTs.ToString("HH:mm"),
                HasImagePreview = false,
                IsPinned = false
            };
        }

        var ext = Path.GetExtension(dto.AttachmentFileName ?? string.Empty).ToLowerInvariant();
        var (badge, bBg, bFg) = ext switch
        {
            ".pdf" => ("PDF", "#FEF2F2", "#DC2626"),
            ".dwg" or ".dxf" or ".rvt" or ".skp" => ("CAD", "#EFF6FF", "#2563EB"),
            ".zip" or ".rar" or ".7z" => ("ZIP", "#FFFBEB", "#D97706"),
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" => ("ẢNH", "#F5F3FF", "#7C3AED"),
            _ => ("TỆP", "#F1F5F9", "#475569")
        };

        var hasQuote = MessageQuoteCodec.TryDecode(dto.Content, out var replyAuthor, out var replySnippet, out var body);
        var resolvedPath = ResolveAttachmentLocalPath(dto.LocalFilePath, dto.AttachmentFileId);
        var isImg = ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp";
        var hasImgPreview = dto.Type == MessageType.FileAttachment
                            && isImg
                            && !string.IsNullOrWhiteSpace(resolvedPath)
                            && File.Exists(resolvedPath);

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
            CanSendFromClient = CanSendMessages,
            IsRecalled = false,
            IsAttachment = dto.Type == MessageType.FileAttachment,
            IsSystemNotice = dto.Type == MessageType.SystemNotice,
            CanPinThisMessage = CanPinThisRoom && dto.Type != MessageType.SystemNotice,
            LocalTimestamp = localTs,
            Content = dto.Content,
            BodyContent = hasQuote ? body : dto.Content,
            HasReplyQuote = hasQuote,
            ReplyAuthor = replyAuthor,
            ReplySnippet = replySnippet,
            TimeText = localTs.ToString("HH:mm"),
            AttachmentFileId = dto.AttachmentFileId,
            AttachmentFileName = dto.AttachmentFileName,
            AttachmentSizeText = dto.AttachmentFileSize.HasValue
                ? $"{DemoFileStorageService.FormatBytes(dto.AttachmentFileSize.Value)} • {badge}"
                : badge,
            AttachmentBadgeText = badge,
            AttachmentBadgeBgHex = bBg,
            AttachmentBadgeFgHex = bFg,
            LocalFilePath = resolvedPath,
            HasImagePreview = hasImgPreview,
            IsPinned = dto.IsPinned
        };
    }

    private static string? ResolveAttachmentLocalPath(string? messageLocalPath, string? fileId)
    {
        if (!string.IsNullOrWhiteSpace(messageLocalPath) && File.Exists(messageLocalPath))
        {
            return messageLocalPath;
        }

        if (!string.IsNullOrEmpty(fileId))
        {
            lock (ClientServiceHost.Current.DemoStore.SyncRoot)
            {
                var manifestPath = ClientServiceHost.Current.DemoStore.LocalFiles
                    .FirstOrDefault(f => f.FileId == fileId)?.LocalFullPath;
                if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
                {
                    return manifestPath;
                }
            }
        }

        return messageLocalPath;
    }

    private static string FormatDateDivider(DateTime localDate)
    {
        if (localDate.Date == DateTime.Today) return "Hôm nay";
        if (localDate.Date == DateTime.Today.AddDays(-1)) return "Hôm qua";
        return localDate.ToString("dd/MM/yyyy");
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
