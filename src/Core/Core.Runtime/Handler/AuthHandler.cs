// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Runtime.Handler
{
    using Core.Abstractions;
    using Core.Abstractions.Runtime;
    using Core.Abstractions.Runtime.Handler;

    /// <summary>
    /// Compatibility adapter for processor overrides over the shared credential authentication step.
    /// </summary>
    public class AuthHandler : IAuthHandler
    {
        public Profile? Profile { get; set; }

        /// <inheritdoc />
        public AcquireTokenAsyncDelegate AcquireTokenAsyncDefault { get; set; } = null!;

        /// <inheritdoc />
        public AcquireTokenAsyncDelegate? AcquireTokenAsyncOverride { get; set; }

        /// <inheritdoc />
        public bool ApplyOverride { get; set; } = false;

        /// <inheritdoc />
        public SendAsyncStep SendAsync => async (HttpRequestMessage request, IEventPublisher callback, ISendAsync next) =>
        {
            // When no profile is resolved, acquire no token. Commands that require a profile
            // (and therefore an authenticated call) fail at point of use with a clear error.
            var profile = this.Profile;
            if (profile is not null)
            {
                AcquireTokenAsyncDelegate tokenAcquirer =
                    this.ApplyOverride && this.AcquireTokenAsyncOverride != null
                        ? this.AcquireTokenAsyncOverride
                        : this.AcquireTokenAsyncDefault;
                var authentication = new CredentialAuthHandler(
                    new ProfileCredential(profile, tokenAcquirer),
                    onAuthenticated: authenticatedProfile => this.Profile = authenticatedProfile);
                return await authentication.SendAsync(request, callback, next);
            }
            return await next.SendAsync(request, callback);
        };
    }
}