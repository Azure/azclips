// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Core.Abstractions.Recovery;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors.Troubleshoot;

internal sealed record TroubleshootEntry(string Id, string Command, string ErrorDetail)
{
    internal static TroubleshootEntry[] List(IReadOnlyList<FailureRecord> records) => records
        .OrderBy(record => record.TerminalSessionId)
        .ThenByDescending(record => record.OccurredAtUtc)
        .ThenByDescending(record => record.Sequence)
        .Select(record => Detail(record) with { ErrorDetail = SummarizeError(record.ErrorText) })
        .ToArray();

    internal static TroubleshootEntry Detail(FailureRecord record) => new(record.Id, record.FailedCommand, record.ErrorText);

    internal static string SummarizeError(string error)
    {
        var summary = new StringBuilder(200);
        foreach (var rune in error.EnumerateRunes())
        {
            var next = Rune.IsWhiteSpace(rune) ? new Rune(' ') : rune;
            if (summary.Length + next.Utf16SequenceLength > 200)
                break;
            summary.Append(next);
        }
        return summary.ToString();
    }
}
