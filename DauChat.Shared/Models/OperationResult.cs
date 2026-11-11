namespace DauChat.Shared.Models;

/// <summary>
/// Kết quả trả về chuẩn cho các thao tác nghiệp vụ bất đồng bộ giữa các tầng.
/// </summary>
public class OperationResult
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;

    public static OperationResult Ok(string message = "OK") =>
        new() { Success = true, Message = message };

    public static OperationResult Fail(string message, string? errorCode = null) =>
        new() { Success = false, Message = message, ErrorCode = errorCode };
}

/// <summary>
/// Kết quả trả về chuẩn kèm dữ liệu kiểu T.
/// </summary>
public sealed class OperationResult<T> : OperationResult
{
    public T? Data { get; set; }

    public static OperationResult<T> Ok(T data, string message = "OK") =>
        new() { Success = true, Data = data, Message = message };

    public new static OperationResult<T> Fail(string message, string? errorCode = null) =>
        new() { Success = false, Message = message, ErrorCode = errorCode };
}

