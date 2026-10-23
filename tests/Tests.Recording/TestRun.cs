// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;

namespace Tests.Recording;

/// <summary>
/// Run-scoped resource naming for record mode. The suffix keeps simultaneous record runs from
/// colliding over fixed names; the sanitisers strip it again, so committed recordings and
/// playback keep the base name. Set <see cref="RunIdVariable"/> to tie the id to a pipeline run.
/// </summary>
public static class TestRun
{
    public const string RunIdVariable = "AZCLIPS_TEST_RUN_ID";

    private static readonly Lazy<string> s_runId = new(() =>
    {
        var seed = Environment.GetEnvironmentVariable(RunIdVariable);
        if (string.IsNullOrEmpty(seed)) seed = Guid.NewGuid().ToString();
        
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(seed)))[..8];
    });

    public static string RunId => s_runId.Value;

    public static string ScopedName(string baseName, RecorderMode mode)
        => mode == RecorderMode.Record ? $"{baseName}-t{RunId}" : baseName;
}
