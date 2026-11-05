// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace  Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Hand-authored auth hook for <see cref="StorageBlobProcessorBase{TRequest, TResponse}"/>. Implements the
    /// codegen-declared <c>OverrideBeforeProcessBase</c> seam so StorageBlob processors acquire tokens for
    /// the StorageBlob audience instead of the default ARM scope.
    /// </summary>
    /// <remarks>
    /// Lives under <c>Customization/</c> so codegen won't overwrite it. Delete once Core provides official
    /// data-plane auth, after which StorageBlob atomics use the engine default token acquisition.
    /// </remarks>
    public abstract partial class ProcessorBase<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Installs an <see cref="IAuthHandler.AcquireTokenAsyncOverride"/> forcing
        /// <see cref="StorageBlobProcessorBase{TRequest, TResponse}.StorageScopes"/>, wrapping the engine default delegate so only the token
        /// scope changes. Runs at the start of every <c>ProcessAsync</c> via <c>BeforeProcessAsync</c>.
        /// </summary>
        partial void OverrideBeforeProcessBase()
        {
            AcquireTokenAsyncDelegate acquireWithDefaultScope = AuthHandler.AcquireTokenAsyncDefault;

            Task<AuthResult> AcquireWithStorageBlobScope(string[]? _, string? tenantId, bool promptOnError, CancellationToken cancellationToken, Profile? profile) =>
                acquireWithDefaultScope(this.AuthScopes, tenantId, promptOnError, cancellationToken, profile);

            AuthHandler.AcquireTokenAsyncOverride = AcquireWithStorageBlobScope;
            AuthHandler.ApplyOverride = true;
        }
    }
}