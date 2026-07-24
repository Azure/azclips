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
        public IRequestAuthProvider? RequestAuthProvider { get; set; }

        /// <inheritdoc />
        public SendAsyncStep SendAsync => async (HttpRequestMessage request, IEventPublisher callback, ISendAsync next) =>
        {
            // Per-request auth override via IRequestAuthProvider
            if (this.RequestAuthProvider is not null)
            {
                var decision = await this.RequestAuthProvider.GetAuthAsync(request, default);
                switch (decision.Action)
                {
                    case RequestAuthAction.Skip:
                        return await next.SendAsync(request, callback);
                    case RequestAuthAction.UseToken:
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", decision.Token!);
                        return await next.SendAsync(request, callback);
                    // UseDefault: fall through to standard logic below
                }
            }

            // When no profile is resolved, acquire no token. Commands that require a profile
            // (and therefore an authenticated call) fail at point of use with a clear error.
            if (this.Profile is not null)
            {
                await callback.Publish(Events.AuthBeforeTokenAcquisition);
                AcquireTokenAsyncDelegate tokenAcquirer = this.ApplyOverride && this.AcquireTokenAsyncOverride != null
                        ? this.AcquireTokenAsyncOverride
                        : this.AcquireTokenAsyncDefault;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", (await tokenAcquirer(null, this.Profile.TenantId, false, default, this.Profile)).AccessToken);
                await callback.Publish(Events.AuthAfterTokenAcquisition);
            }
            return await next.SendAsync(request, callback);
        };
    }
}