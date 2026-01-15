namespace Core.Runtime.Provider
{
    using Core.Abstractions;
    using Core.Abstractions.Runtime;
    using Core.Abstractions.Runtime.Provider;
    using System;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implementation of ICommandAuthProvider.
    /// </summary>
    public class CommandAuthProvider : ICommandAuthProvider
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
            AcquireTokenAsyncDelegate tokenAcquirer = this.ApplyOverride && this.AcquireTokenAsyncOverride != null
                ? this.AcquireTokenAsyncOverride
                : this.AcquireTokenAsyncDefault;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenAcquirer(null, this.AuthContext.TenantId, false));
            return await next.SendAsync(request, callback);
        };
    }
}