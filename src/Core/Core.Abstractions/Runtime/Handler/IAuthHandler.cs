// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime.Handler
{
    /// <summary>
    /// Interface for authentication handlers.
    /// </summary>
    public interface IAuthHandler
    {
        /// <summary>
        /// The resolved profile providing connection state (tenant, subscription) for token
        /// acquisition. Null when no profile is available; token acquisition is skipped in that case.
        /// </summary>
        Profile? Profile { get; set; }

        /// <summary>
        /// Acquires an access token. Implementation injected at runtime.
        /// </summary>
        AcquireTokenAsyncDelegate AcquireTokenAsyncDefault { get; set; }

        /// <summary>
        /// Gets or sets the token acquisition delegate. 
        /// Setting null reverts to engine default.
        /// </summary>
        AcquireTokenAsyncDelegate? AcquireTokenAsyncOverride { get; set; }

        /// <summary>
        /// Gets or sets whether to use <see cref="AcquireTokenAsyncOverride"/> instead of the engine default.
        /// When true, <see cref="AcquireTokenAsyncDefault"/> will invoke the override delegate if set.
        /// When false, the engine's default token acquisition is used regardless of override.
        /// </summary>
        bool ApplyOverride { get; set; }

        /// <summary>
        /// Optional per-request auth provider. When set, the auth handler consults it
        /// before each request to decide whether to skip auth, use a custom token, or
        /// fall through to the default token acquisition.
        /// </summary>
        IRequestAuthProvider? RequestAuthProvider { get; set; }

        /// <summary>
        /// Sends an HTTP request step with authentication.
        /// </summary>
        SendAsyncStep SendAsync { get; }
    }
}
