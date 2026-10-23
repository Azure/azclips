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
            request.Options.TryGetValue(RequestAuthentication.OptionsKey, out var requestAuthentication);
            var profile = requestAuthentication?.Profile ?? this.Profile;
            if (profile is not null)
            {
                await callback.Publish(Events.AuthBeforeTokenAcquisition);
                AcquireTokenAsyncDelegate tokenAcquirer = requestAuthentication is null && this.ApplyOverride && this.AcquireTokenAsyncOverride != null
                        ? this.AcquireTokenAsyncOverride
                        : this.AcquireTokenAsyncDefault;
                var authResult = await tokenAcquirer(requestAuthentication?.Scopes, profile.TenantId, false,
                    requestAuthentication is null ? default : callback.Token, profile);
                profile = profile.WithRuntimeMetadata(authResult);
                if (requestAuthentication is null)
                    this.Profile = profile;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
                await callback.Publish(Events.AuthAfterTokenAcquisition, new EventData { Payload = profile });
            }
            return await next.SendAsync(request, callback);
        };
    }
}