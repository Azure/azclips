// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Hand-authored auth hook for <see cref="GraphProcessorBase{TRequest, TResponse}"/>. Implements the
    /// codegen-declared <c>OverrideBeforeProcessBase</c> seam so Graph processors acquire tokens for
    /// the Microsoft Graph audience instead of the default ARM scope.
    /// </summary>
    /// <remarks>
    /// Lives under <c>Customization/</c> so codegen won't overwrite it. Delete once Core provides official
    /// data-plane auth, after which Graph atomics use the engine default token acquisition.
    /// </remarks>
    public abstract partial class GraphProcessorBase<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Installs an <see cref="IAuthHandler.AcquireTokenAsyncOverride"/> forcing
        /// <see cref="GraphConstants.GraphScopes"/>, wrapping the engine default delegate so only the token
        /// scope changes. Runs at the start of every <c>ProcessAsync</c> via <c>BeforeProcessAsync</c>.
        /// </summary>
        partial void OverrideBeforeProcessBase()
        {
            AcquireTokenAsyncDelegate acquireWithDefaultScope = AuthHandler.AcquireTokenAsyncDefault;

            Task<AuthResult> AcquireWithGraphScope(string[]? _, string? tenantId, bool promptOnError, CancellationToken cancellationToken, Profile? profile) =>
                acquireWithDefaultScope(GraphConstants.GraphScopes, tenantId, promptOnError, cancellationToken, profile);

            AuthHandler.AcquireTokenAsyncOverride = AcquireWithGraphScope;
            AuthHandler.ApplyOverride = true;
        }
    }
}
