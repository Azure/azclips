using Core.Abstractions;
using Core.Agent;
using Core.Agent.Hosting;
using Core.Agent.Models;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Tui.AgentMode;

/// <summary>
/// TUI-process entry point for "agent mode" — the path the classic CLI hands
/// off to via <c>Process.Start("Core.Tui.exe", "--agent-mode")</c> when a
/// classic invocation failed. Owns:
///
/// - parsing <c>--failed-command</c> / <c>--error</c> args,
/// - the banner,
/// - constructing <see cref="CirrusChatAgent"/> with the shared
///   <c>Core.Agent.Hosting</c> renderer / approval / process-executor,
/// - the read-eval chat loop (so stdin has exactly one reader and the SDK
///   approval callback pumps onto the same thread via
///   <see cref="ConsoleApprovalPrompt.PumpOneAsync"/>).
/// </summary>
public static class AgentModeEntry
{
    public static async Task<int> RunAsync(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var failedCommand = ArgValue(args, "--failed-command") ?? string.Empty;
        var errorText     = ArgValue(args, "--error")          ?? string.Empty;

        // Minimal DI — we only need IAuthService for the agent's cache tools.
        CirrusServiceProvider.Initialize(ClientType.CLI);

        try
        {
            PrintBanner(failedCommand, errorText);

            var auth     = CirrusServiceProvider.GetRequiredService<IAuthService>();
            var flavor   = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";
            var azv2     = FindAzv2Exe() ?? "azv2";
            var renderer = new ConsoleEventRenderer();
            var approval = new ConsoleApprovalPrompt();
            var executor = new ProcessExecutor(azv2, rewriteCommand: StripLeadingAzv2);

            await using var agent = await CirrusChatAgent.CreateAsync(
                flavor: flavor,
                auth: auth,
                runCommand: executor.RunAsync,
                approvalSink: approval,
                renderer: renderer,
                mode: "azure",
                ct: cts.Token);

            var firstPrompt = string.IsNullOrWhiteSpace(errorText)
                ? $"I tried `{failedCommand}` but it failed. Help me fix it."
                : $"I tried `{failedCommand}` but it failed:\n{errorText}\nHelp me fix it.";

            await RunChatLoopAsync(agent, approval, firstPrompt, cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 130; // POSIX convention for SIGINT
        }
        catch (Exception ex)
        {
            WriteColor(ConsoleColor.Red, $"agent mode error: {ex.Message}");
            return 1;
        }
        finally
        {
            CirrusServiceProvider.Dispose();
        }
    }

    /// <summary>
    /// Host-owned chat loop. Reads one line of stdin, sends to the agent, and
    /// while the agent's turn is in flight pumps any approval prompts on the
    /// same thread — so stdin has exactly one reader at a time.
    /// </summary>
    private static async Task RunChatLoopAsync(
        CirrusChatAgent agent,
        ConsoleApprovalPrompt approval,
        string firstPrompt,
        CancellationToken ct)
    {
        await SendAndPumpAsync(agent, approval, firstPrompt, ct);

        while (!ct.IsCancellationRequested)
        {
            Console.WriteLine();
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line is null) break;
            line = line.Trim();
            if (line.Length == 0) continue;
            if (line is "exit" or "quit" or "/exit" or "/quit" or ":q") break;

            await SendAndPumpAsync(agent, approval, line, ct);
        }
    }

    private static async Task SendAndPumpAsync(
        CirrusChatAgent agent,
        ConsoleApprovalPrompt approval,
        string prompt,
        CancellationToken ct)
    {
        var sendTask = agent.SendAsync(prompt, ct);

        while (!sendTask.IsCompleted)
        {
            using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pumpTask = approval.PumpOneAsync(pumpCts.Token);
            var completed = await Task.WhenAny(sendTask, pumpTask).ConfigureAwait(false);
            if (completed == sendTask)
            {
                pumpCts.Cancel(); // unblock the pending pump read
                try { await pumpTask.ConfigureAwait(false); } catch { /* cancelled */ }
                break;
            }
            // else: pump consumed one approval; loop and keep pumping until send completes.
        }

        await sendTask.ConfigureAwait(false); // surface exceptions
    }

    private static void PrintBanner(string failedCommand, string error)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine("🤖 agent mode");
        if (!string.IsNullOrWhiteSpace(failedCommand))
            Console.WriteLine($"   triggered by failed command: {failedCommand}");
        if (!string.IsNullOrWhiteSpace(error))
            Console.WriteLine($"   error: {Truncate(error, 200)}");
        Console.WriteLine("   type 'exit' or Ctrl+C to leave");
        Console.WriteLine();
        Console.ForegroundColor = prev;
    }

    private static string? ArgValue(string[] args, string flag)
    {
        var idx = Array.IndexOf(args, flag);
        if (idx < 0 || idx + 1 >= args.Length) return null;
        return args[idx + 1];
    }

    private static string StripLeadingAzv2(string commandLine)
    {
        var trimmed = commandLine.TrimStart();
        var firstSpace = trimmed.IndexOf(' ');
        var firstToken = firstSpace < 0 ? trimmed : trimmed[..firstSpace];
        return firstToken.Equals("azv2", StringComparison.OrdinalIgnoreCase)
            ? trimmed[Math.Min(trimmed.Length, firstToken.Length + 1)..]
            : trimmed;
    }

    private static string? FindAzv2Exe()
    {
        var baseDir = AppContext.BaseDirectory;
        var name = OperatingSystem.IsWindows() ? "azv2.exe" : "azv2";
        foreach (var candidate in new[]
        {
            Path.Combine(baseDir, name),
            Path.Combine(baseDir, "..", "Core.Cli", name),
        })
        {
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        return null;
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

    private static string Truncate(string s, int max) =>
        s.Length > max ? s[..max] + "…" : s;
}
