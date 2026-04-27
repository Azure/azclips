using Core.Tui.Rendering;
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

        var theme = Hex1bThemes.Ocean.Clone("cirrus");
        theme.Set(TextBoxTheme.LeftBracket, "");
        theme.Set(TextBoxTheme.RightBracket, "");

        var options = new Hex1bAppOptions { EnableMouse = true, Theme = theme };
        Hex1bApp? app = null;
        app = new Hex1bApp(ctx =>
        {
            // Wire invalidate callback on first render
            state.Invalidate ??= () => app?.Invalidate();

            // Always keep keyboard focus on TextBox.
            // Mouse scroll still works on VScrollPanel via hit-test routing.
            app?.RequestFocus(n => n is TextBoxNode);

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
                    // VScrollPanel handles mouse scroll via hit-test routing.
                    // .Follow() auto-scrolls to bottom on new content.
                    // HeightHint caps height so TextBox + suggestions + footer always visible.
                    // Text selection: use Shift+mouse drag (standard terminal behavior with mouse mode).
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

                // ── Prompt + TextBox input ──
                widgets.Add(v.HStack(h => [
                    h.Text(state.Prompt),
                    h.TextBox(state.InputText)
                        .OnTextChanged(e =>
                        {
                            state.InputText = e.NewText;
                            state.UpdateSuggestions();
                        })
                        .WithInputBindings(bindings =>
                        {
                            // Override Enter for command execution (instead of default Submit)
                            bindings.Remove(TextBoxWidget.Submit);
                            bindings.Key(Hex1bKey.Enter).Action(() =>
                            {
                                state.ExecuteCommand();
                            }, "Execute command");
                            // Override Tab for autocomplete (instead of default focus-switch)
                            bindings.Key(Hex1bKey.Tab).Action(() =>
                            {
                                state.AcceptSuggestion();
                            }, "Accept suggestion");
                            bindings.Key(Hex1bKey.DownArrow).Action(() =>
                            {
                                if (state.Suggestions.Count > 0)
                                    state.SelectedSuggestion = (state.SelectedSuggestion + 1) % state.Suggestions.Count;
                                else
                                    state.NavigateHistory(1);
                            }, "Next suggestion / history");
                            bindings.Key(Hex1bKey.UpArrow).Action(() =>
                            {
                                if (state.Suggestions.Count > 0)
                                    state.SelectedSuggestion = (state.SelectedSuggestion - 1 + state.Suggestions.Count) % state.Suggestions.Count;
                                else
                                    state.NavigateHistory(-1);
                            }, "Previous suggestion / history");
                            bindings.Ctrl().Key(Hex1bKey.L).Action(() =>
                            {
                                state.History.Clear();
                                state.OutputScrollOffset = 0;
                            }, "Clear output");
                            bindings.Key(Hex1bKey.F1).Action(() =>
                            {
                                state.ShowHelp = !state.ShowHelp;
                            }, "Toggle help");
                            bindings.Key(Hex1bKey.Escape).Action(() =>
                            {
                                if (state.ShowHelp)
                                    state.ShowHelp = false;
                            }, "Close help");
                            bindings.Key(Hex1bKey.F2).Action(() =>
                            {
                                state.ToggleMode();
                            }, "Toggle Azure/Local mode");
                        }),
                ]));

                // ── Inline suggestions ──
                SuggestionRenderer.Render(widgets, v, state.Suggestions, state.SelectedSuggestion);

                // ── Footer ──
                widgets.Add(FooterRenderer.Render(v, state, termWidth));

                return widgets.ToArray();
            });
        },
            options
        );

        await app.RunAsync();
    }
}
