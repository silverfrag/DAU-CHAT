namespace DauChat.Shared.Enums;

/// <summary>
/// Nhóm định dạng tệp phục vụ lọc kho tài liệu Local (MOD_07_04: CAD, PDF, ZIP, Ảnh...).
/// </summary>
public enum FileCategory
{
    All = 0,
    CAD = 1,
    PDF = 2,
    Archive = 3,
    Image = 4,
    Document = 5,
    Other = 99
}

