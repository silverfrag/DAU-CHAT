using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using DauChat.Shared.Constants;
using DauChat.Shared.Enums;

namespace DauChat.Shared.Networking;

/// <summary>
/// Cấu trúc gói tin TCP chuẩn của DAU Chat, hỗ trợ Length-prefix framing (4 byte độ dài + Payload)
/// để chống dính gói tin hoặc đứt gãy gói tin trên NetworkStream (MOD_02_01, MOD_02_06, MOD_02_07).
/// </summary>
public sealed class NetworkPacket
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Mã định danh duy nhất của gói tin (dùng cho ACK / PendingQueue).</summary>
    public string PacketId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Mã tương quan để ghép cặp Request - Response bất đồng bộ.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Loại gói tin phục vụ PacketDispatcher định tuyến tới TV3 / TV4 / TV5.</summary>
    public PacketType Type { get; set; } = PacketType.Unknown;

    /// <summary>UserId người gửi (hoặc "SYSTEM").</summary>
    public string SenderId { get; set; } = string.Empty;

    /// <summary>UserId người nhận hoặc RoomId đích.</summary>
    public string? TargetId { get; set; }

    /// <summary>Mốc thời gian UTC tạo/gán bởi Server.</summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Thời gian sống (giây) khi nằm trong PendingQueue lúc người nhận Offline (MOD_02_06, MOD_02_07).</summary>
    public int TtlSeconds { get; set; } = ProtocolConstants.DefaultPendingQueueTtlSeconds;

    /// <summary>Đánh dấu gói tin yêu cầu phản hồi ACK khi giao thành công (MOD_02_07).</summary>
    public bool RequiresAck { get; set; }

    /// <summary>Nội dung JSON của DTO đi kèm.</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Dữ liệu nhị phân tùy chọn (ví dụ chunk byte[] khi truyền file).</summary>
    public byte[]? BinaryPayload { get; set; }

    public static NetworkPacket Create<T>(
        PacketType type,
        string senderId,
        T payload,
        string? targetId = null,
        string? correlationId = null,
        bool requiresAck = false)
    {
        var packet = new NetworkPacket
        {
            Type = type,
            SenderId = senderId,
            TargetId = targetId,
            CorrelationId = correlationId,
            RequiresAck = requiresAck,
            TimestampUtc = DateTime.UtcNow
        };
        packet.SetPayload(payload);
        return packet;
    }

    public static NetworkPacket CreateAck(string senderId, string acknowledgedPacketId, string? targetId = null)
    {
        return new NetworkPacket
        {
            Type = PacketType.PacketAck,
            SenderId = senderId,
            TargetId = targetId,
            CorrelationId = acknowledgedPacketId,
            TimestampUtc = DateTime.UtcNow
        };
    }

    public void SetPayload<T>(T payload)
    {
        PayloadJson = JsonSerializer.Serialize(payload, JsonOptions);
    }

    public T? GetPayload<T>()
    {
        if (string.IsNullOrWhiteSpace(PayloadJson))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(PayloadJson, JsonOptions);
    }

    /// <summary>
    /// Tuần tự hóa toàn bộ gói tin thành mảng byte UTF-8 JSON (chưa gồm 4 byte header).
    /// </summary>
    public byte[] ToPayloadBytes()
    {
        return JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);
    }

    /// <summary>
    /// Đóng gói theo chuẩn Length-prefix: [4 byte Big-Endian độ dài] + [Payload bytes] (MOD_02_01).
    /// </summary>
    public byte[] ToFramedBytes()
    {
        byte[] payload = ToPayloadBytes();
        if (payload.Length > ProtocolConstants.MaxPacketPayloadBytes)
        {
            throw new InvalidOperationException(
                $"Kích thước gói tin ({payload.Length} bytes) vượt quá giới hạn {ProtocolConstants.MaxPacketPayloadBytes} bytes.");
        }

        byte[] frame = new byte[ProtocolConstants.LengthPrefixSizeBytes + payload.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, ProtocolConstants.LengthPrefixSizeBytes), payload.Length);
        Buffer.BlockCopy(payload, 0, frame, ProtocolConstants.LengthPrefixSizeBytes, payload.Length);
        return frame;
    }

    /// <summary>
    /// Giải mã từ mảng payload bytes (sau khi đã tách 4 byte Length-prefix).
    /// </summary>
    public static NetworkPacket FromPayloadBytes(ReadOnlySpan<byte> payloadBytes)
    {
        var packet = JsonSerializer.Deserialize<NetworkPacket>(payloadBytes, JsonOptions);
        return packet ?? throw new InvalidDataException("Không thể giải mã NetworkPacket từ payload bytes.");
    }

    /// <summary>
    /// Ghi bất đồng bộ gói tin đã đóng khung Length-prefix xuống NetworkStream.
    /// </summary>
    public async Task WriteFramedAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] framed = ToFramedBytes();
        await stream.WriteAsync(framed, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Đọc bất đồng bộ đúng 1 gói tin theo chuẩn Length-prefix từ NetworkStream.
    /// Trả về null nếu kết nối đã đóng sạch (EOF trước khi đọc byte header đầu tiên).
    /// </summary>
    public static async Task<NetworkPacket?> ReadFramedAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] lengthBuffer = new byte[ProtocolConstants.LengthPrefixSizeBytes];
        int headerRead = await ReadAtMostOrExactHeaderAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
        if (headerRead == 0)
        {
            return null; // Stream closed cleanly
        }

        int payloadLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
        if (payloadLength <= 0 || payloadLength > ProtocolConstants.MaxPacketPayloadBytes)
        {
            throw new InvalidDataException($"Độ dài gói tin không hợp lệ: {payloadLength} bytes.");
        }

        byte[] payloadBuffer = new byte[payloadLength];
        await ReadExactAsync(stream, payloadBuffer, cancellationToken).ConfigureAwait(false);
        return FromPayloadBytes(payloadBuffer);
    }

    private static async Task<int> ReadAtMostOrExactHeaderAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                if (totalRead == 0)
                {
                    return 0; // Clean EOF
                }

                throw new EndOfStreamException("Kết nối TCP bị ngắt giữa chừng khi đang đọc 4-byte length prefix.");
            }

            totalRead += read;
        }

        return totalRead;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    $"Kết nối TCP bị ngắt giữa chừng khi đang đọc payload ({totalRead}/{buffer.Length} bytes).");
            }

            totalRead += read;
        }
    }
}

