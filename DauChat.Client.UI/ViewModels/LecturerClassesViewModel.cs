using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DauChat.Client.UI.Infrastructure;
using DauChat.Client.UI.Models;
using DauChat.Client.UI.Services;
using DauChat.Shared.Enums;
using DauChat.Shared.Models;

namespace DauChat.Client.UI.ViewModels;

public sealed class LecturerClassesViewModel : ObservableObject
{
    private readonly Action<string> _notify;
    private readonly Action _onOpenCreateClass;
    private readonly Action<ClassItemModel> _onOpenEditClass;
    private readonly Action<string, string> _onOpenBulkAddMembers;
    private readonly Func<string, Task> _onOpenRoomInChatAsync;

    private List<ClassItemModel> _allClasses = new();
    private List<RoomMemberItemModel> _allSelectedMembers = new();
    private ClassItemModel? _selectedClass;
    private string _searchQuery = string.Empty;
    private string _memberSearchQuery = string.Empty;
    private bool _isLoading;

    public LecturerClassesViewModel(
        Action<string> notify,
        Action onOpenCreateClass,
        Action<ClassItemModel> onOpenEditClass,
        Action<string, string> onOpenBulkAddMembers,
        Func<string, Task> onOpenRoomInChatAsync)
    {
        _notify = notify;
        _onOpenCreateClass = onOpenCreateClass;
        _onOpenEditClass = onOpenEditClass;
        _onOpenBulkAddMembers = onOpenBulkAddMembers;
        _onOpenRoomInChatAsync = onOpenRoomInChatAsync;

        SelectClassCommand = new AsyncRelayCommand(async param =>
        {
            if (param is ClassItemModel item)
            {
                await SelectClassAsync(item);
            }
        });
        OpenCreateClassCommand = new RelayCommand(() => _onOpenCreateClass());
        OpenEditClassCommand = new RelayCommand(() =>
        {
            if (SelectedClass is not null) _onOpenEditClass(SelectedClass);
        }, () => SelectedClass is not null);
        DeleteClassCommand = new AsyncRelayCommand(DeleteSelectedClassAsync, () => SelectedClass is not null);
        CopyJoinCodeCommand = new RelayCommand(CopyJoinCode, () => SelectedClass is not null);
        RegenerateJoinCodeCommand = new AsyncRelayCommand(RegenerateJoinCodeAsync, () => SelectedClass is not null);
        ToggleAllowJoinCodeCommand = new AsyncRelayCommand(ToggleAllowJoinCodeAsync, () => SelectedClass is not null);
        OpenBulkAddMembersCommand = new RelayCommand(() =>
        {
            if (SelectedClass is not null)
            {
                _onOpenBulkAddMembers(SelectedClass.ClassId, $"{SelectedClass.ClassCode} — {SelectedClass.ClassName}");
            }
        }, () => SelectedClass is not null);
        RemoveMemberCommand = new AsyncRelayCommand(RemoveMemberAsync);
        OpenOrSyncLinkedRoomCommand = new AsyncRelayCommand(OpenOrSyncLinkedRoomAsync, () => SelectedClass is not null);
        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync(SelectedClass?.ClassId));
    }

    public ObservableCollection<ClassItemModel> FilteredClasses { get; } = new();
    public ObservableCollection<RoomMemberItemModel> FilteredMembers { get; } = new();

    public ICommand SelectClassCommand { get; }
    public ICommand OpenCreateClassCommand { get; }
    public ICommand OpenEditClassCommand { get; }
    public ICommand DeleteClassCommand { get; }
    public ICommand CopyJoinCodeCommand { get; }
    public ICommand RegenerateJoinCodeCommand { get; }
    public ICommand ToggleAllowJoinCodeCommand { get; }
    public ICommand OpenBulkAddMembersCommand { get; }
    public ICommand RemoveMemberCommand { get; }
    public ICommand OpenOrSyncLinkedRoomCommand { get; }
    public ICommand RefreshCommand { get; }

    public ClassItemModel? SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (SetProperty(ref _selectedClass, value))
            {
                OnPropertyChanged(nameof(HasSelectedClass));
                OnPropertyChanged(nameof(AllowStudentJoinByCode));
                OnPropertyChanged(nameof(LinkedRoomActionLabel));
            }
        }
    }

    public bool HasSelectedClass => SelectedClass is not null;
    public bool AllowStudentJoinByCode => SelectedClass?.AllowStudentJoinByCode ?? false;
    public string LinkedRoomActionLabel => SelectedClass?.HasLinkedRoom == true
        ? "Mở phòng chat lớp"
        : "Tạo & đồng bộ phòng chat";

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyClassFilter();
            }
        }
    }

    public string MemberSearchQuery
    {
        get => _memberSearchQuery;
        set
        {
            if (SetProperty(ref _memberSearchQuery, value))
            {
                ApplyMemberFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public int TotalClassesCount => _allClasses.Count;

    public async Task RefreshAsync(string? preferClassId = null)
    {
        var currentUser = ClientServiceHost.Current.AuthRoom.CurrentUser;
        if (currentUser is null || currentUser.Role != UserRole.GiangVien) return;

        IsLoading = true;
        try
        {
            var res = await ClientServiceHost.Current.AuthRoom.GetClassesAsync();
            if (!res.Success || res.Data is null) return;

            var ownClasses = res.Data
                .Where(c => c.LecturerId == currentUser.UserId || c.MemberIds.Contains(currentUser.UserId))
                .Select(c => new ClassItemModel { RawClass = c })
                .ToList();

            _allClasses = ownClasses;
            OnPropertyChanged(nameof(TotalClassesCount));
            ApplyClassFilter();

            var targetId = preferClassId ?? SelectedClass?.ClassId;
            var target = _allClasses.FirstOrDefault(c => c.ClassId == targetId)
                         ?? FilteredClasses.FirstOrDefault()
                         ?? _allClasses.FirstOrDefault();

            if (target is not null)
            {
                await SelectClassAsync(target);
            }
            else
            {
                SelectedClass = null;
                _allSelectedMembers.Clear();
                ApplyMemberFilter();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectClassAsync(ClassItemModel item)
    {
        foreach (var c in _allClasses)
        {
            c.IsSelected = c.ClassId == item.ClassId;
        }

        SelectedClass = item;
        var host = ClientServiceHost.Current;
        var membersRes = await host.AuthRoom.GetClassMembersAsync(item.ClassId);

        _allSelectedMembers.Clear();
        if (membersRes.Success && membersRes.Data is not null)
        {
            foreach (var u in membersRes.Data)
            {
                var (bg, fg) = host.DemoStore.GetAvatarTone(u.UserId);
                _allSelectedMembers.Add(new RoomMemberItemModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Username = u.Username,
                    Initials = DemoDataStore.GetInitials(u.FullName),
                    AvatarBgHex = bg,
                    AvatarFgHex = fg,
                    Role = u.Role,
                    Presence = u.Presence,
                    CanBeRemoved = u.UserId != item.LecturerId
                });
            }
        }

        ApplyMemberFilter();
    }

    private void ApplyClassFilter()
    {
        FilteredClasses.Clear();
        var q = SearchQuery.Trim();
        foreach (var c in _allClasses)
        {
            if (!string.IsNullOrEmpty(q)
                && !c.ClassCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !c.ClassName.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !c.JoinCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !c.Department.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            FilteredClasses.Add(c);
        }
    }

    private void ApplyMemberFilter()
    {
        FilteredMembers.Clear();
        var q = MemberSearchQuery.Trim();
        foreach (var m in _allSelectedMembers)
        {
            if (!string.IsNullOrEmpty(q)
                && !m.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !m.Username.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            FilteredMembers.Add(m);
        }
    }

    private void CopyJoinCode()
    {
        if (SelectedClass is null) return;
        try
        {
            Clipboard.SetText(SelectedClass.JoinCode);
            _notify($"Đã sao chép mã lớp '{SelectedClass.JoinCode}'. Chia sẻ mã này để sinh viên tham gia ngay.");
        }
        catch
        {
            _notify($"Mã lớp hiện tại: {SelectedClass.JoinCode}");
        }
    }

    private async Task RegenerateJoinCodeAsync()
    {
        if (SelectedClass is null) return;
        var res = await ClientServiceHost.Current.AuthRoom.RegenerateJoinCodeAsync(SelectedClass.ClassId);
        _notify(res.Message);
        if (res.Success)
        {
            await RefreshAsync(SelectedClass.ClassId);
        }
    }

    private async Task ToggleAllowJoinCodeAsync()
    {
        if (SelectedClass is null) return;
        var raw = SelectedClass.RawClass;
        var next = new ClassDto
        {
            ClassId = raw.ClassId,
            ClassCode = raw.ClassCode,
            ClassName = raw.ClassName,
            Department = raw.Department,
            AcademicYear = raw.AcademicYear,
            LecturerId = raw.LecturerId,
            LecturerName = raw.LecturerName,
            JoinCode = raw.JoinCode,
            AllowStudentJoinByCode = !raw.AllowStudentJoinByCode
        };

        var res = await ClientServiceHost.Current.AuthRoom.UpdateClassAsync(next);
        if (res.Success)
        {
            _notify(next.AllowStudentJoinByCode
                ? $"Đã bật cho phép sinh viên tự tham gia bằng mã {raw.JoinCode}."
                : $"Đã tạm khóa tự tham gia bằng mã cho lớp {raw.ClassCode}.");
            await RefreshAsync(raw.ClassId);
        }
        else
        {
            _notify(res.Message);
        }
    }

    private async Task RemoveMemberAsync(object? param)
    {
        if (SelectedClass is null || param is not RoomMemberItemModel member) return;
        var res = await ClientServiceHost.Current.AuthRoom.RemoveClassMembersAsync(
            SelectedClass.ClassId,
            new[] { member.UserId });
        _notify(res.Message);
        if (res.Success)
        {
            await RefreshAsync(SelectedClass.ClassId);
        }
    }

    private async Task OpenOrSyncLinkedRoomAsync()
    {
        if (SelectedClass is null) return;
        var res = await ClientServiceHost.Current.AuthRoom.CreateOrLinkClassRoomAsync(
            SelectedClass.ClassId,
            $"{SelectedClass.ClassName}");
        _notify(res.Message);
        await RefreshAsync(SelectedClass.ClassId);
        if (res.Success && res.Data is not null)
        {
            await _onOpenRoomInChatAsync(res.Data.RoomId);
        }
    }

    private async Task DeleteSelectedClassAsync()
    {
        if (SelectedClass is null) return;
        var target = SelectedClass;
        var confirm = MessageBox.Show(
            $"Xóa lớp học phần '{target.ClassCode} - {target.ClassName}'?",
            "Xóa lớp học phần",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var res = await ClientServiceHost.Current.AuthRoom.DeleteClassAsync(target.ClassId);
        _notify(res.Message);
        if (res.Success)
        {
            await RefreshAsync();
        }
    }
}

