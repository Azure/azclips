using Core.Abstractions.Runtime;

namespace Core.Abstractions;

public interface IEngine : IDisposable
{
    /// <summary>
    /// Processes a command invocation synchronously on the caller's thread.
    /// </summary>
    /// <remarks>
    /// <para><b>Why synchronous (sync-over-async by design):</b>
    /// PowerShell cmdlets (e.g., <c>$PSCmdlet.WriteObject</c>) have strict thread-affinity:
    /// they must be called on the same thread that entered the <c>ProcessRecord</c> pipeline.
    /// An <c>async Task ProcessAsync</c> signature would allow continuations to resume on
    /// an arbitrary thread-pool thread, violating this invariant and causing
    /// <c>PSInvalidOperationException</c> at runtime.</para>
    ///
    /// <para>The implementation offloads real async work (HTTP, token acquisition) to a
    /// worker task, pipes results through a <see cref="System.Threading.Channels.Channel{T}"/>,
    /// and pumps the channel on the caller's (runspace-affine) thread so that the
    /// <paramref name="outputDelegate"/> always executes in the correct synchronization context.</para>
    ///
    /// <para>This is the accepted pattern for bridging async work into PowerShell's
    /// synchronous pipeline. See:
    /// https://learn.microsoft.com/en-us/powershell/scripting/developer/cmdlet/cmdlet-overview
    /// </para>
    /// </remarks>
    /// <param name="invocation">The command invocation to process.</param>
    /// <param name="outputDelegate">The output delegate for this specific invocation.
    /// Must be called on the caller's thread (PowerShell runspace thread).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    void Process(CommandInvocation invocation, CommandOutputDelegate outputDelegate, CancellationToken cancellationToken = default);
}

public interface ICommandEngine
{
    /// <summary>
    /// Gets the last extracted PowerShell command (if applicable)
    /// </summary>
    string? ExtractedCommand { get; }

    Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default);
}