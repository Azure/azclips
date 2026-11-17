// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Cryptography;

namespace Core.Abstractions.Recovery;

/// <summary>A failure ID: the parent-shell PID and 8 random lowercase hex digits, such as 48212-cfd9ddb4.</summary>
public readonly record struct FailureId(uint TerminalSessionId, string Suffix)
{
    private const int SuffixLength = 8;

    public static string Create(uint terminalSessionId)
    {
        ArgumentOutOfRangeException.ThrowIfZero(terminalSessionId);
        return string.Create(CultureInfo.InvariantCulture,
            $"{terminalSessionId}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(SuffixLength / 2))}");
    }

    public static bool TryParse(string? value, out FailureId id)
    {
        id = default;
        if (value is null)
            return false;

        var parts = value.Split('-');
        if (parts.Length != 2 || parts[1].Length != SuffixLength || !parts[1].All(char.IsAsciiHexDigitLower) ||
            !uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid == 0 ||
            parts[0] != pid.ToString(CultureInfo.InvariantCulture))
        {
            return false;
        }

        id = new FailureId(pid, parts[1]);
        return true;
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{TerminalSessionId}-{Suffix}");
}
