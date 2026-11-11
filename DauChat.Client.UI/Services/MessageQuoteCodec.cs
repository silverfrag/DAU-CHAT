namespace DauChat.Client.UI.Services;

internal static class MessageQuoteCodec
{
    public const string RecalledMarker = "[[DAU_RECALLED]]";
    private const string Prefix = "[[REPLY|";
    private const string Suffix = "]]";

    public static string Encode(string senderName, string targetMessageContent, string replyBody)
    {
        var cleanSender = SanitizeField(senderName);
        if (string.IsNullOrEmpty(cleanSender)) cleanSender = "Thành viên";

        var plainTarget = ExtractPlainBody(targetMessageContent);
        var cleanSnippet = SanitizeField(plainTarget);
        if (cleanSnippet.Length > 88)
        {
            cleanSnippet = cleanSnippet[..88] + "...";
        }

        return $"{Prefix}{cleanSender}|{cleanSnippet}{Suffix}\n{replyBody}";
    }

    public static bool TryDecode(string? raw, out string replyAuthor, out string replySnippet, out string body)
    {
        replyAuthor = string.Empty;
        replySnippet = string.Empty;
        body = raw ?? string.Empty;

        if (string.IsNullOrEmpty(raw) || !raw.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var endIdx = raw.IndexOf(Suffix, Prefix.Length, StringComparison.Ordinal);
        if (endIdx < 0)
        {
            return false;
        }

        var header = raw[Prefix.Length..endIdx];
        var sepIdx = header.IndexOf('|');
        if (sepIdx < 0)
        {
            return false;
        }

        replyAuthor = header[..sepIdx].Trim();
        replySnippet = header[(sepIdx + 1)..].Trim();

        var after = raw[(endIdx + Suffix.Length)..];
        if (after.StartsWith("\r\n", StringComparison.Ordinal))
        {
            body = after[2..];
        }
        else if (after.StartsWith('\n'))
        {
            body = after[1..];
        }
        else
        {
            body = after.TrimStart();
        }

        return true;
    }

    public static bool IsRecalledRaw(string? raw)
        => string.Equals(raw, RecalledMarker, StringComparison.Ordinal);

    public static string ExtractPlainBody(string? raw)
    {
        if (IsRecalledRaw(raw)) return "Tin nhắn đã được thu hồi";
        return TryDecode(raw, out _, out _, out var body) ? body : (raw ?? string.Empty);
    }

    public static string FormatSidebarPreview(string? raw)
    {
        if (IsRecalledRaw(raw)) return "Tin nhắn đã được thu hồi";
        return TryDecode(raw, out _, out _, out var body) ? $"↩ {body}" : (raw ?? string.Empty);
    }

    private static string SanitizeField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Replace("|", "¦")
            .Replace(Suffix, "]")
            .Trim();
    }
}

