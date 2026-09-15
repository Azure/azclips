// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Tests.Common.Utilities;

using Core.Recording;

namespace Tests.Common.Recording;

/// <summary>
/// Canonical on-disk layout for recordings:
/// <c>tests/&lt;TestProject&gt;/SessionRecords/&lt;TestClass&gt;/&lt;TestName&gt;.json</c>,
/// resolved from the repository root (via <see cref="RepoLocator"/>) rather than the test's
/// output directory, so record mode writes into the source tree where the file can be
/// committed, and playback reads that same committed file locally and in CI.
/// </summary>
public static class SessionRecordFiles
{
    public static string GetPath(Type testClass, string testName)
        => GetPath(RepoLocator.GetRepoRoot(), testClass, testName);

    public static string GetPath(string repoRoot, Type testClass, string testName)
        => Path.Combine(
            repoRoot,
            "tests",
            testClass.Assembly.GetName().Name!,
            "SessionRecords",
            testClass.Name,
            $"{testName}.json");

    /// <summary>
    /// Opens a <see cref="RecordingSession"/> for a test: mode from <see cref="RecorderModeResolver"/>,
    /// file from the canonical layout, scenario metadata stamped on record.
    /// </summary>
    public static RecordingSession OpenSession(Type testClass, string testName, IRequestMatcher? matcher = null, RecordingSanitizers? sanitizers = null)
    {
        var session = RecordingSession.Open(RecorderModeResolver.FromEnvironment(), GetPath(testClass, testName), matcher, sanitizers);
        if (session.Mode == RecorderMode.Record)
        {
            session.Record.Metadata["scenario"] = $"{testClass.Name}/{testName}";
        }
        return session;
    }
}
