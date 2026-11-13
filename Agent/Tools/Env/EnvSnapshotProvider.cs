// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;

namespace Core.Agent.Tools.Env;

/// <summary>Shared live profile/environment projection for CLI output and agent hooks.</summary>
public static class EnvSnapshotProvider
{
    private static readonly JsonSerializerOptions s_json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task<string> GetJsonAsync(
        IProfileService profileService,
        string azExePath,
        CancellationToken ct = default,
        Func<CancellationToken, Task<string>>? readCommandCatalog = null)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var prober = new EnvironmentProber(profileService, flavor: "cli", CliInstallation.GetVersion(azExePath), readCommandCatalog);
            var snapshot = await prober.ProbeAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Serialize(snapshot, s_json);
        }
        catch (Exception ex) when (IsExpectedReadFailure(ex))
        {
            ct.ThrowIfCancellationRequested();
            return "{\"error\":\"environment_snapshot_failed\",\"hint\":\"Could not read the active profile or CLI environment.\"}";
        }
    }

    private static bool IsExpectedReadFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException ||
        // ProfileService.LoadProfilesAsync wraps these file/JSON failures in
        // Exception. Do not hide a wrapped programming fault (or cancellation).
        (exception.GetType() == typeof(Exception) &&
         exception.InnerException is IOException or UnauthorizedAccessException or JsonException);
}
