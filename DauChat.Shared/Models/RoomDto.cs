using DauChat.Shared.Enums;

namespace DauChat.Shared.Models;

/// <summary>
/// DTO thông tin phòng chat / nhóm học (MOD_04_01 -> MOD_04_15, MOD_06_07).
/// </summary>
public sealed class RoomDto
{
    public string RoomId { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RoomType RoomType { get; set; } = RoomType.StudyGroup;
    public string? ClassId { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public List<string> MemberIds { get; set; } = new();
    public PinnedNoticeDto? PinnedNotice { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastActivityAtUtc { get; set; }
}

/// <summary>
/// Thông báo quan trọng được Giảng viên ghim trên đầu phòng chat (MOD_04_14, MOD_04_15).
/// </summary>
public sealed class PinnedNoticeDto
{
    public string MessageId { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public string PinnedByUserId { get; set; } = string.Empty;
    public string PinnedByName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PinnedAtUtc { get; set; } = DateTime.UtcNow;
}

