namespace Core.Runtime.Handler
{
    using Core.Abstractions;
    using Core.Abstractions.Runtime;
    using Core.Abstractions.Runtime.Handler;
    using System.Net.Http.Headers;

    /// <summary>
    /// Implementation of ICommandAuthHandler.
    /// </summary>
    public class CommandAuthHandler : ICommandAuthHandler
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
            using (var _ = callback.StartActivity?.Invoke($"{nameof(CommandAuthHandler)}.{nameof(SendAsync)}"))
            {
                AcquireTokenAsyncDelegate tokenAcquirer = this.ApplyOverride && this.AcquireTokenAsyncOverride != null
                    ? this.AcquireTokenAsyncOverride
                    : this.AcquireTokenAsyncDefault;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", (await tokenAcquirer(null, this.AuthContext.TenantId, false)).AccessToken);   
            }
            return await next.SendAsync(request, callback);
        };
    }
}