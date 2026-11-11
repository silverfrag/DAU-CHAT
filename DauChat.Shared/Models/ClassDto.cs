namespace DauChat.Shared.Models;

/// <summary>
/// DTO thông tin lớp học / nhóm môn học (MOD_06_01 -> MOD_06_07).
/// </summary>
public sealed class ClassDto
{
    public string ClassId { get; set; } = string.Empty;
    public string ClassCode { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public string? LecturerId { get; set; }
    public string? LecturerName { get; set; }
    public string? LinkedRoomId { get; set; }
    public int MemberCount { get; set; }
    public List<string> MemberIds { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

