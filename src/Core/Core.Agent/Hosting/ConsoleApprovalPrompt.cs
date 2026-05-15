using System.Threading.Channels;
using GitHub.Copilot.SDK;

namespace Core.Agent.Hosting;

/// <summary>
/// SDK <c>OnPermissionRequest</c> callback runs on a background dispatch thread
/// owned by the daemon. Calling <see cref="Console.ReadLine"/> there races
/// against any other reader the host loop might run, and on Windows the global
/// stdin reader can return EOF instead of blocking when two threads contend —
/// that's the "y/n: ▶ run_command" wedge the spike kept hitting.
///
/// This sink decouples the producer (SDK callback) from the consumer (host's
/// main thread): callbacks post a request + <see cref="TaskCompletionSource{TResult}"/>
/// onto an unbounded channel and await the answer. The host pumps the channel
/// on its own thread via <see cref="PumpOneAsync"/>, where it can safely write
/// the prompt and read the answer. Only the pump thread ever touches stdin,
/// so the stdin race goes away by construction.
///
/// Pattern:    background producer → channel → single-threaded consumer.
/// Avoid:      ❌ calling <see cref="Console.ReadLine"/> from the SDK callback
///                directly (the bug we're fixing);
///             ❌ blocking the SDK callback synchronously waiting on the host —
///                a single Channel + TaskCompletionSource lets the callback
///                await without occupying a daemon thread.
/// </summary>
public sealed class ConsoleApprovalPrompt
{
    private readonly Channel<PendingApproval> _queue =
        Channel.CreateUnbounded<PendingApproval>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    private record PendingApproval(string ToolName, string ArgsPreview, TaskCompletionSource<bool> Result);

    /// <summary>
    /// Called from the SDK background callback. Returns a task that completes
    /// when the host pumps the next request and the user answers.
    /// </summary>
    public Task<bool> RequestAsync(PermissionRequest req, CancellationToken ct)
    {
        var toolName = (req as PermissionRequestCustomTool)?.ToolName ?? "tool";
        var args     = (req as PermissionRequestCustomTool)?.Args?.ToString() ?? string.Empty;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        _queue.Writer.TryWrite(new PendingApproval(toolName, Trim(args, 200), tcs));
        return tcs.Task;
    }

    /// <summary>
    /// Synchronously consumes one pending request (if any) on the calling
    /// thread, writes the prompt + reads y/n from stdin. Designed to be raced
    /// against <c>SendAndWaitAsync</c> via <see cref="Task.WhenAny(Task, Task)"/>
    /// so the host's main thread can pump approvals during a busy turn without
    /// spawning a second stdin reader.
    /// </summary>
    /// <returns>true if an approval was handled; false on cancellation.</returns>
    public async Task<bool> PumpOneAsync(CancellationToken ct)
    {
        PendingApproval pending;
        try
        {
            pending = await _queue.Reader.ReadAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        Console.WriteLine();
        WriteColor(ConsoleColor.Yellow, $"⚠ Approve '{pending.ToolName}'?");
        WriteColor(ConsoleColor.DarkGray, $"   args: {pending.ArgsPreview}");
        Console.Write("   y/n: ");

        var line = Console.ReadLine();
        var approved = line is not null
            && (line.Trim().Equals("y", StringComparison.OrdinalIgnoreCase)
                || line.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase));

        pending.Result.TrySetResult(approved);
        return true;
    }

    private static void WriteColor(ConsoleColor color, string text)
    {
        var prev = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = prev;
        }
    }

    private static string Trim(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length > max ? s[..max] + "…" : s);
}
