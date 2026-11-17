// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Abstractions.Recovery;

public sealed record FailureRecord
{
    public string Id { get; init; } = string.Empty;
    public uint TerminalSessionId { get; init; }
    public ulong Sequence { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required string[] Arguments { get; init; }
    public required string FailedCommand { get; init; }
    public required string ErrorText { get; init; }
    public required int ExitCode { get; init; }
    public string? CommandName { get; init; }
    public string? ErrorCode { get; init; }
    public string? ExceptionType { get; init; }
    public JsonElement? ErrorDetails { get; init; }
    public int? HttpStatusCode { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? ProfileName { get; init; }
    public string? ClientVersion { get; init; }
    public string? CommandTraceId { get; init; }
    public string? CommandSpanId { get; init; }
}

// The local completion index must never contain command arguments or error text.
// KeyReplaced is set only by the save that replaced a damaged key; it is never stored.
public sealed record FailureHistoryEntry(
    string Id, DateTimeOffset OccurredAtUtc, bool Unencrypted = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool KeyReplaced = false);
