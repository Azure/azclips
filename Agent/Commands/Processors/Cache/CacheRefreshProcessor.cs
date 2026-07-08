// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Resource.Cache;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors.Cache
{
    /// <summary>
    /// `az agent tool cache refresh` — synchronously rebuilds the on-disk
    /// ARM cache used by resource find/tree/show. The other resource commands
    /// kick a fire-and-forget refresh but return immediately with whatever is
    /// on disk (often empty on a cold start). This command exists so users
    /// and CI can wait for the cache to actually settle before querying.
    /// </summary>
    public class CacheRefreshProcessor : AgentProcessorBase
    {
        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            var auth = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();
            var httpClient = AzclipsServiceProvider.GetRequiredService<HttpClient>();
            var profile = await profileService.GetActiveProfileAsync(Token);

            var startedAt = DateTime.UtcNow;
            string status;
            string? errorMessage = null;

            if (profile is null || string.IsNullOrEmpty(profile.TenantId))
            {
                // Same fast-path as ArmCacheRefresher.RefreshOnceAsync — no
                // tenant means nothing to refresh. Surface it explicitly instead
                // of returning "ok" (which would tell the user the refresh
                // succeeded when it never ran).
                status = "not_signed_in";
                errorMessage = "No active profile — run `az login` first.";
            }
            else
            {
                // Reuse the DI-registered singleton HttpClient (matches
                // ResourceProcessorSetup / ResourceSkuProcessor) rather than
                // letting ArmCacheRefresher construct its own per invocation.
                var refresher = new ArmCacheRefresher(auth, profileService, httpClient,
                    progress: msg => Console.Error.WriteLine(msg));
                try
                {
                    // RefreshOnceAsync throws on failure — LastError is only
                    // populated by KickAsync (the fire-and-forget entry point).
                    // So this direct-await path only ever ends up in "ok" or
                    // the catch block below, never in a "partial" state.
                    await refresher.RefreshOnceAsync(Token);
                    status = "ok";
                }
                catch (Exception ex)
                {
                    status = "failed";
                    errorMessage = ex.Message;
                }
            }

            var elapsed = DateTime.UtcNow - startedAt;
            var json = JsonSerializer.Serialize(new
            {
                status,
                tenantId = profile?.TenantId,
                elapsedSeconds = Math.Round(elapsed.TotalSeconds, 2),
                error = errorMessage,
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false,
            });

            await WriteOutput(json);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
