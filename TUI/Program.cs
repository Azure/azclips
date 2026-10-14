using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;
using Core.Tui.Azure;
using Core.Tui.Rendering;
using Core.Tui.Services;
using Core.Tui.Telemetry;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Tui;

public static class Program
{
    private const string FirstPartyApplicationId = "1950a258-227b-4e31-a9cf-717495945fc2";
    private static ILoggingService? _logger;

    public static async Task<int> Main(string[] args)
    {
        AzclipsServiceProvider.PrependRegistration(services =>
        {
            services.AddSingleton<ArmClient>();
            services.AddSingleton<AzureVfs>();
            services.AddSingleton<ProcessService>();
            services.AddSingleton<PromptService>();
            services.AddSingleton<HistoryService>();
            services.AddSingleton<AppState>();
        });
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            // Replace the default ScopedLoggingService (which falls back to
            // ConsoleLoggingService and writes VERBOSE:/DEBUG: directly to the
            // console) with a silent implementation that won't corrupt the TUI.
            services.AddSingleton<ILoggingService, TuiLoggingService>();
            services.AddSingleton<ITelemetrySender, FileTelemetrySender>();
            services.AddSingleton<ITelemetryProvider, TUITelemetryProvider>();
        });
        var bootSw = System.Diagnostics.Stopwatch.StartNew();
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(ClientType.TUI, FirstPartyApplicationId));
        Console.Error.WriteLine($"[startup] DI init: {bootSw.ElapsedMilliseconds}ms");
        _logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();

        try
        {
            var state = AzclipsServiceProvider.GetRequiredService<AppState>();
            var vfsSw = System.Diagnostics.Stopwatch.StartNew();
            await state.InitVfsAsync();
            Console.Error.WriteLine($"[startup] InitVfsAsync total: {vfsSw.ElapsedMilliseconds}ms");
            Console.Error.WriteLine($"[startup] Total before UI: {bootSw.ElapsedMilliseconds}ms");

            var theme = TuiTheme.CreateBaseTheme();

            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithHex1bApp((app, options) =>
                {
                    options.EnableMouse = true;
                    options.Theme = theme;

                    // Wire state to app
                    state.Invalidate = () => app.Invalidate();
                    state.RequestStop = () => app.RequestStop();

                    return ctx => BuildUI(ctx, app, state);
                })
                .Build();

            await terminal.RunAsync();
            return 0;
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    private static Hex1bWidget BuildUI<TParent>(WidgetContext<TParent> ctx, Hex1bApp app, AppState state)
        where TParent : Hex1bWidget
    {
        // Always request focus on the hidden TextBox so character input works.
        // This ensures focus is restored even if widget-type changes at other
        // indices cause the FocusRing to lose track of the previously focused node.
        app.RequestFocus(n => n is TextBoxNode);

        var termWidth = Console.WindowWidth;

        Hex1bWidget hex1BWidget = ctx.VStack(v =>
        {
            var widgets = new List<Hex1bWidget>();

            // ── Menu bar (themed dark) ──
            widgets.Add(TuiTheme.ApplyMenuBarTheme(v,
                MenuBarRenderer.Render(v, state, () => app.Invalidate())));

            widgets.Add(v.Separator());

            widgets.Add(v.VStack(top =>
            {
                var topWidgets = new List<Hex1bWidget>();

                // ── Header / Welcome / Help ──
                if (state.ShowHelp)
                {
                    topWidgets.Add(HelpRenderer.Render(v).Fill());
                }
                else if (state.HistoryCount == 0)
                {
                    topWidgets.Add(HeaderRenderer.RenderWelcome(v, termWidth).Fill());
                }
                else
                {
                    // ── Scrollable history panel ──
                    topWidgets.Add((v.VScrollPanel(sv =>
                    {
                        var rendered = HistoryRenderer.Render(sv, state.GetHistorySnapshot(), state.Theme);
                        return rendered.ToArray();
                    }, showScrollbar: true).Follow()).Fill());
                }

                // ── Running indicator (always present to keep widget indices stable) ──
                if (state.IsRunning)
                {
                    var elapsed = state.ElapsedSeconds;
                    var timeStr = elapsed < 60 ? $"{elapsed}s" : $"{elapsed / 60}m{elapsed % 60}s";
                    topWidgets.Add(v.HStack(h =>
                    [
                        h.Spinner(SpinnerStyle.Dots),
                        h.Text($" running... ({timeStr})"),
                    ]).Height(SizeHint.Content));
                }
                else
                {
                    topWidgets.Add(v.HStack(h => []).Height(SizeHint.Fixed(0)));
                }

                var t = state.Theme;

                // ── Suggestions (above prompt, if configured) ──
                if (t.SuggestionStyle == SuggestionStyle.Above && state.Suggestions.Count > 0)
                {
                    topWidgets.Add(v.Separator());
                    topWidgets.Add(v.Border(b => [SuggestionRenderer.Render(v, state.Suggestions, state.SelectedSuggestion)]));
                }
                else
                {
                    topWidgets.Add(v.Separator());
                }

                // Prompt + input (visible TextBox)
                var prompt = state.Prompt.TrimEnd(' ', '>');

                var promptColor = TuiTheme.ToHex1bColor(t.PromptPathColor);
                topWidgets.Add(TuiTheme.WithForeground(v, promptColor, v.Text(prompt)).Height(SizeHint.Content));

                var inputWidth = Math.Max(1, termWidth - 2); // 2 = "> "
                var wrapLines = ComputeWordWrapLines(state.InputText, inputWidth);

                var chevronColor = TuiTheme.ToHex1bColor(t.ChevronColor);
                var chevronWidget = TuiTheme.WithForeground(v, chevronColor, v.Text("> ")).ContentWidth();

                topWidgets.Add(v.HStack(h =>
                [
                    chevronWidget,
                    v.TextBox().State(state.EditorState).Multiline().WordWrap().Height(wrapLines)
                        .OnTextChanged(e =>
                        {
                            state.UpdateSuggestions();
                            app.Invalidate();
                        })
                ]));

                // ── Suggestions (below input or inline) ──
                if (t.SuggestionStyle != SuggestionStyle.Above && state.Suggestions.Count > 0)
                {
                    if (t.SuggestionStyle == SuggestionStyle.Inline)
                        topWidgets.Add(SuggestionRenderer.Render(v, state.Suggestions, state.SelectedSuggestion));
                    else
                        topWidgets.Add(v.Border(b => [SuggestionRenderer.Render(v, state.Suggestions, state.SelectedSuggestion)]));
                }
                else if (t.SuggestionStyle != SuggestionStyle.Above)
                {
                    topWidgets.Add(v.Text("").Height(SizeHint.Fixed(0)));
                    topWidgets.Add(v.Separator());
                }
                else
                {
                    topWidgets.Add(v.Separator());
                }

                return topWidgets.ToArray();
            }).Fill());

            widgets.Add(v.VStack(bottom =>
            {
                var bottomWidgets = new List<Hex1bWidget>();

                // ── Footer ──
                bottomWidgets.Add(FooterRenderer.Render(v, state, termWidth));

                return bottomWidgets.ToArray();
            }).Height(SizeHint.Content));

            return widgets.ToArray();
        }).InputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.F1).Global()
                .Action(_ =>
                {
                    state.ShowHelp = !state.ShowHelp;
                    app.Invalidate();
                }, "Toggle help");

            bindings.Key(Hex1bKey.F2).Global()
                .Action(_ =>
                {
                    state.ToggleMode();
                    app.Invalidate();
                }, "Toggle mode");

            bindings.Key(Hex1bKey.Escape).Global()
                .Action(_ =>
                {
                    if (state.IsSuggestionSelected()) { state.ResetSelectedSuggestion(); }
                    else if (state.ShowHelp) { state.ShowHelp = false; app.Invalidate(); }
                    else if (state.IsRunning) { state.CancelRunning(); }
                    else { app.RequestStop(); }
                }, "Reset Suggestion Status/Close Help Panel/Cancel Running/Exit Application");

            bindings.Ctrl().Key(Hex1bKey.C).Global()
                .Action(_ =>
                {
                    if (state.IsRunning)
                    {
                        state.CancelRunning();
                    }
                    else
                    {
                        // No selection — new input line (like pwsh Ctrl+C)
                        if (!string.IsNullOrEmpty(state.InputText))
                        {
                            state.AddHistory(new HistoryEntry(HistoryItemType.User, state.Prompt + state.InputText));
                            state.AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
                        }
                        state.InputText = "";
                        state.CursorPos = 0;
                        state.UpdateSuggestions();
                    }
                    app.Invalidate();
                }, "Cancel/New line");

            bindings.Key(Hex1bKey.Enter).Global()
                .Action(_ =>
                {
                    if (state.IsSuggestionSelected())
                    {
                        state.AcceptSuggestion();
                        app.Invalidate();
                    }
                    else
                    {
                        state.ExecuteCommand();
                        app.Invalidate();
                    }
                }, "Select suggestion or submit command");

            bindings.Ctrl().Key(Hex1bKey.Enter).Global()
                .Action(_ =>
                {
                    state.InputText = state.InputText.Insert(state.CursorPos, "\n");
                    state.CursorPos++;
                    app.Invalidate();
                }, "Insert newline");

            bindings.Key(Hex1bKey.UpArrow)
                .Global()
                .Action(_ =>
                {
                    if (state.Suggestions.Count > 0)
                    {
                        state.NavigateSuggestion(-1);
                    }
                    else
                    {
                        state.NavigateHistory(-1);
                    }
                    app.Invalidate();
                }, "Previous suggestion/history");

            bindings.Key(Hex1bKey.DownArrow)
                .Global()
                .Action(_ =>
                {
                    if (state.Suggestions.Count > 0)
                    {
                        state.NavigateSuggestion(1);
                    }
                    else
                        state.NavigateHistory(1);
                    app.Invalidate();
                }, "Next suggestion/history");

            bindings.Key(Hex1bKey.Tab)
                .Global()
                .Action(_ =>
                {
                    state.AcceptSuggestion();
                    app.Invalidate();
                }, "Accept suggestion");

            bindings.Ctrl().Key(Hex1bKey.L)
                .Global()
                .Action(_ =>
                {
                    state.ClearHistory();
                    state.OutputScrollOffset = 0;
                    app.Invalidate();
                }, "Clear history");
        });

        return hex1BWidget;
    }

    /// <summary>
    /// Computes wrapped line count matching TextBox's WrapLine algorithm.
    /// </summary>
    private static int ComputeWordWrapLines(string text, int width)
    {
        if (width <= 0 || string.IsNullOrEmpty(text) || text.Length <= width)
            return 1;

        var count = 0;
        var offset = 0;
        while (offset < text.Length)
        {
            count++;
            var remaining = text.Length - offset;
            if (remaining <= width) break;

            var breakPoint = text.LastIndexOf(' ', offset + width - 1, width);
            if (breakPoint <= offset)
                breakPoint = offset + width;
            else
                breakPoint++;
            offset = breakPoint;
        }
        return Math.Max(1, count);
    }
}
