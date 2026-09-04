// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Recording;

/// <summary>
/// Ties a scenario's mode, recording file, and interceptors together. Playback loads the committed
/// recording (each <see cref="CreateInterceptor"/> replays it independently, so one recording drives
/// both surfaces) and <see cref="Save"/> is a no-op. Record captures against the live pipeline and
/// <see cref="Save"/> writes the file. Capture from a single surface.
/// </summary>
public sealed class RecordingSession
{
    private readonly IRequestMatcher? _matcher;
    private readonly RecordingSanitizers? _sanitizers;

    private RecordingSession(RecorderMode mode, string path, SessionRecord record, IRequestMatcher? matcher, RecordingSanitizers? sanitizers)
    {
        Mode = mode;
        Path = path;
        Record = record;
        _matcher = matcher;
        _sanitizers = sanitizers;
    }

    public RecorderMode Mode { get; }

    /// <summary>The recording file this session reads (playback) or writes (record).</summary>
    public string Path { get; }

    /// <summary>The recording: loaded from disk in playback, accumulated in record.</summary>
    public SessionRecord Record { get; }

    /// <summary>Opens the session with an explicit mode and file. Tests use the canonical layout helper in SessionRecordFiles.</summary>
    public static RecordingSession Open(RecorderMode mode, string path, IRequestMatcher? matcher = null, RecordingSanitizers? sanitizers = null)
    {
        var record = mode == RecorderMode.Playback ? SessionRecord.Load(path) : new SessionRecord();
        return new RecordingSession(mode, path, record, matcher, sanitizers);
    }

    /// <summary>
    /// A fresh interceptor over this session's recording. In playback each interceptor tracks its own
    /// consumption, so one per surface replays the whole recording; in record they share the one
    /// growing recording.
    /// </summary>
    public RecordPlaybackInterceptor CreateInterceptor()
        => new(Mode, Record, _matcher, _sanitizers);

    /// <summary>In record mode, writes the captured recording to <see cref="Path"/>. No-op in playback.</summary>
    public void Save()
    {
        if (Mode == RecorderMode.Record)
        {
            Record.Save(Path);
        }
    }
}
