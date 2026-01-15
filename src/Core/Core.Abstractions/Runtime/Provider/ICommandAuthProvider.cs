namespace Core.Abstractions.Runtime.Provider
{
    /// <summary>
    /// Interface for authentication providers.
    /// </summary>
    public interface ICommandAuthProvider
    {
        AuthContext AuthContext { get; set; }

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
        /// When true, <see cref="AcquireTokenAsync"/> will invoke the override delegate if set.
        /// When false, the engine's default token acquisition is used regardless of override.
        /// </summary>
        bool ApplyOverride { get; set; }

        /// <summary>
        ///  Sends an HTTP request step with authentication.
        /// </summary>
        SendAsyncStep SendAsync { get; }
    }
}