// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

namespace Core.Abstractions.Recovery;

public readonly record struct FailureId(uint TerminalSessionId, DateTimeOffset OccurredAtUtc, ulong Sequence)
{
    public static string Format(uint terminalSessionId, DateTimeOffset occurredAtUtc, ulong sequence)
    {
        ArgumentOutOfRangeException.ThrowIfZero(terminalSessionId);
        return string.Create(CultureInfo.InvariantCulture,
            $"{terminalSessionId}-{occurredAtUtc.UtcDateTime:yyyy-MMdd-HHmm}-{sequence:D4}");
    }

    public static bool TryParse(string? value, out FailureId id)
    {
        id = default;
        if (value is null || value.Length > 46)
            return false;

        var parts = value.Split('-');
        if (parts.Length != 5 || parts[1].Length != 4 || parts[2].Length != 4 ||
            parts[3].Length != 4 || parts[4].Length < 4 ||
            !uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid == 0 ||
            !ulong.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) ||
            !DateTimeOffset.TryParseExact(parts[1] + parts[2] + parts[3], "yyyyMMddHHmm",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp) ||
            !string.Equals(value, Format(pid, timestamp, sequence), StringComparison.Ordinal))
        {
            return false;
        }

        id = new FailureId(pid, timestamp, sequence);
        return true;
    }

    public override string ToString() => Format(TerminalSessionId, OccurredAtUtc, Sequence);
}
