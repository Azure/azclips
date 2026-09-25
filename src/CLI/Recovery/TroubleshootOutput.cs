// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Core.Abstractions.Recovery;
using Core.Cli.Output;
using Core.Cli.Parser;

namespace Core.Cli.Recovery;

internal static class TroubleshootOutput
{
    internal sealed record Entry(string Id, string Command, string ErrorDetail);

    internal static Entry[] List(IReadOnlyList<FailureRecord> records) => records
        .OrderBy(record => record.TerminalSessionId)
        .ThenByDescending(record => record.OccurredAtUtc)
        .ThenByDescending(record => record.Sequence)
        .Select(record => Detail(record) with { ErrorDetail = SummarizeError(record.ErrorText) })
        .ToArray();

    internal static Entry Detail(FailureRecord record) => new(record.Id, record.FailedCommand, record.ErrorText);

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

    internal static string FormatDetail(
        FailureRecord record, OutputFormat format, OutputFormat simpleFormat,
        bool color, string? query, int? terminalWidth = null)
    {
        var entry = Detail(record);
        var json = CliOutputFormatter.FormatValue(entry, OutputFormat.Json, query: query);
        if (CliOutputFormatter.SelectFormat(json, format, simpleFormat) != OutputFormat.Table)
            return CliOutputFormatter.FormatValue(entry, format, simpleFormat, color, query, terminalWidth);

        // Tables flatten multiline cells even without a width limit. Only show uses a
        // vertical, lossless view; list and all other commands retain normal tables.
        using var document = JsonDocument.Parse(json);
        return FormatDetailFields(document.RootElement);
    }

    private static string FormatDetailFields(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => string.Join(Environment.NewLine,
            value.EnumerateObject().Select(property => $"{property.Name}:{Environment.NewLine}{FormatDetailFields(property.Value)}")),
        JsonValueKind.Array => string.Join(Environment.NewLine, value.EnumerateArray().Select(FormatDetailFields)),
        JsonValueKind.String => value.GetString()!,
        _ => value.GetRawText(),
    };
}
