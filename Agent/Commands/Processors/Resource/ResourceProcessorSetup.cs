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
        public static (CachedResourceTools tools, ArmCacheRefresher refresher) Initialize()
        {
            var auth = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();

            var refresher = new ArmCacheRefresher(auth, profileService);
            // The tools push the active profile's tenant into ResourceCache on
            // every invocation (CachedResourceTools.SyncTenantAsync), so there's
            // nothing to pin here; wire only the background refresher.
            ResourceCache.ConfigureSingleton(
                currentTenantId: null,
                refresher: refresher);

            var armDetails = new ArmDetailsClient(auth);
            return (new CachedResourceTools(armDetails, profileService), refresher);
        }
    }
}
