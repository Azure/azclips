using System.Threading.Channels;
using GitHub.Copilot;

namespace Core.Agent.Hosting;

/// <summary>
/// SDK <c>OnPermissionRequest</c> callback runs on a background dispatch thread
/// owned by the daemon. Calling <see cref="Console.ReadLine"/> there races
/// against any other reader the host loop might run, and on Windows the global
/// stdin reader can return EOF instead of blocking when two threads contend.
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
    private readonly SpinnerService? _spinner;
    private readonly IAgentRenderer? _renderer;
    private readonly AbortKeyWatcher? _abortWatcher;

    public ConsoleApprovalPrompt(
        SpinnerService? spinner = null,
        IAgentRenderer? renderer = null,
        AbortKeyWatcher? abortWatcher = null)
    {
        _spinner = spinner;
        _renderer = renderer;
        _abortWatcher = abortWatcher;
    }

    private readonly Channel<PendingApproval> _queue =
        Channel.CreateUnbounded<PendingApproval>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    private record PendingApproval(
        string ToolName,
        string ArgsPreview,
        string? PlanMarkdown,
        TaskCompletionSource<bool> Result,
        CancellationTokenRegistration Reg);

    // Outcome of one approval prompt. When Approved=false and FeedbackPrompt is
    // set, the user typed free-form text at the y/n prompt — the host treats it
    // as a rejection of the current tool plus the next chat-loop input.
    public sealed record ApprovalOutcome(bool Handled, bool Approved, string? FeedbackPrompt)
    {
        public static readonly ApprovalOutcome NotHandled = new(false, false, null);
        public static readonly ApprovalOutcome Yes = new(true, true, null);
        public static readonly ApprovalOutcome No = new(true, false, null);
        public static ApprovalOutcome Redirect(string feedback) => new(true, false, feedback);
    }

    /// <summary>
    /// Called from the SDK background callback. Returns a task that completes
    /// when the host pumps the next request and the user answers.
    /// </summary>
    public Task<bool> RequestAsync(PermissionRequest req, CancellationToken ct)
    {
        var (toolName, args) = Describe(req);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        _queue.Writer.TryWrite(new PendingApproval(toolName, args, null, tcs, reg));
        return tcs.Task;
    }

    /// <summary>
    /// Approval path for OnPreToolUse rewrites. SDK semantics force the hook to
    /// own approval when it modifies args ("ask" discards modifiedArgs), so the
    /// hook routes through this method instead of OnPermissionRequest.
    /// </summary>
    public Task<bool> RequestRewriteAsync(string toolName, string rewrittenCommand, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        _queue.Writer.TryWrite(new PendingApproval(toolName, rewrittenCommand ?? string.Empty, null, tcs, reg));
        return tcs.Task;
    }

    /// <summary>
    /// Stage-1 plan approval. The hook intercepts propose_plan and routes here to
    /// render the markdown plan and read y/n — same channel + pump + three-outcome
    /// machinery as the other approval paths, only the rendered body differs.
    /// </summary>
    public Task<bool> RequestPlanAsync(string planMarkdown, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reg = ct.Register(() => tcs.TrySetCanceled(ct));

        _queue.Writer.TryWrite(new PendingApproval("propose_plan", string.Empty, planMarkdown ?? string.Empty, tcs, reg));
        return tcs.Task;
    }

    /// <summary>
    /// Maps each <see cref="PermissionRequest"/> subtype to a (label, args)
    /// pair the user can read. The fallback returns <see cref="PermissionRequest.Kind"/>
    /// so when the SDK adds a new subtype we still see a recognizable tool name
    /// in the approval prompt — and know exactly which case to add here.
    /// </summary>
    private static (string ToolName, string Args) Describe(PermissionRequest req) => req switch
    {
        PermissionRequestCustomTool c => (c.ToolName,                              c.Args?.ToString() ?? string.Empty),
        PermissionRequestShell s      => ("shell",                                 s.FullCommandText ?? string.Empty),
        PermissionRequestRead r       => ("read",                                  r.Path ?? string.Empty),
        PermissionRequestWrite w      => ("write",                                 w.FileName ?? string.Empty),
        PermissionRequestUrl u        => ("web",                                   u.Url ?? string.Empty),
        PermissionRequestMcp m        => ($"mcp:{m.ServerName}/{m.ToolName}",      m.Args?.ToString() ?? string.Empty),
        PermissionRequestMemory mem   => ($"memory:{mem.Action}",                  $"{mem.Subject}: {mem.Fact}"),
        PermissionRequestHook h       => ($"hook:{h.ToolName}",                    h.ToolArgs?.ToString() ?? string.Empty),
        _                              => (req.Kind ?? "tool",                     string.Empty),
    };

    /// <summary>
    /// Synchronously consumes one pending request (if any) on the calling
    /// thread, writes the prompt + reads y/n from stdin. Designed to be raced
    /// against <c>SendAndWaitAsync</c> via <see cref="Task.WhenAny(Task, Task)"/>
    /// so the host's main thread can pump approvals during a busy turn without
    /// spawning a second stdin reader.
    /// </summary>
    /// <returns>
    /// <see cref="ApprovalOutcome.NotHandled"/> on cancellation; otherwise
    /// <see cref="ApprovalOutcome.Yes"/> / <see cref="ApprovalOutcome.No"/>, or
    /// <see cref="ApprovalOutcome.Redirect(string)"/> when the user typed free
    /// text at the prompt instead of y/n. The SDK tool call is already settled
    /// (true for Yes, false for No/Redirect) before this method returns; the
    /// caller only needs to act on the FeedbackPrompt.
    /// </returns>
    public async Task<ApprovalOutcome> PumpOneAsync(CancellationToken ct)
    {
        PendingApproval pending;
        try
        {
            pending = await _queue.Reader.ReadAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ApprovalOutcome.NotHandled;
        }

        _spinner?.Pause();
        _abortWatcher?.Pause();
        string? line = null;
        try
        {
            Action body = () =>
            {
                Console.WriteLine();
                if (pending.PlanMarkdown is not null)
                {
                    RenderPlan(pending.PlanMarkdown);
                    WriteColorInline(ConsoleColor.DarkGray, "   approve plan? [y]es / [n]o, tell me what to do differently: ");
                }
                else
                {
                    WriteColor(ConsoleColor.Yellow, $"⚠ Approve '{pending.ToolName}'?");
                    WriteArgs(pending.ArgsPreview);
                    WriteColorInline(ConsoleColor.DarkGray, "   [y]es / [n]o, tell me what to do differently: ");
                }
                line = Console.ReadLine();
            };
            if (_renderer is not null) _renderer.RunExclusive(body);
            else                       body();
        }
        finally
        {
            _abortWatcher?.Resume();
            _spinner?.Resume();
            pending.Reg.Dispose();
        }

        var trimmed = line?.Trim() ?? string.Empty;
        if (trimmed.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            pending.Result.TrySetResult(true);
            return ApprovalOutcome.Yes;
        }

        // Inverse match: anything that isn't y/yes counts as No. Empty / n / no
        // is a plain rejection; anything else is a rejection PLUS a feedback
        // prompt the host should feed back into the next chat turn.
        pending.Result.TrySetResult(false);
        if (trimmed.Length == 0 ||
            trimmed.Equals("n", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("no", StringComparison.OrdinalIgnoreCase))
            return ApprovalOutcome.No;

        return ApprovalOutcome.Redirect(trimmed);
    }

    // Stage-1 plan rendering. Commit #4 prints the markdown verbatim under a
    // header; the Options/steps layout (decision 8) refines this in a later
    // change. REVIEW: raw markdown in a non-UTF-8 PowerShell host may show
    // mojibake for any non-ASCII the model emits — verify in e2e, the prompts
    // already steer the model toward ASCII output.
    private static void RenderPlan(string planMarkdown)
    {
        WriteColor(ConsoleColor.Cyan, "🤖 Proposed plan:");
        Console.WriteLine();
        foreach (var lineText in planMarkdown.Replace("\r\n", "\n").Split('\n'))
            Console.WriteLine($"   {lineText}");
        Console.WriteLine();
    }

    // Soft-wrap long args across terminal width. Never truncates — the user
    // needs to see the FULL command (passwords, unusual flags hide at the
    // tail). Continuation lines are indented to align under the first arg
    // char so the visual block reads as one value.
    private const string ArgsLabel    = "   args: ";
    private const string ArgsContinue = "         ";
    private static void WriteArgs(string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            Console.WriteLine(ArgsLabel);
            return;
        }
        int width = TryGetWindowWidth();
        int budget = Math.Max(20, width - ArgsLabel.Length);
        bool first = true;
        for (int i = 0; i < args.Length; i += budget)
        {
            int take = Math.Min(budget, args.Length - i);
            Console.Write(first ? ArgsLabel : ArgsContinue);
            Console.WriteLine(args.AsSpan(i, take));
            first = false;
        }
    }

    private static int TryGetWindowWidth()
    {
        try
        {
            int w = Console.WindowWidth;
            return w > 0 ? w : 120;
        }
        catch
        {
            return 120; // redirected stdout / non-TTY
        }
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

    private static void WriteColorInline(ConsoleColor color, string text)
    {
        var prev = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.Write(text);
        }
        finally
        {
            Console.ForegroundColor = prev;
        }
    }
}
