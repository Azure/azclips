// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

using Tests.Common.Utilities;

using Tests.Recording;

namespace Tests.Common.Recording;

/// <summary>
/// Canonical on-disk layout for recordings: a <c>Records</c> folder next to the test,
/// <c>&lt;test source dir&gt;/Records/&lt;TestClass&gt;/&lt;TestName&gt;.json</c>. The directory comes
/// from the calling test's source location, so organising the tests organises the recordings,
/// and the root comes from <see cref="RepoLocator"/> rather than the test's output directory,
/// so record mode writes into the source tree where the file can be committed, and playback
/// reads that same committed file locally and in CI. Test class names must stay unique across
/// modules, or two tests would fight over one recording; the suite has a test for that.
/// </summary>
public static class SessionRecordFiles
{
    public static string GetPath(Type testClass, string testName, [CallerFilePath] string testSourcePath = "")
        => GetPath(RepoLocator.GetRepoRoot(), testClass, testName, testSourcePath);

    public static string GetPath(string repoRoot, Type testClass, string testName, [CallerFilePath] string testSourcePath = "")
        => Path.Combine(repoRoot, TestDirectory(testSourcePath), "Records", testClass.Name, $"{testName}.json");

    // The compiler stamps the caller's source path at build time; only the directory from
    // tests/ down is kept, so the path stays valid wherever the repo is checked out.
    private static string TestDirectory(string testSourcePath)
    {
        var normalized = testSourcePath.Replace('\\', '/');
        var marker = normalized.LastIndexOf("/tests/", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            throw new ArgumentException($"test source file is not under tests/: {testSourcePath}", nameof(testSourcePath));
        }
        return Path.GetDirectoryName(normalized[(marker + 1)..])!;
    }

    /// <summary>
    /// Opens a <see cref="RecordingSession"/> for a test: mode from <see cref="RecorderModeResolver"/>,
    /// file from the canonical layout, scenario metadata stamped on record.
    /// </summary>
    public static RecordingSession OpenSession(Type testClass, string testName, IRequestMatcher? matcher = null, RecordingSanitizers? sanitizers = null, [CallerFilePath] string testSourcePath = "")
    {
        var session = RecordingSession.Open(RecorderModeResolver.FromEnvironment(), GetPath(RepoLocator.GetRepoRoot(), testClass, testName, testSourcePath), matcher, sanitizers);
        if (session.Mode == RecorderMode.Record)
        {
            session.Record.Metadata["scenario"] = $"{testClass.Name}/{testName}";
        }
        return session;
    }
}
