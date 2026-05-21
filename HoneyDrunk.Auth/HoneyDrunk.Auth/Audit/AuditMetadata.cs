using System.Text;
using System.Text.Json;

namespace HoneyDrunk.Auth.Audit;

internal static class AuditMetadata
{
    public const int MaxContextBytes = 4096;

    private const string TruncationSuffix = "...[truncated]";

    public static IReadOnlyDictionary<string, string> Cap(
        IReadOnlyDictionary<string, string> metadata,
        int maxBytes = MaxContextBytes)
    {
        var json = JsonSerializer.Serialize(metadata);
        if (Encoding.UTF8.GetByteCount(json) <= maxBytes)
        {
            return metadata;
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["context"] = TruncateUtf8(json, maxBytes),
            ["context.truncated"] = "true",
        };
    }

    public static string TruncateUtf8(string value, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(value);

        var suffixBytes = Encoding.UTF8.GetByteCount(TruncationSuffix);
        if (maxBytes <= suffixBytes)
        {
            return string.Empty;
        }

        var budget = maxBytes - suffixBytes;
        var usedBytes = 0;
        var builder = new StringBuilder(value.Length);

        foreach (var rune in value.EnumerateRunes())
        {
            var runeBytes = rune.Utf8SequenceLength;
            if (usedBytes + runeBytes > budget)
            {
                break;
            }

            builder.Append(rune);
            usedBytes += runeBytes;
        }

        builder.Append(TruncationSuffix);
        return builder.ToString();
    }
}
