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
        public AuthContext AuthContext { get; set; } = null!;

        /// <inheritdoc />
        public AcquireTokenAsyncDelegate AcquireTokenAsyncDefault { get; set; } = null!;

        /// <inheritdoc />
        public AcquireTokenAsyncDelegate? AcquireTokenAsyncOverride { get; set; }

        /// <inheritdoc />
        public bool ApplyOverride { get; set; } = false;

        /// <inheritdoc />
        public SendAsyncStep SendAsync => async (HttpRequestMessage request, IEventListener callback, ISendAsync next) =>
        {
            await callback.Signal(Events.AuthBeforeTokenAcquisition);
            AcquireTokenAsyncDelegate tokenAcquirer = this.ApplyOverride && this.AcquireTokenAsyncOverride != null
                    ? this.AcquireTokenAsyncOverride
                    : this.AcquireTokenAsyncDefault;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", (await tokenAcquirer(null, this.AuthContext.TenantId, false)).AccessToken);
            await callback.Signal(Events.AuthAfterTokenAcquisition);
            return await next.SendAsync(request, callback);
        };
    }
}