using Core.Abstractions;
using Core.DependencyInjection;
using Core.Tui.Azure;
using Core.Tui.Rendering;
using Core.Tui.Services;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Tui;

public static class Program
{
    private static ITelemetryService? _telemetryService;
    private static ILoggingService? _logger;

    public static async Task Main()
    {
        CirrusServiceProvider.PrependRegistration = services =>
        {
            services.AddSingleton<ArmClient>();
            services.AddSingleton<AzureVfs>();
            services.AddSingleton<ProcessService>();
            services.AddSingleton<PromptService>();
            services.AddSingleton<HistoryService>();
            services.AddSingleton<AppState>();
        };
        CirrusServiceProvider.OverrideRegistration = services =>
        {
            // Replace the default ScopedLoggingService (which falls back to
            // ConsoleLoggingService and writes [VERBOSE]/[DEBUG] directly to the
            // console) with a silent implementation that won't corrupt the TUI.
            services.AddSingleton<ILoggingService, TuiLoggingService>();
        };
        CirrusServiceProvider.Initialize(ClientType.TUI);
        _telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        _logger = CirrusServiceProvider.GetRequiredService<ILoggingService>();

        try
        {
            var state = CirrusServiceProvider.GetRequiredService<AppState>();
            await state.InitVfsAsync();

            var theme = Hex1bThemes.Ocean.Clone("cirrus");

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
        }
        finally
        {
            _telemetryService?.Dispose();
            CirrusServiceProvider.Dispose();
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

            widgets.Add(v.VStack(top =>
            {
                var topWidgets = new List<Hex1bWidget>();

                // ── Header / Welcome / Help ──
                if (state.ShowHelp)
                {
                    topWidgets.Add(HelpRenderer.Render(v));
                }
                else if (state.HistoryCount == 0)
                {
                    topWidgets.Add(HeaderRenderer.RenderWelcome(v, termWidth));
                }
                else
                {
                    // ── Scrollable history panel ──
                    topWidgets.Add((v.VScrollPanel(sv =>
                    {
                        var rendered = HistoryRenderer.Render(sv, state.GetHistorySnapshot());
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

                // ── Prompt + input (Text widget for display, hidden TextBox for focus/input) ──
                var prompt = state.Prompt;
                var input = state.InputText;
                var cursorPos = state.CursorPos;

                // Build display text: prompt + input with a block cursor via ANSI reverse video
                var fullText = prompt + input;
                var absCursor = prompt.Length + cursorPos;
                var before = fullText[..Math.Min(absCursor, fullText.Length)];
                var cursorChar = absCursor < fullText.Length ? fullText[absCursor].ToString() : " ";
                var after = absCursor < fullText.Length ? fullText[(absCursor + 1)..] : "";
                var cursorBlock = $"\x1b[7m{cursorChar}\x1b[27m";
                var displayLine = $"{before}{cursorBlock}{after}";

                topWidgets.Add(v.Text(displayLine).Wrap().Height(SizeHint.Content));

                // Hidden TextBox (0 height) as a focus sink for AnyCharacter input.
                // Character bindings only fire on the focused node (TextBoxNode),
                // not on wrapper nodes. So we use OnTextChanged to relay typed chars.
                // Global bindings handle Backspace/Delete/arrows etc. and fire BEFORE
                // the TextBox sees them, so they never reach the TextBox.
                topWidgets.Add(v.ThemePanel(
                    t => t
                        .Set(TextBoxTheme.FillBackgroundColor, Hex1bColor.Default)
                        .Set(TextBoxTheme.FocusedFillBackgroundColor, Hex1bColor.Default),
                    v.TextBox(state.InputText)
                        .OnTextChanged(e =>
                        {
                            // TextBox cursor is always at end (reset by reconciliation).
                            // Detect appended characters and insert at our cursor position.
                            if (e.NewText.Length > e.OldText.Length)
                            {
                                var added = e.NewText[e.OldText.Length..];
                                state.InputText = state.InputText.Insert(state.CursorPos, added);
                                state.CursorPos += added.Length;
                                state.UpdateSuggestions();
                                app.Invalidate();
                            }
                        })
                ).Height(SizeHint.Fixed(0)));

                // ── Inline suggestions (always present to keep widget indices stable) ──
                if (state.Suggestions.Count > 0)
                    topWidgets.Add(SuggestionRenderer.Render(v, state.Suggestions, state.SelectedSuggestion));
                else
                    topWidgets.Add(v.Text("").Height(SizeHint.Fixed(0)));

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
                    if (state.ShowHelp) { state.ShowHelp = false; app.Invalidate(); }
                    else if (state.IsRunning) { state.CancelRunning(); }
                }, "Close/Cancel");

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
                    state.ExecuteCommand();
                    app.Invalidate();
                }, "Submit command");

            bindings.Key(Hex1bKey.Backspace).Global()
                .Action(_ =>
                {
                    if (state.CursorPos > 0)
                    {
                        state.InputText = state.InputText.Remove(state.CursorPos - 1, 1);
                        state.CursorPos--;
                        state.UpdateSuggestions();
                        app.Invalidate();
                    }
                }, "Delete backward");

            bindings.Key(Hex1bKey.Delete).Global()
                .Action(_ =>
                {
                    if (state.CursorPos < state.InputText.Length)
                    {
                        state.InputText = state.InputText.Remove(state.CursorPos, 1);
                        state.UpdateSuggestions();
                        app.Invalidate();
                    }
                }, "Delete forward");

            bindings.Key(Hex1bKey.LeftArrow).Global()
                .Action(_ =>
                {
                    if (state.CursorPos > 0) { state.CursorPos--; app.Invalidate(); }
                }, "Cursor left");

            bindings.Key(Hex1bKey.RightArrow).Global()
                .Action(_ =>
                {
                    if (state.CursorPos < state.InputText.Length) { state.CursorPos++; app.Invalidate(); }
                }, "Cursor right");

            bindings.Key(Hex1bKey.Home).Global()
                .Action(_ =>
                {
                    state.CursorPos = 0;
                    app.Invalidate();
                }, "Cursor to start");

            bindings.Key(Hex1bKey.End).Global()
                .Action(_ =>
                {
                    state.CursorPos = state.InputText.Length;
                    app.Invalidate();
                }, "Cursor to end");

            bindings.Ctrl().Key(Hex1bKey.Backspace).Global()
                .Action(_ =>
                {
                    if (state.CursorPos > 0)
                    {
                        var pos = state.CursorPos;
                        while (pos > 0 && state.InputText[pos - 1] == ' ') pos--;
                        while (pos > 0 && state.InputText[pos - 1] != ' ') pos--;
                        state.InputText = state.InputText.Remove(pos, state.CursorPos - pos);
                        state.CursorPos = pos;
                        state.UpdateSuggestions();
                        app.Invalidate();
                    }
                }, "Delete word backward");

            bindings.Key(Hex1bKey.UpArrow)
                .Global()
                .Action(_ =>
                {
                    if (state.Suggestions.Count > 0)
                        state.SelectedSuggestion = (state.SelectedSuggestion - 1 + state.Suggestions.Count) % state.Suggestions.Count;
                    else
                        state.NavigateHistory(-1);
                    app.Invalidate();
                }, "Previous suggestion/history");

            bindings.Key(Hex1bKey.DownArrow)
                .Global()
                .Action(_ =>
                {
                    if (state.Suggestions.Count > 0)
                        state.SelectedSuggestion = (state.SelectedSuggestion + 1) % state.Suggestions.Count;
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
