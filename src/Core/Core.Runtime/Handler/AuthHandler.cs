// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Runtime.Handler
{
    using Core.Abstractions;
    using Core.Abstractions.Runtime;
    using Core.Abstractions.Runtime.Handler;
    using System.Net.Http.Headers;

    /// <summary>
    /// Implementation of IAuthHandler.
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
            if (this.Profile is not null
                && !(request.Options.TryGetValue(
                    RuntimeHttpRequestOptions.DisableProfileAuthentication,
                    out bool disableProfileAuthentication)
                    && disableProfileAuthentication))
            {
                await callback.Publish(Events.AuthBeforeTokenAcquisition);
                AcquireTokenAsyncDelegate tokenAcquirer = this.ApplyOverride && this.AcquireTokenAsyncOverride != null
                        ? this.AcquireTokenAsyncOverride
                        : this.AcquireTokenAsyncDefault;
                request.Options.TryGetValue(RuntimeHttpRequestOptions.Scopes, out string[]? scopes);
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    (await tokenAcquirer(scopes, this.Profile.TenantId, false, callback.Token, this.Profile)).AccessToken);
                await callback.Publish(Events.AuthAfterTokenAcquisition);
            }
            return await next.SendAsync(request, callback);
        };
    }
}