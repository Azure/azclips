using Core.Tui.Rendering;
using Core.Tui.Services;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Core.Tui;

public static class Program
{
    public static async Task Main()
    {
        var state = new AppState();

        // Create the shell workload (handles prompt + line editing via ANSI)
        var shellWorkload = new CirrusShellWorkload(state, Console.WindowWidth, 1);

        // Build an embedded terminal backed by our shell workload
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(shellWorkload)
            .WithTerminalWidget(out var shellHandle)
            .Build();

        // Wire AppState to redraw the shell prompt when state changes
        state.Invalidate = () =>
        {
            shellWorkload.RefreshPrompt();
        };

        // Run the terminal in the background
        var terminalCts = new CancellationTokenSource();
        var terminalTask = terminal.RunAsync(terminalCts.Token);
        shellWorkload.Start();

        var theme = Hex1bThemes.Ocean.Clone("cirrus");

        var options = new Hex1bAppOptions { EnableMouse = true, Theme = theme };
        Hex1bApp? app = null;
        app = new Hex1bApp(ctx =>
        {
            // Keep focus on the terminal widget for keyboard input
            app?.RequestFocus(n => n is Hex1b.Nodes.TerminalNode);

            return ctx.VStack(v =>
            {
                var widgets = new List<Hex1bWidget>();
                var termWidth = Console.WindowWidth;

                // ── Header / Welcome / Help ──
                if (state.ShowHelp)
                {
                    widgets.AddRange(HelpRenderer.Render(v));
                }
                else if (state.History.Count == 0)
                {
                    widgets.AddRange(HeaderRenderer.RenderWelcome(v, termWidth));
                }
                else
                {
                    // ── Scrollable history panel ──
                    var reservedLines = 2 + (state.IsRunning ? 1 : 0)
                        + Math.Min(state.Suggestions.Count, 8)
                        + (state.Suggestions.Count > 8 ? 2 : 0) + 1;
                    var panelHeight = Math.Max(4, Console.WindowHeight - reservedLines);

                    var rendered = HistoryRenderer.Render(state.History);
                    var panel = v.VScrollPanel(sv =>
                    {
                        var lines = new List<Hex1bWidget>();
                        foreach (var line in rendered)
                            lines.Add(sv.Text(line));
                        return lines.ToArray();
                    }, showScrollbar: true).Follow() with { HeightHint = SizeHint.Fixed(panelHeight) };
                    widgets.Add(panel);
                }

                // ── Running indicator ──
                if (state.IsRunning)
                {
                    var elapsed = state.ElapsedSeconds;
                    var timeStr = elapsed < 60 ? $"{elapsed}s" : $"{elapsed / 60}m{elapsed % 60}s";
                    widgets.Add(v.Text($"  {Ansi.AccentPurple}⠋ running...{Ansi.End} {Ansi.Comment}(esc to cancel, {timeStr}){Ansi.End}"));
                }

                // ── Embedded shell terminal (prompt + input) ──
                var termRows = shellWorkload.RequiredRows;
                widgets.Add(v.Terminal(shellHandle) with { HeightHint = SizeHint.Fixed(termRows) });

                // ── Inline suggestions ──
                SuggestionRenderer.Render(widgets, v, state.Suggestions, state.SelectedSuggestion);

                // ── Footer ──
                widgets.Add(FooterRenderer.Render(v, state, termWidth));

                return widgets.ToArray();
            });
        },
            options
        );

        // Wire invalidate to also refresh the app
        var originalInvalidate = state.Invalidate;
        state.Invalidate = () =>
        {
            shellWorkload.RefreshPrompt();
            app?.Invalidate();
        };
        state.RequestStop = () => app?.RequestStop();

        await app.RunAsync();

        // Clean up terminal
        terminalCts.Cancel();
        try { await terminalTask; } catch (OperationCanceledException) { }
        await terminal.DisposeAsync();
    }
}
