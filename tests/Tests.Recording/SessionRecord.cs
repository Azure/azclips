// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tests.Recording;

/// <summary>
/// One recorded HTTP exchange: the request as it left the pipeline and the response the
/// service returned. Playback matches an outgoing request against <see cref="Request"/> and
/// serves <see cref="Response"/> without touching the network.
/// </summary>
public sealed class SessionRecordEntry
{
    public required RecordedRequest Request { get; init; }
    public required RecordedResponse Response { get; init; }
}

public sealed class RecordedRequest
{
    public required string Method { get; init; }
    public required string Uri { get; init; }
    public Dictionary<string, string[]> Headers { get; init; } = [];
    public string? Body { get; init; }
}

public sealed class RecordedResponse
{
    public required int StatusCode { get; init; }

    /// <summary>Response headers (e.g. <c>x-ms-request-id</c>, LRO polling headers).</summary>
    public Dictionary<string, string[]> Headers { get; init; } = [];

    /// <summary>Content headers (e.g. <c>Content-Type</c>), kept apart so the response can be rebuilt faithfully.</summary>
    public Dictionary<string, string[]> ContentHeaders { get; init; } = [];

    public string? Body { get; init; }
}

/// <summary>
/// The recorded HTTP session for one scenario: a version stamp, free-form metadata, and the
/// exchanges in send order. Serializes to the JSON file that lives at
/// <c>SessionRecords/&lt;TestClass&gt;/&lt;TestName&gt;.json</c> (see <see cref="SessionRecordFiles"/>).
/// Entry order is significant: playback consumes duplicate matches in recorded order, which is
/// what makes polling sequences replayable.
/// </summary>
public sealed class SessionRecord
{
    /// <summary>Schema version written by this code. Bump on breaking changes to the on-disk shape.</summary>
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int Version { get; init; } = CurrentVersion;

    /// <summary>
    /// Free-form annotations about the recording (scenario name, origin, refresh hints).
    /// Round-trips verbatim; nothing in record/playback interprets it.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = [];

    public List<SessionRecordEntry> Entries { get; init; } = [];

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, s_jsonOptions) + "\n");
    }

    public static SessionRecord Load(string path)
        => JsonSerializer.Deserialize<SessionRecord>(File.ReadAllText(path), s_jsonOptions)
           ?? throw new InvalidOperationException($"Session record '{path}' deserialized to null.");
}
