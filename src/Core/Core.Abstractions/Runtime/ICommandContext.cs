using Core.Abstractions.Runtime.Provider;

namespace Core.Abstractions.Runtime
{
    /// <summary>
    /// The interface representing the context of a command execution.
    /// <para><b>Lifecycle:</b> A new instance is created for each command invocation.
    /// The engine creates and initializes the context but does not modify it thereafter.
    /// Extensions may modify context members (e.g., set provider overrides) during execution.
    /// Each command has isolated context; modifications do not affect other commands.</para>
    /// </summary>
    public interface ICommandContext
    {
        /// <summary>
        /// Gets the authentication provider for the command execution.
        /// Provides token acquisition and HTTP pipeline capabilities.
        /// Extensions can override <see cref="ICommandAuthProvider.AcquireTokenAsyncOverride"/> 
        /// before execution to customize authentication behavior.
        /// </summary>
        ICommandAuthProvider CommandAuthProvider { get; }

        /// <summary>
        /// Gets the HTTP pipeline for sending requests.
        /// </summary>
        IRuntimeHttpPipeline HttpPipeline { get; }

        /// <summary>
        /// Gets the event listener for logging and telemetry.
        /// </summary>
        // IEventListener? EventListener { get; }
    }
}