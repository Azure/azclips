// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Agent.Tools.Resource.Cache;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource
{
    /// <summary>
    /// Shared setup for all `az agent tool resource ...` processors. Every one
    /// of them reads from the arm-cache singleton, which needs both a current
    /// tenant id (so it points at the right per-tenant subdirectory) and the
    /// background refresher (so calling one of these commands eventually
    /// populates a cold cache).
    /// </summary>
    internal static class ResourceProcessorSetup
    {
        /// <summary>
        /// Wires the ResourceCache singleton for a CLI-side processor call.
        /// Returns the caller-scoped tools instance ready to invoke.
        /// </summary>
        public static async Task<(CachedResourceTools tools, ArmCacheRefresher refresher)> InitializeAsync(CancellationToken ct)
        {
            var auth = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();

            var refresher = new ArmCacheRefresher(auth, profileService);
            var profile = await profileService.GetActiveProfileAsync(ct).ConfigureAwait(false);
            var pinnedTenantId = profile?.TenantId;
            ResourceCache.ConfigureSingleton(
                currentTenantId: () => pinnedTenantId,
                refresher: refresher);

            var armDetails = new ArmDetailsClient(auth);
            return (new CachedResourceTools(armDetails), refresher);
        }
    }
}
