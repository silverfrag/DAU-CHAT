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

public sealed class ClassOptionItem
{
    public string? ClassId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public ClassDto? RawClass { get; init; }
}

public enum BulkAddTab
{
    SelectFromList,
    PasteOrCsv
}

public sealed class MainShellViewModel : ObservableObject
{
    private readonly Action<string?> _onLoggedOut;
    private bool _isSessionActive;
    private bool _isExternalSyncRunning;

    private NavSection _activeSection = NavSection.Messages;
    private string _toastMessage = string.Empty;
    private bool _hasToast;
    private string _currentUserName = string.Empty;
    private string _currentUserInitials = "DA";
    private string _currentUserRoleLabel = "Sinh viên";
    private string _currentUsername = string.Empty;
    private UserRole _currentRole = UserRole.SinhVien;
    private PresenceStatus _currentPresence = PresenceStatus.Online;

    // 1. Create / Edit Room modal
    private bool _isCreateRoomOpen;
    private bool _isEditRoomMode;
    private RoomDto? _editingRoom;
    private string _newRoomName = string.Empty;
    private string _newRoomDescription = string.Empty;
    private RoomType _selectedRoomType = RoomType.ClassRoom;
    private ClassOptionItem? _selectedRoomClassOption;

    // 2. Profile / Password / Presence modal
    private bool _isProfileOpen;
    private string _profileFullName = string.Empty;
    private string _profileEmail = string.Empty;
    private string _oldPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;

    // 3. Create / Edit Account modal
    private bool _isCreateAccountOpen;
    private bool _isEditAccountMode;
    private string _editingAccountId = string.Empty;
    private string _newAccUsername = string.Empty;
    private string _newAccFullName = string.Empty;
    private string _newAccEmail = string.Empty;
    private string _newAccInitialPassword = "123456";
    private UserRole _newAccRole = UserRole.SinhVien;
    private ClassOptionItem? _newAccClassOption;

    // 4. Lock Account modal
    private bool _isLockModalOpen;
    private AccountItemModel? _lockTargetAccount;
    private string _lockReasonText = "Vi phạm quy định phòng chat nội bộ";

    // 5. Create / Edit Class modal
    private bool _isCreateClassOpen;
    private bool _isEditClassMode;
    private string _editingClassId = string.Empty;
    private string _newClassCode = string.Empty;
    private string _newClassName = string.Empty;
    private string _newClassDepartment = "Khoa Công nghệ Thông tin (DAU)";
    private string _newClassAcademicYear = "2023 - 2027";
    private UserDto? _selectedClassLecturer;
    private bool _autoCreateClassRoom = true;
    private bool _allowClassJoinByCode = true;

    // 6. Upgraded 2-Tab Bulk Add Members modal (Class or Room)
    private bool _isAddMemberOpen;
    private bool _bulkTargetIsClass;
    private string _bulkTargetId = string.Empty;
    private string _bulkTargetTitle = string.Empty;
    private BulkAddTab _activeBulkAddTab = BulkAddTab.SelectFromList;
    private string _candidateSearchQuery = string.Empty;
    private ClassOptionItem? _candidateClassFilterOption;
    private string _batchMssvText = string.Empty;
    private List<SelectableAccountItem> _allCandidates = new();
    private HashSet<string> _existingMemberIds = new();
    private string _matchedPreviewText = string.Empty;
    private int _matchedMssvCount;
    private string _alreadyMembersNote = string.Empty;
    private string _unmatchedTokensNote = string.Empty;
    private List<string> _parsedMatchedUserIds = new();

    // 7. Admin Members Manager modal (Class or Room)
    private bool _isMembersManagerOpen;
    private bool _membersManagerIsClass;
    private string _membersManagerTargetId = string.Empty;
    private string _membersManagerTitle = string.Empty;
    private string? _membersManagerProtectedOwnerId;

    // 8. Student Join By Class Code modal
    private bool _isJoinByCodeOpen;
    private string _joinCodeInput = string.Empty;

    // 9. Pin Notice modal
    private bool _isPinModalOpen;
    private string _pinRoomId = string.Empty;
    private string _pinContentText = string.Empty;

    public MainShellViewModel(Action<string?> onLoggedOut)
    {
        _onLoggedOut = onLoggedOut;

        Chat = new ChatWorkspaceViewModel(
            ShowToast,
            OpenCreateRoomModal,
            OpenEditRoomModal,
            roomId => OpenBulkAddModal(false, roomId, ResolveRoomTitle(roomId)),
            OpenPinNoticeModal,
            OpenJoinByCodeModal);

        Classes = new LecturerClassesViewModel(
            ShowToast,
            OpenCreateClassModal,
            OpenEditClassModal,
            (classId, displayName) => OpenBulkAddModal(true, classId, displayName),
            OpenRoomInChatAsync);

        Files = new FilesLibraryViewModel(ShowToast);
        Storage = new StorageBackupViewModel(ShowToast);

        Admin = new AdminCenterViewModel(
            ShowToast,
            OpenCreateAccountModal,
            OpenEditAccountModal,
            OpenLockAccountModal,
            OpenCreateClassModal,
            OpenEditClassModal,
            (classId, displayName) => OpenMembersManagerModal(true, classId, displayName),
            OpenCreateRoomModal,
            OpenEditRoomModal,
            (roomId, roomName) => OpenMembersManagerModal(false, roomId, roomName),
            OpenRoomInChatAsync);

        var host = ClientServiceHost.Current;
        host.DemoAuthRoom.ForceLoggedOut += OnForceLoggedOut;
        host.DemoStore.ExternalSyncApplied += diff => { _ = OnExternalSyncAppliedAsync(); };

        NavigateCommand = new AsyncRelayCommand(async param =>
        {
            if (param is string s && Enum.TryParse<NavSection>(s, out var section))
            {
                await NavigateToAsync(section);
            }
        });
        NavigateHomeCommand = new AsyncRelayCommand(() => NavigateToAsync(IsAdmin ? NavSection.Admin : NavSection.Messages));

        OpenProfileCommand = new RelayCommand(OpenProfileModal);
        CloseModalsCommand = new RelayCommand(CloseAllModals);

        SubmitCreateRoomCommand = new AsyncRelayCommand(SubmitCreateRoomAsync);
        SaveProfileCommand = new AsyncRelayCommand(SaveProfileAsync);
        ChangePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync);
        SetPresenceCommand = new RelayCommand(param =>
        {
            if (param is string p && Enum.TryParse<PresenceStatus>(p, out var status)
                && ClientServiceHost.Current.AuthRoom.CurrentUser is { } u)
            {
                ClientServiceHost.Current.DemoAuthRoom.UpdateUserPresence(u.UserId, status);
                CurrentPresence = status;
                ShowToast(status == PresenceStatus.Online ? "Trạng thái: Đang hoạt động" : "Trạng thái: Đang bận");
            }
        });
        LogoutCommand = new AsyncRelayCommand(ExecuteLogoutAsync);

        SubmitCreateAccountCommand = new AsyncRelayCommand(SubmitCreateAccountAsync);
        SubmitLockAccountCommand = new AsyncRelayCommand(SubmitLockAccountAsync);
        SubmitCreateClassCommand = new AsyncRelayCommand(SubmitCreateClassAsync);

        SwitchBulkAddTabCommand = new RelayCommand(param =>
        {
            if (param is string t && Enum.TryParse<BulkAddTab>(t, out var tab))
            {
                ActiveBulkAddTab = tab;
            }
        });
        SelectAllFilteredCandidatesCommand = new RelayCommand(() =>
        {
            foreach (var c in FilteredCandidates) c.IsSelected = true;
            OnPropertyChanged(nameof(SelectedCandidatesCount));
        });
        DeselectAllCandidatesCommand = new RelayCommand(() =>
        {
            foreach (var c in _allCandidates) c.IsSelected = false;
            OnPropertyChanged(nameof(SelectedCandidatesCount));
        });
        SubmitSelectedCandidatesCommand = new AsyncRelayCommand(SubmitSelectedCandidatesAsync);
        ImportMssvFileCommand = new RelayCommand(ImportMssvFromFile);
        FillAvailableStudentsMssvCommand = new RelayCommand(FillAvailableStudentsMssv);
        SubmitBatchMssvCommand = new AsyncRelayCommand(SubmitBatchMssvAsync);

        OpenBulkAddFromManagerCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(_membersManagerTargetId))
            {
                OpenBulkAddModal(_membersManagerIsClass, _membersManagerTargetId, _membersManagerTitle);
            }
        });
        RemoveManagedMemberCommand = new AsyncRelayCommand(RemoveManagedMemberAsync);

        PickJoinCodeHintCommand = new RelayCommand(param =>
        {
            if (param is ClassItemModel cls)
            {
                JoinCodeInput = cls.JoinCode;
            }
        });
        SubmitJoinByCodeCommand = new AsyncRelayCommand(SubmitJoinByCodeAsync);
        SubmitPinNoticeCommand = new AsyncRelayCommand(SubmitPinNoticeAsync);
    }

    public ChatWorkspaceViewModel Chat { get; }
    public LecturerClassesViewModel Classes { get; }
    public FilesLibraryViewModel Files { get; }
    public StorageBackupViewModel Storage { get; }
    public AdminCenterViewModel Admin { get; }

    public ObservableCollection<ClassOptionItem> RoomClassOptions { get; } = new();
    public ObservableCollection<ClassOptionItem> AccountClassOptions { get; } = new();
    public ObservableCollection<ClassOptionItem> CandidateClassFilterOptions { get; } = new();
    public ObservableCollection<UserDto> AvailableLecturers { get; } = new();
    public ObservableCollection<RoomType> AvailableRoomTypes { get; } = new()
    {
        RoomType.ClassRoom,
        RoomType.StudyGroup,
        RoomType.General
    };
    public ObservableCollection<UserRole> AssignableRoles { get; } = new()
    {
        UserRole.SinhVien,
        UserRole.GiangVien,
        UserRole.Admin
    };

    public ObservableCollection<SelectableAccountItem> FilteredCandidates { get; } = new();
    public ObservableCollection<RoomMemberItemModel> ManagedMembers { get; } = new();
    public ObservableCollection<ClassItemModel> AvailableJoinCodeHints { get; } = new();

    public ICommand NavigateCommand { get; }
    public ICommand NavigateHomeCommand { get; }
    public ICommand OpenProfileCommand { get; }
    public ICommand CloseModalsCommand { get; }
    public ICommand SubmitCreateRoomCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand SetPresenceCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand SubmitCreateAccountCommand { get; }
    public ICommand SubmitLockAccountCommand { get; }
    public ICommand SubmitCreateClassCommand { get; }
    public ICommand SwitchBulkAddTabCommand { get; }
    public ICommand SelectAllFilteredCandidatesCommand { get; }
    public ICommand DeselectAllCandidatesCommand { get; }
    public ICommand SubmitSelectedCandidatesCommand { get; }
    public ICommand ImportMssvFileCommand { get; }
    public ICommand FillAvailableStudentsMssvCommand { get; }
    public ICommand SubmitBatchMssvCommand { get; }
    public ICommand OpenBulkAddFromManagerCommand { get; }
    public ICommand RemoveManagedMemberCommand { get; }
    public ICommand PickJoinCodeHintCommand { get; }
    public ICommand SubmitJoinByCodeCommand { get; }
    public ICommand SubmitPinNoticeCommand { get; }

    public NavSection ActiveSection
    {
        get => _activeSection;
        set
        {
            if (SetProperty(ref _activeSection, value))
            {
                OnPropertyChanged(nameof(IsChatViewVisible));
            }
        }
    }

    public bool IsChatViewVisible => ActiveSection is NavSection.Messages or NavSection.Rooms;
    public bool IsAdmin => _currentRole == UserRole.Admin;
    public bool IsLecturer => _currentRole == UserRole.GiangVien;
    public bool IsStudent => _currentRole == UserRole.SinhVien;
    public bool IsNotAdmin => !IsAdmin;
    public string RoomsNavLabel => IsAdmin ? "Phòng chat" : "Phòng học";

    public string CurrentUserName { get => _currentUserName; set => SetProperty(ref _currentUserName, value); }
    public string CurrentUserInitials { get => _currentUserInitials; set => SetProperty(ref _currentUserInitials, value); }
    public string CurrentUserRoleLabel { get => _currentUserRoleLabel; set => SetProperty(ref _currentUserRoleLabel, value); }
    public string CurrentUsername { get => _currentUsername; set => SetProperty(ref _currentUsername, value); }
    public PresenceStatus CurrentPresence { get => _currentPresence; set => SetProperty(ref _currentPresence, value); }

    public string ToastMessage { get => _toastMessage; set => SetProperty(ref _toastMessage, value); }
    public bool HasToast { get => _hasToast; set => SetProperty(ref _hasToast, value); }

    // Room Modal Bindings
    public bool IsCreateRoomOpen { get => _isCreateRoomOpen; set => SetProperty(ref _isCreateRoomOpen, value); }
    public bool IsEditRoomMode
    {
        get => _isEditRoomMode;
        set
        {
            if (SetProperty(ref _isEditRoomMode, value))
            {
                OnPropertyChanged(nameof(RoomModalBadge));
                OnPropertyChanged(nameof(RoomModalTitle));
                OnPropertyChanged(nameof(RoomModalSubmitLabel));
                OnPropertyChanged(nameof(IsRoomClassPickerEnabled));
            }
        }
    }
    public string RoomModalBadge => IsEditRoomMode ? "CẬP NHẬT PHÒNG" : "PHÒNG MỚI";
    public string RoomModalTitle => IsEditRoomMode ? "Chỉnh sửa phòng trò chuyện" : "Tạo phòng trò chuyện";
    public string RoomModalSubmitLabel => IsEditRoomMode ? "Lưu thay đổi" : "Tạo phòng";
    public bool IsRoomClassPickerEnabled => !IsEditRoomMode;
    public string NewRoomName { get => _newRoomName; set => SetProperty(ref _newRoomName, value); }
    public string NewRoomDescription { get => _newRoomDescription; set => SetProperty(ref _newRoomDescription, value); }
    public RoomType SelectedRoomType { get => _selectedRoomType; set => SetProperty(ref _selectedRoomType, value); }
    public ClassOptionItem? SelectedRoomClassOption { get => _selectedRoomClassOption; set => SetProperty(ref _selectedRoomClassOption, value); }

    // Profile Modal Bindings
    public bool IsProfileOpen { get => _isProfileOpen; set => SetProperty(ref _isProfileOpen, value); }
    public string ProfileFullName { get => _profileFullName; set => SetProperty(ref _profileFullName, value); }
    public string ProfileEmail { get => _profileEmail; set => SetProperty(ref _profileEmail, value); }
    public string OldPassword { get => _oldPassword; set => SetProperty(ref _oldPassword, value); }
    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
    public string ConfirmPassword { get => _confirmPassword; set => SetProperty(ref _confirmPassword, value); }

    // Account Modal Bindings
    public bool IsCreateAccountOpen { get => _isCreateAccountOpen; set => SetProperty(ref _isCreateAccountOpen, value); }
    public bool IsEditAccountMode
    {
        get => _isEditAccountMode;
        set
        {
            if (SetProperty(ref _isEditAccountMode, value))
            {
                OnPropertyChanged(nameof(AccountModalBadge));
                OnPropertyChanged(nameof(AccountModalTitle));
                OnPropertyChanged(nameof(AccountModalSubmitLabel));
                OnPropertyChanged(nameof(IsUsernameEditable));
                OnPropertyChanged(nameof(IsInitialPasswordVisible));
            }
        }
    }
    public string AccountModalBadge => IsEditAccountMode ? "CẬP NHẬT TÀI KHOẢN" : "CẤP PHÁT NỘI BỘ";
    public string AccountModalTitle => IsEditAccountMode ? "Chỉnh sửa thông tin tài khoản" : "Tạo tài khoản mới";
    public string AccountModalSubmitLabel => IsEditAccountMode ? "Lưu thay đổi" : "Tạo tài khoản";
    public bool IsUsernameEditable => !IsEditAccountMode;
    public bool IsInitialPasswordVisible => !IsEditAccountMode;
    public string NewAccUsername { get => _newAccUsername; set => SetProperty(ref _newAccUsername, value); }
    public string NewAccFullName { get => _newAccFullName; set => SetProperty(ref _newAccFullName, value); }
    public string NewAccEmail { get => _newAccEmail; set => SetProperty(ref _newAccEmail, value); }
    public string NewAccInitialPassword { get => _newAccInitialPassword; set => SetProperty(ref _newAccInitialPassword, value); }
    public UserRole NewAccRole { get => _newAccRole; set => SetProperty(ref _newAccRole, value); }
    public ClassOptionItem? NewAccClassOption { get => _newAccClassOption; set => SetProperty(ref _newAccClassOption, value); }

    // Lock Modal Bindings
    public bool IsLockModalOpen { get => _isLockModalOpen; set => SetProperty(ref _isLockModalOpen, value); }
    public AccountItemModel? LockTargetAccount { get => _lockTargetAccount; set => SetProperty(ref _lockTargetAccount, value); }
    public string LockReasonText { get => _lockReasonText; set => SetProperty(ref _lockReasonText, value); }

    // Class Modal Bindings
    public bool IsCreateClassOpen { get => _isCreateClassOpen; set => SetProperty(ref _isCreateClassOpen, value); }
    public bool IsEditClassMode
    {
        get => _isEditClassMode;
        set
        {
            if (SetProperty(ref _isEditClassMode, value))
            {
                OnPropertyChanged(nameof(ClassModalBadge));
                OnPropertyChanged(nameof(ClassModalTitle));
                OnPropertyChanged(nameof(ClassModalSubmitLabel));
                OnPropertyChanged(nameof(IsAutoCreateRoomOptionVisible));
            }
        }
    }
    public string ClassModalBadge => IsEditClassMode ? "CẬP NHẬT LỚP HỌC" : "LỚP HỌC MỚI";
    public string ClassModalTitle => IsEditClassMode ? "Chỉnh sửa lớp học phần" : "Tạo lớp học phần";
    public string ClassModalSubmitLabel => IsEditClassMode ? "Lưu thay đổi" : "Tạo lớp học";
    public bool IsLecturerPickerVisible => IsAdmin;
    public bool IsAutoCreateRoomOptionVisible => !IsEditClassMode;
    public string NewClassCode { get => _newClassCode; set => SetProperty(ref _newClassCode, value); }
    public string NewClassName { get => _newClassName; set => SetProperty(ref _newClassName, value); }
    public string NewClassDepartment { get => _newClassDepartment; set => SetProperty(ref _newClassDepartment, value); }
    public string NewClassAcademicYear { get => _newClassAcademicYear; set => SetProperty(ref _newClassAcademicYear, value); }
    public UserDto? SelectedClassLecturer { get => _selectedClassLecturer; set => SetProperty(ref _selectedClassLecturer, value); }
    public bool AutoCreateClassRoom { get => _autoCreateClassRoom; set => SetProperty(ref _autoCreateClassRoom, value); }
    public bool AllowClassJoinByCode { get => _allowClassJoinByCode; set => SetProperty(ref _allowClassJoinByCode, value); }

    // Bulk Add Members Modal Bindings
    public bool IsAddMemberOpen { get => _isAddMemberOpen; set => SetProperty(ref _isAddMemberOpen, value); }
    public string BulkModalBadge => _bulkTargetIsClass ? "LỚP HỌC PHẦN • THÊM SINH VIÊN" : "PHÒNG CHAT • THÊM THÀNH VIÊN";
    public string BulkModalTitle => string.IsNullOrWhiteSpace(_bulkTargetTitle)
        ? "Thêm thành viên"
        : $"Thêm thành viên • {_bulkTargetTitle}";
    public BulkAddTab ActiveBulkAddTab { get => _activeBulkAddTab; set => SetProperty(ref _activeBulkAddTab, value); }
    public string CandidateSearchQuery
    {
        get => _candidateSearchQuery;
        set
        {
            if (SetProperty(ref _candidateSearchQuery, value))
            {
                ApplyCandidateFilter();
            }
        }
    }
    public ClassOptionItem? CandidateClassFilterOption
    {
        get => _candidateClassFilterOption;
        set
        {
            if (SetProperty(ref _candidateClassFilterOption, value))
            {
                ApplyCandidateFilter();
            }
        }
    }
    public int SelectedCandidatesCount => _allCandidates.Count(c => c.IsSelected);
    public string BatchMssvText
    {
        get => _batchMssvText;
        set
        {
            if (SetProperty(ref _batchMssvText, value))
            {
                ParseBatchMssvInput();
            }
        }
    }
    public int MatchedMssvCount { get => _matchedMssvCount; set => SetProperty(ref _matchedMssvCount, value); }
    public string MatchedPreviewText { get => _matchedPreviewText; set => SetProperty(ref _matchedPreviewText, value); }
    public string AlreadyMembersNote { get => _alreadyMembersNote; set => SetProperty(ref _alreadyMembersNote, value); }
    public string UnmatchedTokensNote { get => _unmatchedTokensNote; set => SetProperty(ref _unmatchedTokensNote, value); }

    // Members Manager Modal Bindings
    public bool IsMembersManagerOpen { get => _isMembersManagerOpen; set => SetProperty(ref _isMembersManagerOpen, value); }
    public string MembersManagerBadge => _membersManagerIsClass ? "DANH SÁCH LỚP HỌC" : "DANH SÁCH PHÒNG CHAT";
    public string MembersManagerTitle { get => _membersManagerTitle; set => SetProperty(ref _membersManagerTitle, value); }

    // Student Join By Code Modal Bindings
    public bool IsJoinByCodeOpen { get => _isJoinByCodeOpen; set => SetProperty(ref _isJoinByCodeOpen, value); }
    public string JoinCodeInput { get => _joinCodeInput; set => SetProperty(ref _joinCodeInput, value); }

    // Pin Modal Bindings
    public bool IsPinModalOpen { get => _isPinModalOpen; set => SetProperty(ref _isPinModalOpen, value); }
    public string PinContentText { get => _pinContentText; set => SetProperty(ref _pinContentText, value); }

    public event Action? PasswordFieldsResetRequested;

    public async Task InitializeForUserAsync(UserDto user)
    {
        _isSessionActive = true;
        HasToast = false;
        ToastMessage = string.Empty;
        SyncCurrentUserInfo(user);
        CloseAllModals();

        var landing = user.Role == UserRole.Admin ? NavSection.Admin : NavSection.Messages;
        ActiveSection = landing;
        Chat.RoomsOnly = user.Role == UserRole.Admin;

        await RefreshLookupsAsync();
        await Chat.LoadAsync();

        if (user.Role == UserRole.Admin)
        {
            await Admin.RefreshAsync();
        }
        else if (user.Role == UserRole.GiangVien)
        {
            await Classes.RefreshAsync();
        }
    }

    public async Task NavigateToAsync(NavSection section)
    {
        if (IsAdmin && section is NavSection.Messages or NavSection.Classes or NavSection.Files or NavSection.Storage)
        {
            ShowToast("Phân quyền RBAC: Tài khoản Admin chỉ thao tác trên Quản trị hệ thống và Phòng chat.");
            return;
        }

        if (!IsAdmin && section == NavSection.Admin)
        {
            ShowToast("Phân quyền RBAC: Chức năng Quản trị chỉ dành cho tài khoản Admin.");
            return;
        }

        if (!IsLecturer && section == NavSection.Classes)
        {
            ShowToast("Phân quyền RBAC: Không gian Lớp học phần dành cho Giảng viên.");
            return;
        }

        ActiveSection = section;
        switch (section)
        {
            case NavSection.Messages:
                Chat.RoomsOnly = false;
                await Chat.LoadAsync(Chat.SelectedConversation?.IsRoom == false ? Chat.SelectedConversation.Id : null);
                break;
            case NavSection.Rooms:
                Chat.RoomsOnly = true;
                await Chat.LoadAsync(Chat.SelectedConversation?.IsRoom == true ? Chat.SelectedConversation.Id : null);
                break;
            case NavSection.Classes:
                await Classes.RefreshAsync(Classes.SelectedClass?.ClassId);
                break;
            case NavSection.Files:
                await Files.RefreshAsync();
                break;
            case NavSection.Storage:
                await Storage.RefreshAsync();
                break;
            case NavSection.Admin:
                await Admin.RefreshAsync();
                break;
        }
    }

    public async Task OpenRoomInChatAsync(string roomId)
    {
        ActiveSection = NavSection.Rooms;
        Chat.RoomsOnly = true;
        await Chat.LoadAsync(roomId);
    }

    public void ShowToast(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ToastMessage = message;
            HasToast = true;
        });

        _ = Task.Delay(3400).ContinueWith(_ =>
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (ToastMessage == message)
                {
                    HasToast = false;
                }
            }));
    }

    private void SyncCurrentUserInfo(UserDto user)
    {
        _currentRole = user.Role;
        CurrentUserName = user.FullName;
        CurrentUsername = user.Username;
        CurrentUserInitials = DemoDataStore.GetInitials(user.FullName);
        CurrentPresence = user.Presence;
        CurrentUserRoleLabel = user.Role switch
        {
            UserRole.Admin => "Quản trị viên",
            UserRole.GiangVien => "Giảng viên",
            _ => "Sinh viên"
        };

        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsLecturer));
        OnPropertyChanged(nameof(IsStudent));
        OnPropertyChanged(nameof(IsNotAdmin));
        OnPropertyChanged(nameof(RoomsNavLabel));
        OnPropertyChanged(nameof(IsLecturerPickerVisible));
        Chat.RefreshRoleBindings();
    }

    private async Task RefreshLookupsAsync()
    {
        var host = ClientServiceHost.Current;
        var clsRes = await host.AuthRoom.GetClassesAsync();
        RoomClassOptions.Clear();
        AccountClassOptions.Clear();
        CandidateClassFilterOptions.Clear();

        RoomClassOptions.Add(new ClassOptionItem { ClassId = null, DisplayName = "Không liên kết lớp (Nhóm tự do)" });
        AccountClassOptions.Add(new ClassOptionItem { ClassId = null, DisplayName = "Chưa gán lớp cố định" });
        CandidateClassFilterOptions.Add(new ClassOptionItem { ClassId = null, DisplayName = "Tất cả lớp trong trường" });

        if (clsRes.Success && clsRes.Data is not null)
        {
            foreach (var c in clsRes.Data)
            {
                var label = $"{c.ClassCode} — {c.ClassName}";
                RoomClassOptions.Add(new ClassOptionItem { ClassId = c.ClassId, DisplayName = label, RawClass = c });
                AccountClassOptions.Add(new ClassOptionItem { ClassId = c.ClassId, DisplayName = label, RawClass = c });
                CandidateClassFilterOptions.Add(new ClassOptionItem { ClassId = c.ClassId, DisplayName = c.ClassCode, RawClass = c });
            }
        }

        SelectedRoomClassOption ??= RoomClassOptions.FirstOrDefault();
        NewAccClassOption ??= AccountClassOptions.FirstOrDefault();
        CandidateClassFilterOption ??= CandidateClassFilterOptions.FirstOrDefault();

        var usersRes = await host.AuthRoom.GetAllUsersAsync();
        AvailableLecturers.Clear();
        if (usersRes.Success && usersRes.Data is not null)
        {
            foreach (var gv in usersRes.Data.Where(u => u.Role == UserRole.GiangVien && u.Status == AccountStatus.Active))
            {
                AvailableLecturers.Add(gv);
            }
        }
    }

    private async Task OnExternalSyncAppliedAsync()
    {
        if (!_isSessionActive || _isExternalSyncRunning) return;
        _isExternalSyncRunning = true;
        try
        {
            if (Application.Current?.Dispatcher is not { } dispatcher) return;
            await dispatcher.InvokeAsync(async () =>
            {
                var cu = ClientServiceHost.Current.AuthRoom.CurrentUser;
                if (!_isSessionActive || cu is null) return;

                SyncCurrentUserInfo(cu);
                await RefreshLookupsAsync();

                if (IsAdmin)
                {
                    await Admin.RefreshAsync();
                }
                else if (IsLecturer)
                {
                    await Classes.RefreshAsync(Classes.SelectedClass?.ClassId);
                }

                if (ActiveSection == NavSection.Files)
                {
                    await Files.RefreshAsync();
                }
                else if (ActiveSection == NavSection.Storage)
                {
                    await Storage.RefreshAsync();
                }

                if (IsMembersManagerOpen && !string.IsNullOrEmpty(_membersManagerTargetId))
                {
                    await ReloadMembersManagerListAsync();
                }
            }).Task.Unwrap();
        }
        finally
        {
            _isExternalSyncRunning = false;
        }
    }

    private void OnForceLoggedOut(object? sender, string reason)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (!_isSessionActive) return;
            _isSessionActive = false;
            CloseAllModals();
            _onLoggedOut(reason);
        });
    }

    private void CloseAllModals()
    {
        IsCreateRoomOpen = false;
        IsProfileOpen = false;
        IsCreateAccountOpen = false;
        IsLockModalOpen = false;
        IsCreateClassOpen = false;
        IsAddMemberOpen = false;
        IsMembersManagerOpen = false;
        IsJoinByCodeOpen = false;
        IsPinModalOpen = false;
    }

    // =====================================================
    // 1. CREATE / EDIT ROOM MODAL
    // =====================================================
    private async void OpenCreateRoomModal()
    {
        CloseAllModals();
        await RefreshLookupsAsync();
        IsEditRoomMode = false;
        _editingRoom = null;
        NewRoomName = string.Empty;
        NewRoomDescription = string.Empty;
        SelectedRoomType = RoomType.ClassRoom;
        SelectedRoomClassOption = RoomClassOptions.FirstOrDefault();
        IsCreateRoomOpen = true;
    }

    private async void OpenEditRoomModal(RoomDto room)
    {
        CloseAllModals();
        await RefreshLookupsAsync();
        IsEditRoomMode = true;
        _editingRoom = room;
        NewRoomName = room.RoomName;
        NewRoomDescription = room.Description ?? string.Empty;
        SelectedRoomType = room.RoomType;
        SelectedRoomClassOption = RoomClassOptions.FirstOrDefault(o => o.ClassId == room.ClassId) ?? RoomClassOptions.FirstOrDefault();
        IsCreateRoomOpen = true;
    }

    private async Task SubmitCreateRoomAsync()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (user is null) return;
        if (string.IsNullOrWhiteSpace(NewRoomName))
        {
            ShowToast("Vui lòng nhập tên phòng trò chuyện.");
            return;
        }

        if (IsEditRoomMode && _editingRoom is not null)
        {
            var upd = await ClientServiceHost.Current.AuthRoom.UpdateRoomAsync(new RoomDto
            {
                RoomId = _editingRoom.RoomId,
                RoomName = NewRoomName.Trim(),
                Description = NewRoomDescription.Trim(),
                RoomType = SelectedRoomType
            });
            ShowToast(upd.Message);
            if (upd.Success)
            {
                IsCreateRoomOpen = false;
                await Chat.LoadAsync(_editingRoom.RoomId);
                if (IsAdmin) await Admin.RefreshAsync();
            }
            return;
        }

        var selectedCls = SelectedRoomClassOption?.RawClass;
        var res = await ClientServiceHost.Current.AuthRoom.CreateRoomAsync(new RoomDto
        {
            RoomName = NewRoomName.Trim(),
            Description = NewRoomDescription.Trim(),
            RoomType = selectedCls is not null ? RoomType.ClassRoom : SelectedRoomType,
            ClassId = selectedCls?.ClassId,
            OwnerId = selectedCls?.LecturerId ?? (user.Role == UserRole.GiangVien ? user.UserId : "u_gv_demo01")
        }, selectedCls?.MemberIds);

        ShowToast(res.Message);
        if (res.Success && res.Data is not null)
        {
            IsCreateRoomOpen = false;
            await Chat.LoadAsync(res.Data.RoomId);
            if (IsAdmin) await Admin.RefreshAsync();
            else if (IsLecturer) await Classes.RefreshAsync(Classes.SelectedClass?.ClassId);
        }
    }

    // =====================================================
    // 2. PROFILE / PASSWORD / LOGOUT MODAL
    // =====================================================
    private void OpenProfileModal()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (user is null) return;

        CloseAllModals();
        ProfileFullName = user.FullName;
        ProfileEmail = user.Email;
        OldPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        PasswordFieldsResetRequested?.Invoke();
        IsProfileOpen = true;
    }

    private async Task SaveProfileAsync()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (user is null) return;

        var res = await ClientServiceHost.Current.AuthRoom.UpdateUserProfileAsync(user.UserId, ProfileFullName, ProfileEmail);
        if (res.Success && res.Data is not null)
        {
            SyncCurrentUserInfo(res.Data);
            if (IsAdmin) await Admin.RefreshAsync();
        }
        ShowToast(res.Message);
    }

    private async Task ChangePasswordAsync()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (user is null) return;

        if (NewPassword != ConfirmPassword)
        {
            ShowToast("Mật khẩu xác nhận không khớp.");
            return;
        }

        var res = await ClientServiceHost.Current.AuthRoom.ChangePasswordAsync(user.UserId, OldPassword, NewPassword);
        ShowToast(res.Message);
        if (res.Success)
        {
            OldPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            PasswordFieldsResetRequested?.Invoke();
        }
    }

    private async Task ExecuteLogoutAsync()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        _isSessionActive = false;
        HasToast = false;
        ToastMessage = string.Empty;
        CloseAllModals();
        if (user is not null)
        {
            await ClientServiceHost.Current.AuthRoom.LogoutAsync(user.UserId);
        }
        _onLoggedOut(null);
    }

    // =====================================================
    // 3. CREATE / EDIT USER ACCOUNT MODAL (ADMIN)
    // =====================================================
    private async void OpenCreateAccountModal()
    {
        CloseAllModals();
        await RefreshLookupsAsync();
        IsEditAccountMode = false;
        _editingAccountId = string.Empty;
        NewAccUsername = string.Empty;
        NewAccFullName = string.Empty;
        NewAccEmail = string.Empty;
        NewAccInitialPassword = "123456";
        NewAccRole = UserRole.SinhVien;
        NewAccClassOption = AccountClassOptions.FirstOrDefault(o => o.ClassId is not null) ?? AccountClassOptions.FirstOrDefault();
        IsCreateAccountOpen = true;
    }

    private async void OpenEditAccountModal(AccountItemModel account)
    {
        CloseAllModals();
        await RefreshLookupsAsync();
        IsEditAccountMode = true;
        _editingAccountId = account.UserId;
        NewAccUsername = account.Username;
        NewAccFullName = account.FullName;
        NewAccEmail = account.Email;
        NewAccRole = account.Role;
        NewAccClassOption = AccountClassOptions.FirstOrDefault(o => o.ClassId == account.RawUser.ClassId)
                            ?? AccountClassOptions.FirstOrDefault();
        IsCreateAccountOpen = true;
    }

    private async Task SubmitCreateAccountAsync()
    {
        if (IsEditAccountMode)
        {
            var upd = await ClientServiceHost.Current.AuthRoom.UpdateUserAsync(new UserDto
            {
                UserId = _editingAccountId,
                Username = NewAccUsername,
                FullName = NewAccFullName,
                Email = NewAccEmail,
                Role = NewAccRole,
                ClassId = NewAccClassOption?.ClassId,
                ClassName = NewAccClassOption?.RawClass is { } cls ? $"{cls.ClassCode} • {cls.ClassName}" : null
            });

            ShowToast(upd.Message);
            if (upd.Success)
            {
                IsCreateAccountOpen = false;
                await RefreshLookupsAsync();
                await Admin.RefreshAsync();
            }
            return;
        }

        var res = await ClientServiceHost.Current.AuthRoom.CreateUserAsync(new CreateUserRequestDto
        {
            Username = NewAccUsername,
            FullName = NewAccFullName,
            Email = NewAccEmail,
            InitialPassword = NewAccInitialPassword,
            Role = NewAccRole,
            ClassId = NewAccClassOption?.ClassId
        });

        ShowToast(res.Message);
        if (res.Success)
        {
            IsCreateAccountOpen = false;
            await RefreshLookupsAsync();
            await Admin.RefreshAsync();
            await Chat.LoadAsync();
        }
    }

    // =====================================================
    // 4. LOCK ACCOUNT MODAL
    // =====================================================
    private void OpenLockAccountModal(AccountItemModel account)
    {
        CloseAllModals();
        LockTargetAccount = account;
        LockReasonText = "Tài khoản tạm khóa theo yêu cầu Quản trị viên";
        IsLockModalOpen = true;
    }

    private async Task SubmitLockAccountAsync()
    {
        if (LockTargetAccount is null) return;
        var res = await ClientServiceHost.Current.AuthRoom.LockUserAsync(LockTargetAccount.UserId, LockReasonText);
        IsLockModalOpen = false;
        ShowToast(res.Message);
        await Admin.RefreshAsync();
    }

    // =====================================================
    // 5. CREATE / EDIT CLASS MODAL (ADMIN & LECTURER)
    // =====================================================
    private async void OpenCreateClassModal()
    {
        CloseAllModals();
        await RefreshLookupsAsync();
        IsEditClassMode = false;
        _editingClassId = string.Empty;
        NewClassCode = string.Empty;
        NewClassName = string.Empty;
        NewClassDepartment = "Khoa Công nghệ Thông tin (DAU)";
        NewClassAcademicYear = "2023 - 2027";
        AutoCreateClassRoom = true;
        AllowClassJoinByCode = true;

        var cu = ClientServiceHost.Current.AuthRoom.CurrentUser;
        SelectedClassLecturer = cu?.Role == UserRole.GiangVien
            ? AvailableLecturers.FirstOrDefault(u => u.UserId == cu.UserId) ?? AvailableLecturers.FirstOrDefault()
            : AvailableLecturers.FirstOrDefault();

        IsCreateClassOpen = true;
    }

    private async void OpenEditClassModal(ClassItemModel cls)
    {
        CloseAllModals();
        await RefreshLookupsAsync();
        IsEditClassMode = true;
        _editingClassId = cls.ClassId;
        NewClassCode = cls.ClassCode;
        NewClassName = cls.ClassName;
        NewClassDepartment = cls.Department;
        NewClassAcademicYear = string.IsNullOrWhiteSpace(cls.AcademicYear) ? "2023 - 2027" : cls.AcademicYear;
        AllowClassJoinByCode = cls.AllowStudentJoinByCode;
        SelectedClassLecturer = AvailableLecturers.FirstOrDefault(u => u.UserId == cls.LecturerId)
                                ?? AvailableLecturers.FirstOrDefault();
        IsCreateClassOpen = true;
    }

    private async Task SubmitCreateClassAsync()
    {
        var cu = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (cu is null) return;

        var lecturerId = IsAdmin
            ? (SelectedClassLecturer?.UserId ?? AvailableLecturers.FirstOrDefault()?.UserId ?? "u_gv_demo01")
            : cu.UserId;

        if (IsEditClassMode)
        {
            var upd = await ClientServiceHost.Current.AuthRoom.UpdateClassAsync(new ClassDto
            {
                ClassId = _editingClassId,
                ClassCode = NewClassCode,
                ClassName = NewClassName,
                Department = NewClassDepartment,
                AcademicYear = NewClassAcademicYear,
                LecturerId = lecturerId,
                AllowStudentJoinByCode = AllowClassJoinByCode
            });

            ShowToast(upd.Message);
            if (upd.Success)
            {
                IsCreateClassOpen = false;
                await RefreshLookupsAsync();
                if (IsAdmin) await Admin.RefreshAsync();
                if (IsLecturer) await Classes.RefreshAsync(_editingClassId);
            }
            return;
        }

        var res = await ClientServiceHost.Current.AuthRoom.CreateClassAsync(new ClassDto
        {
            ClassCode = NewClassCode,
            ClassName = NewClassName,
            Department = NewClassDepartment,
            AcademicYear = NewClassAcademicYear,
            LecturerId = lecturerId,
            AllowStudentJoinByCode = AllowClassJoinByCode
        });

        if (res.Success && res.Data is not null)
        {
            if (AutoCreateClassRoom)
            {
                await ClientServiceHost.Current.AuthRoom.CreateOrLinkClassRoomAsync(res.Data.ClassId, res.Data.ClassName);
            }

            IsCreateClassOpen = false;
            ShowToast(res.Message);
            await RefreshLookupsAsync();
            if (IsAdmin) await Admin.RefreshAsync();
            if (IsLecturer) await Classes.RefreshAsync(res.Data.ClassId);
            await Chat.LoadAsync();
        }
        else
        {
            ShowToast(res.Message);
        }
    }

    // =====================================================
    // 6. UPGRADED 2-TAB BULK ADD MEMBERS MODAL
    // =====================================================
    private async void OpenBulkAddModal(bool isClass, string targetId, string targetDisplayName)
    {
        CloseAllModals();
        await RefreshLookupsAsync();
        _bulkTargetIsClass = isClass;
        _bulkTargetId = targetId;
        _bulkTargetTitle = targetDisplayName;
        OnPropertyChanged(nameof(BulkModalBadge));
        OnPropertyChanged(nameof(BulkModalTitle));

        var host = ClientServiceHost.Current;
        var membersRes = isClass
            ? await host.AuthRoom.GetClassMembersAsync(targetId)
            : await host.AuthRoom.GetRoomMembersAsync(targetId);

        _existingMemberIds = membersRes.Data?.Select(m => m.UserId).ToHashSet() ?? new HashSet<string>();

        var allRes = await host.AuthRoom.GetAllUsersAsync();
        _allCandidates.Clear();

        if (allRes.Success && allRes.Data is not null)
        {
            var eligible = allRes.Data
                .Where(u => !_existingMemberIds.Contains(u.UserId)
                         && u.Status == AccountStatus.Active
                         && u.Role != UserRole.Admin)
                .OrderBy(u => u.Role == UserRole.SinhVien ? 0 : 1)
                .ThenBy(u => u.Username);

            foreach (var u in eligible)
            {
                var (bg, fg) = host.DemoStore.GetAvatarTone(u.UserId);
                var selectable = new SelectableAccountItem
                {
                    Account = new AccountItemModel
                    {
                        RawUser = u,
                        Initials = DemoDataStore.GetInitials(u.FullName),
                        AvatarBgHex = bg,
                        AvatarFgHex = fg
                    }
                };
                selectable.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(SelectableAccountItem.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedCandidatesCount));
                    }
                };
                _allCandidates.Add(selectable);
            }
        }

        _activeBulkAddTab = BulkAddTab.SelectFromList;
        OnPropertyChanged(nameof(ActiveBulkAddTab));
        _candidateSearchQuery = string.Empty;
        OnPropertyChanged(nameof(CandidateSearchQuery));
        _candidateClassFilterOption = CandidateClassFilterOptions.FirstOrDefault();
        OnPropertyChanged(nameof(CandidateClassFilterOption));
        _batchMssvText = string.Empty;
        OnPropertyChanged(nameof(BatchMssvText));

        ApplyCandidateFilter();
        ParseBatchMssvInput();
        OnPropertyChanged(nameof(SelectedCandidatesCount));
        IsAddMemberOpen = true;
    }

    private void ApplyCandidateFilter()
    {
        FilteredCandidates.Clear();
        var q = CandidateSearchQuery.Trim();
        var filterClassId = CandidateClassFilterOption?.ClassId;

        foreach (var item in _allCandidates)
        {
            if (!string.IsNullOrEmpty(filterClassId) && item.ClassId != filterClassId)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(q)
                && !item.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !item.Username.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !item.ClassCaption.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FilteredCandidates.Add(item);
        }
    }

    private void ImportMssvFromFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Chọn danh sách MSSV (.csv hoặc .txt)",
            Filter = "Danh sách MSSV (*.csv;*.txt)|*.csv;*.txt|Tất cả tệp (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var raw = File.ReadAllText(dlg.FileName);
            BatchMssvText = string.IsNullOrWhiteSpace(BatchMssvText)
                ? raw
                : $"{BatchMssvText}\n{raw}";
        }
        catch (Exception ex)
        {
            ShowToast($"Không thể đọc tệp: {ex.Message}");
        }
    }

    private void FillAvailableStudentsMssv()
    {
        var codes = _allCandidates
            .Where(c => c.Role == UserRole.SinhVien)
            .Select(c => c.Username)
            .ToList();

        if (codes.Count == 0)
        {
            ShowToast("Không còn sinh viên nào chưa tham gia để điền mẫu.");
            return;
        }

        BatchMssvText = string.Join(", ", codes);
    }

    private void ParseBatchMssvInput()
    {
        _parsedMatchedUserIds.Clear();
        MatchedMssvCount = 0;
        MatchedPreviewText = string.Empty;
        AlreadyMembersNote = string.Empty;
        UnmatchedTokensNote = string.Empty;

        var raw = BatchMssvText;
        if (string.IsNullOrWhiteSpace(raw)) return;

        var tokens = raw
            .Split(new[] { ',', ';', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t)
                     && !t.Equals("mssv", StringComparison.OrdinalIgnoreCase)
                     && !t.Equals("username", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allByUsername = ClientServiceHost.Current.DemoStore.Accounts
            .ToDictionary(a => a.User.Username, a => a.User, StringComparer.OrdinalIgnoreCase);

        var matchedNames = new List<string>();
        var alreadyTokens = new List<string>();
        var unmatchedTokens = new List<string>();

        foreach (var token in tokens)
        {
            if (!allByUsername.TryGetValue(token, out var user) || user.Status != AccountStatus.Active || user.Role == UserRole.Admin)
            {
                unmatchedTokens.Add(token);
                continue;
            }

            if (_existingMemberIds.Contains(user.UserId))
            {
                alreadyTokens.Add(user.Username);
                continue;
            }

            _parsedMatchedUserIds.Add(user.UserId);
            matchedNames.Add($"{user.Username} ({user.FullName})");
        }

        MatchedMssvCount = _parsedMatchedUserIds.Count;
        MatchedPreviewText = matchedNames.Count > 0 ? string.Join(", ", matchedNames) : string.Empty;
        AlreadyMembersNote = alreadyTokens.Count > 0 ? $"Đã có trong danh sách: {string.Join(", ", alreadyTokens)}" : string.Empty;
        UnmatchedTokensNote = unmatchedTokens.Count > 0 ? $"Không tìm thấy hoặc bị khóa: {string.Join(", ", unmatchedTokens)}" : string.Empty;
    }

    private async Task SubmitSelectedCandidatesAsync()
    {
        var ids = _allCandidates.Where(c => c.IsSelected).Select(c => c.UserId).ToList();
        if (ids.Count == 0)
        {
            ShowToast("Vui lòng tích chọn ít nhất 1 thành viên để thêm.");
            return;
        }

        await ExecuteBulkAddAsync(ids);
    }

    private async Task SubmitBatchMssvAsync()
    {
        if (_parsedMatchedUserIds.Count == 0)
        {
            ShowToast("Chưa có MSSV / tài khoản hợp lệ nào trong danh sách nhập.");
            return;
        }

        await ExecuteBulkAddAsync(_parsedMatchedUserIds.ToList());
    }

    private async Task ExecuteBulkAddAsync(IReadOnlyList<string> userIds)
    {
        var host = ClientServiceHost.Current;
        var res = _bulkTargetIsClass
            ? await host.AuthRoom.AddClassMembersAsync(_bulkTargetId, userIds)
            : await host.AuthRoom.AddRoomMembersAsync(_bulkTargetId, userIds);

        ShowToast(res.Message);
        if (res.Success)
        {
            IsAddMemberOpen = false;
            if (IsLecturer) await Classes.RefreshAsync(_bulkTargetIsClass ? _bulkTargetId : Classes.SelectedClass?.ClassId);
            if (IsAdmin) await Admin.RefreshAsync();
            await Chat.LoadAsync(Chat.SelectedConversation?.Id);
        }
    }

    // =====================================================
    // 7. ADMIN MEMBERS MANAGER MODAL (CLASS OR ROOM)
    // =====================================================
    private async void OpenMembersManagerModal(bool isClass, string targetId, string title)
    {
        CloseAllModals();
        _membersManagerIsClass = isClass;
        _membersManagerTargetId = targetId;
        _membersManagerTitle = title;
        OnPropertyChanged(nameof(MembersManagerBadge));
        OnPropertyChanged(nameof(MembersManagerTitle));

        await ReloadMembersManagerListAsync();
        IsMembersManagerOpen = true;
    }

    private async Task ReloadMembersManagerListAsync()
    {
        var host = ClientServiceHost.Current;
        if (_membersManagerIsClass)
        {
            var clsRes = await host.AuthRoom.GetClassesAsync();
            _membersManagerProtectedOwnerId = clsRes.Data?.FirstOrDefault(c => c.ClassId == _membersManagerTargetId)?.LecturerId;
        }
        else
        {
            var currentUser = host.AuthRoom.CurrentUser;
            if (currentUser is not null)
            {
                var roomsRes = await host.Messaging.GetJoinedRoomsAsync(currentUser.UserId);
                _membersManagerProtectedOwnerId = roomsRes.Data?.FirstOrDefault(r => r.RoomId == _membersManagerTargetId)?.OwnerId;
            }
        }

        var membersRes = _membersManagerIsClass
            ? await host.AuthRoom.GetClassMembersAsync(_membersManagerTargetId)
            : await host.AuthRoom.GetRoomMembersAsync(_membersManagerTargetId);

        ManagedMembers.Clear();
        if (membersRes.Success && membersRes.Data is not null)
        {
            foreach (var m in membersRes.Data)
            {
                var (bg, fg) = host.DemoStore.GetAvatarTone(m.UserId);
                ManagedMembers.Add(new RoomMemberItemModel
                {
                    UserId = m.UserId,
                    FullName = m.FullName,
                    Username = m.Username,
                    Initials = DemoDataStore.GetInitials(m.FullName),
                    AvatarBgHex = bg,
                    AvatarFgHex = fg,
                    Role = m.Role,
                    Presence = m.Presence,
                    CanBeRemoved = m.UserId != _membersManagerProtectedOwnerId
                });
            }
        }
    }

    private async Task RemoveManagedMemberAsync(object? param)
    {
        if (param is not RoomMemberItemModel member) return;
        var host = ClientServiceHost.Current;
        var res = _membersManagerIsClass
            ? await host.AuthRoom.RemoveClassMembersAsync(_membersManagerTargetId, new[] { member.UserId })
            : await host.AuthRoom.RemoveRoomMemberAsync(_membersManagerTargetId, member.UserId);

        ShowToast(res.Message);
        if (res.Success)
        {
            await ReloadMembersManagerListAsync();
            if (IsAdmin) await Admin.RefreshAsync();
            await Chat.LoadAsync(Chat.SelectedConversation?.Id);
        }
    }

    // =====================================================
    // 8. STUDENT JOIN BY CLASS CODE MODAL
    // =====================================================
    private async void OpenJoinByCodeModal()
    {
        CloseAllModals();
        JoinCodeInput = string.Empty;
        AvailableJoinCodeHints.Clear();

        var cu = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (cu is not null)
        {
            var clsRes = await ClientServiceHost.Current.AuthRoom.GetClassesAsync();
            if (clsRes.Success && clsRes.Data is not null)
            {
                foreach (var c in clsRes.Data.Where(c => c.AllowStudentJoinByCode
                                                      && !string.IsNullOrWhiteSpace(c.JoinCode)
                                                      && !c.MemberIds.Contains(cu.UserId)))
                {
                    AvailableJoinCodeHints.Add(new ClassItemModel { RawClass = c });
                }
            }
        }

        IsJoinByCodeOpen = true;
    }

    private async Task SubmitJoinByCodeAsync()
    {
        var cu = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (cu is null) return;

        var res = await ClientServiceHost.Current.AuthRoom.JoinByCodeAsync(cu.UserId, JoinCodeInput);
        ShowToast(res.Message);
        if (res.Success)
        {
            IsJoinByCodeOpen = false;
            var roomId = res.Data?.JoinedRoom?.RoomId ?? res.Data?.JoinedClass?.LinkedRoomId;
            if (!string.IsNullOrEmpty(roomId))
            {
                await OpenRoomInChatAsync(roomId);
            }
            else
            {
                await Chat.LoadAsync();
            }
        }
    }

    // =====================================================
    // 9. PIN NOTICE MODAL
    // =====================================================
    private void OpenPinNoticeModal(string roomId, string? existingText)
    {
        CloseAllModals();
        _pinRoomId = roomId;
        PinContentText = existingText ?? string.Empty;
        IsPinModalOpen = true;
    }

    private async Task SubmitPinNoticeAsync()
    {
        var user = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (user is null || string.IsNullOrEmpty(_pinRoomId) || string.IsNullOrWhiteSpace(PinContentText))
        {
            ShowToast("Vui lòng nhập nội dung thông báo cần ghim.");
            return;
        }

        var res = await ClientServiceHost.Current.Messaging.PinRoomAnnouncementAsync(_pinRoomId, user.UserId, PinContentText);
        if (res.Success)
        {
            IsPinModalOpen = false;
            ShowToast(res.Message);
        }
    }

    private string ResolveRoomTitle(string roomId)
    {
        lock (ClientServiceHost.Current.DemoStore.SyncRoot)
        {
            return ClientServiceHost.Current.DemoStore.Rooms
                .FirstOrDefault(r => r.RoomId == roomId)?.RoomName ?? "Phòng chat";
        }
    }
}
