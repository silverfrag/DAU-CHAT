using DauChat.Client.UI.Infrastructure;
using DauChat.Shared.Enums;
using DauChat.Shared.Models;

namespace DauChat.Client.UI.Models;

public enum NavSection
{
    Messages,
    Rooms,
    Classes,
    Files,
    Storage,
    Admin
}

public enum AdminSubTab
{
    OverviewAndUsers,
    Classes,
    Rooms,
    ServerMonitoring
}

public enum AccountFilterOption
{
    All,
    Students,
    Lecturers,
    Admins,
    Locked
}

public sealed class DemoQuickAccountModel
{
    public string Label { get; init; } = string.Empty;
    public string RoleTag { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public string BadgeBgHex { get; init; } = "#FEF2F2";
    public string BadgeFgHex { get; init; } = "#CB0914";
}

public sealed class ConversationItemModel : ObservableObject
{
    private string _subtitle = string.Empty;
    private string _timeLabel = string.Empty;
    private int _unreadCount;
    private bool _isOnline;
    private bool _isSelected;
    private bool _isMuted;

    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public bool IsRoom { get; init; }
    public string Initials { get; init; } = string.Empty;
    public string AvatarBgHex { get; init; } = "#FFF1F1";
    public string AvatarFgHex { get; init; } = "#CB0914";
    public string MetaCaption { get; init; } = string.Empty;
    public RoomDto? RoomData { get; init; }
    public UserDto? PartnerData { get; init; }

    public bool HasJoinCode => IsRoom && !string.IsNullOrWhiteSpace(RoomData?.JoinCode);
    public string RoomJoinCode => RoomData?.JoinCode ?? string.Empty;

    public string Subtitle
    {
        get => _subtitle;
        set => SetProperty(ref _subtitle, value);
    }

    public string TimeLabel
    {
        get => _timeLabel;
        set => SetProperty(ref _timeLabel, value);
    }

    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (SetProperty(ref _unreadCount, value))
            {
                OnPropertyChanged(nameof(HasUnread));
            }
        }
    }

    public bool HasUnread => UnreadCount > 0;

    public bool IsOnline
    {
        get => _isOnline;
        set => SetProperty(ref _isOnline, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsMuted
    {
        get => _isMuted;
        set => SetProperty(ref _isMuted, value);
    }
}

public sealed class MessageItemModel
{
    public string MessageId { get; init; } = string.Empty;
    public string SenderId { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string SenderInitials { get; init; } = string.Empty;
    public string AvatarBgHex { get; init; } = "#1E293B";
    public string AvatarFgHex { get; init; } = "#FFFFFF";
    public string SenderRoleLabel { get; init; } = string.Empty;
    public bool HasRoleBadge => !string.IsNullOrEmpty(SenderRoleLabel);
    public bool IsOwnMessage { get; init; }
    public bool CanSendFromClient { get; init; }
    public bool IsRecalled { get; init; }
    public bool IsAttachment { get; init; }
    public bool IsSystemNotice { get; init; }
    public bool CanPinThisMessage { get; init; }

    public bool IsIncomingText => !IsOwnMessage && !IsAttachment && !IsSystemNotice && !IsRecalled;
    public bool IsOutgoingText => IsOwnMessage && !IsAttachment && !IsSystemNotice && !IsRecalled;
    public bool IsIncomingAttachment => !IsOwnMessage && IsAttachment && !IsRecalled;
    public bool IsOutgoingAttachment => IsOwnMessage && IsAttachment && !IsRecalled;
    public bool IsIncomingRecalled => !IsOwnMessage && IsRecalled && !IsSystemNotice;
    public bool IsOutgoingRecalled => IsOwnMessage && IsRecalled && !IsSystemNotice;

    public bool CanReplyThisMessage => CanSendFromClient && !IsRecalled && !IsSystemNotice;
    public bool CanCopyThisMessage => !IsRecalled && !IsSystemNotice;
    public bool CanRecallThisMessage => CanSendFromClient && IsOwnMessage && !IsRecalled && !IsSystemNotice;
    public bool CanDeleteForMe => CanSendFromClient && !IsSystemNotice;
    public bool ShowContextActionSeparator => CanCopyThisMessage && CanDeleteForMe;

    public DateTime LocalTimestamp { get; init; }
    public DateTime LocalDate => LocalTimestamp.Date;
    public bool ShowDateDivider { get; set; }
    public string DateDividerLabel { get; set; } = string.Empty;

    public string Content { get; init; } = string.Empty;
    public string BodyContent { get; init; } = string.Empty;
    public bool HasReplyQuote { get; init; }
    public string ReplyAuthor { get; init; } = string.Empty;
    public string ReplySnippet { get; init; } = string.Empty;

    public string TimeText { get; init; } = string.Empty;
    public string? AttachmentFileId { get; init; }
    public string? AttachmentFileName { get; init; }
    public string AttachmentSizeText { get; init; } = string.Empty;
    public string AttachmentBadgeText { get; init; } = "FILE";
    public string AttachmentBadgeBgHex { get; init; } = "#EFF6FF";
    public string AttachmentBadgeFgHex { get; init; } = "#2563EB";
    public string? LocalFilePath { get; init; }
    public bool HasImagePreview { get; init; }
    public bool IsFileCardWithoutPreview => IsAttachment && !HasImagePreview;
    public bool IsPinned { get; init; }
}

public sealed class FileTransferItemModel : ObservableObject
{
    private double _percentage;
    private string _statusText = string.Empty;
    private bool _isCompleted;
    private string _sha256Preview = string.Empty;

    public string TransferId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string TotalSizeText { get; init; } = string.Empty;

    public double Percentage
    {
        get => _percentage;
        set => SetProperty(ref _percentage, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }

    public string Sha256Preview
    {
        get => _sha256Preview;
        set => SetProperty(ref _sha256Preview, value);
    }
}

public sealed class RoomMemberItemModel
{
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public string AvatarBgHex { get; init; } = "#E2E8F0";
    public string AvatarFgHex { get; init; } = "#334155";
    public UserRole Role { get; init; }
    public PresenceStatus Presence { get; init; }
    public bool IsOnline => Presence != PresenceStatus.Offline;
    public string RoleBadgeBgHex => Role switch
    {
        UserRole.Admin => "#FEF2F2",
        UserRole.GiangVien => "#EFF6FF",
        _ => "#F1F5F9"
    };
    public string RoleBadgeFgHex => Role switch
    {
        UserRole.Admin => "#C41313",
        UserRole.GiangVien => "#1D4ED8",
        _ => "#475569"
    };
    public bool CanBeRemoved { get; init; }
}

public sealed class FileItemModel
{
    public string FileId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public FileCategory Category { get; init; }
    public string CategoryLabel { get; init; } = string.Empty;
    public string SizeAndTimeText { get; init; } = string.Empty;
    public string ConversationName { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string Sha256Short { get; init; } = string.Empty;
    public string LocalFullPath { get; init; } = string.Empty;
    public string IconBgHex { get; init; } = "#FEF2F2";
    public string IconFgHex { get; init; } = "#DC2626";
}

public sealed class RoomStorageItemModel
{
    public string DisplayName { get; init; } = string.Empty;
    public string UsedText { get; init; } = string.Empty;
    public string DetailText { get; init; } = string.Empty;
    public double BarPercent { get; init; }
    public string BarHex { get; init; } = "#CB0914";
}

public sealed class AccountItemModel
{
    public UserDto RawUser { get; init; } = new();
    public string UserId => RawUser.UserId;
    public string Username => RawUser.Username;
    public string FullName => RawUser.FullName;
    public string Email => RawUser.Email;
    public UserRole Role => RawUser.Role;
    public AccountStatus Status => RawUser.Status;
    public bool IsActive => RawUser.Status == AccountStatus.Active;
    public bool IsLocked => RawUser.Status == AccountStatus.Locked;
    public bool IsAdmin => RawUser.Role == UserRole.Admin;
    public bool CanToggleLock { get; init; } = true;
    public string Initials { get; init; } = string.Empty;
    public string AvatarBgHex { get; init; } = "#F1F5F9";
    public string AvatarFgHex { get; init; } = "#334155";
    public string RoleBadgeBgHex => RawUser.Role switch
    {
        UserRole.Admin => "#FEF2F2",
        UserRole.GiangVien => "#EFF6FF",
        _ => "#F1F5F9"
    };
    public string RoleBadgeFgHex => RawUser.Role switch
    {
        UserRole.Admin => "#C41313",
        UserRole.GiangVien => "#1D4ED8",
        _ => "#475569"
    };
    public string ClassCaption => string.IsNullOrWhiteSpace(RawUser.ClassName) ? "Chưa gán lớp" : RawUser.ClassName;
}

public sealed class SelectableAccountItem : ObservableObject
{
    private bool _isSelected;

    public AccountItemModel Account { get; init; } = new();
    public string UserId => Account.UserId;
    public string Username => Account.Username;
    public string FullName => Account.FullName;
    public UserRole Role => Account.Role;
    public string? ClassId => Account.RawUser.ClassId;
    public string ClassCaption => Account.ClassCaption;
    public string Initials => Account.Initials;
    public string AvatarBgHex => Account.AvatarBgHex;
    public string AvatarFgHex => Account.AvatarFgHex;
    public string RoleBadgeBgHex => Account.RoleBadgeBgHex;
    public string RoleBadgeFgHex => Account.RoleBadgeFgHex;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class ClassItemModel : ObservableObject
{
    private bool _isSelected;

    public ClassDto RawClass { get; init; } = new();
    public string ClassId => RawClass.ClassId;
    public string ClassCode => RawClass.ClassCode;
    public string ClassName => RawClass.ClassName;
    public string Department => RawClass.Department;
    public string AcademicYear => RawClass.AcademicYear;
    public string? LecturerId => RawClass.LecturerId;
    public string LecturerName => RawClass.LecturerName ?? "Chưa phân công";
    public string? LinkedRoomId => RawClass.LinkedRoomId;
    public string JoinCode => string.IsNullOrWhiteSpace(RawClass.JoinCode) ? "CHƯA CẤP" : RawClass.JoinCode;
    public bool AllowStudentJoinByCode => RawClass.AllowStudentJoinByCode;
    public int MemberCount => RawClass.MemberCount;
    public bool HasLinkedRoom => !string.IsNullOrEmpty(RawClass.LinkedRoomId);
    public string LinkedRoomLabel => HasLinkedRoom ? "Đã liên kết phòng chat" : "Chưa liên kết phòng";
    public string SubtitleCaption => $"{Department} • {MemberCount} thành viên";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class RoomItemModel
{
    public RoomDto RawRoom { get; init; } = new();
    public string RoomId => RawRoom.RoomId;
    public string RoomName => RawRoom.RoomName;
    public string Description => string.IsNullOrWhiteSpace(RawRoom.Description) ? "Phòng trao đổi nội bộ DAU" : RawRoom.Description!;
    public RoomType RoomType => RawRoom.RoomType;
    public string RoomTypeLabel => RawRoom.RoomType switch
    {
        RoomType.ClassRoom => "Lớp học",
        RoomType.StudyGroup => "Nhóm học tập",
        RoomType.General => "Thảo luận chung",
        _ => "Nhóm học tập"
    };
    public string OwnerName { get; init; } = string.Empty;
    public string JoinCode => string.IsNullOrWhiteSpace(RawRoom.JoinCode) ? "—" : RawRoom.JoinCode;
    public int MemberCount => RawRoom.MemberIds.Count;
    public bool HasPinnedNotice => RawRoom.PinnedNotice is not null;
    public string MetaCaption => $"{RoomTypeLabel} • Chủ phòng: {OwnerName} • {MemberCount} thành viên";
}

public sealed class BatchAccountPreviewItem
{
    public int LineNumber { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public UserRole Role { get; init; } = UserRole.SinhVien;
    public string ClassCode { get; init; } = string.Empty;
    public string? ResolvedClassId { get; init; }
    public bool IsValid { get; init; }
    public string StatusNote { get; init; } = string.Empty;
    public string StatusBgHex => IsValid ? "#ECFDF5" : "#FEF2F2";
    public string StatusFgHex => IsValid ? "#047857" : "#DC2626";
}
