// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

namespace Core.Abstractions.Recovery;

public static class FailureHistoryKeyNames
{
    public const string LegacyFileName = "master-key.v1.protected";
    private const string Prefix = "master-key.v2.";
    private const string Suffix = ".protected";

    public static string GetFileName(ulong generation) =>
        Prefix + generation.ToString(CultureInfo.InvariantCulture) + Suffix;

    public static bool IsGenerationFile(string fileName)
    {
        // The prefix and suffix share a dot, so a short name can match both without room for a generation.
        if (fileName.Length <= Prefix.Length + Suffix.Length ||
            !fileName.StartsWith(Prefix, StringComparison.Ordinal) || !fileName.EndsWith(Suffix, StringComparison.Ordinal))
            return false;
        return ulong.TryParse(fileName.AsSpan(Prefix.Length, fileName.Length - Prefix.Length - Suffix.Length),
                   NumberStyles.None, CultureInfo.InvariantCulture, out var generation) &&
               string.Equals(fileName, GetFileName(generation), StringComparison.Ordinal);
    }
}
