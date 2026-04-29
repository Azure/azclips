using System.Reflection;
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

        var theme = Hex1bThemes.Ocean.Clone("cirrus");
        theme.Set(TextBoxTheme.LeftBracket, "");
        theme.Set(TextBoxTheme.RightBracket, "");
        theme.Set(TextBoxTheme.UseFillMode, false);
        theme.Set(TextBoxTheme.BackgroundColor, Hex1bColor.Default);
        theme.Set(TextBoxTheme.FillBackgroundColor, Hex1bColor.Default);
        theme.Set(TextBoxTheme.FocusedFillBackgroundColor, Hex1bColor.Default);

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
                var promptPrefix = state.Prompt;
                var promptLen = promptPrefix.Length;

                // Reflection helper to access internal TextBoxState
                var stateProperty = typeof(TextBoxNode).GetProperty("State",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                int GetCursor(TextBoxNode node)
                {
                    var s = stateProperty?.GetValue(node);
                    return s is null ? 0 : (int)(s.GetType().GetProperty("CursorPosition")!.GetValue(s)!);
                }

                void SetCursor(TextBoxNode node, int pos)
                {
                    var s = stateProperty?.GetValue(node);
                    s?.GetType().GetProperty("CursorPosition")?.SetValue(s, pos);
                }

                var promptTextBox = v.TextBox(promptPrefix + state.InputText)
                    .FillWidth()
                    .Multiline()
                    .WordWrap()
                    .OnTextChanged(e =>
                    {
                        if (e.NewText.StartsWith(promptPrefix))
                        {
                            state.InputText = e.NewText[promptLen..];
                            state.UpdateSuggestions();
                        }
                        else
                        {
                            e.Node.Text = promptPrefix + state.InputText;
                            SetCursor(e.Node, promptLen);
                        }
                    })
                    .WithInputBindings(bindings =>
                    {
                        // Guard Backspace: don't delete into prompt
                        bindings.Remove(TextBoxWidget.DeleteBackward);
                        bindings.Key(Hex1bKey.Backspace).Action(ctx =>
                        {
                            if (ctx.FocusedNode is TextBoxNode node)
                            {
                                int cursor = GetCursor(node);
                                if (cursor > promptLen)
                                {
                                    var text = node.Text;
                                    node.Text = text[..(cursor - 1)] + text[cursor..];
                                    SetCursor(node, cursor - 1);
                                    state.InputText = node.Text[promptLen..];
                                    state.UpdateSuggestions();
                                }
                            }
                        }, "Delete backward (guarded)");

                        // Guard Home: go to prompt end, not position 0
                        bindings.Remove(TextBoxWidget.MoveHome);
                        bindings.Key(Hex1bKey.Home).Action(ctx =>
                        {
                            if (ctx.FocusedNode is TextBoxNode node)
                                SetCursor(node, promptLen);
                        }, "Move to start of input");

                        // Guard Left arrow: don't move into prompt
                        bindings.Remove(TextBoxWidget.MoveLeft);
                        bindings.Key(Hex1bKey.LeftArrow).Action(ctx =>
                        {
                            if (ctx.FocusedNode is TextBoxNode node && GetCursor(node) > promptLen)
                                SetCursor(node, GetCursor(node) - 1);
                        }, "Move left (guarded)");

                        // Override Enter for command execution (not newline insertion)
                        bindings.Remove(TextBoxWidget.Submit);
                        bindings.Remove(TextBoxWidget.InsertNewline);
                        bindings.Key(Hex1bKey.Enter).Action(() =>
                        {
                            state.ExecuteCommand();
                        }, "Execute command");
                        // Override Tab for autocomplete (instead of default focus-switch)
                        bindings.Key(Hex1bKey.Tab).Action(() =>
                        {
                            state.AcceptSuggestion();
                        }, "Accept suggestion");
                        // Override Up/Down for suggestions/history (not multiline line navigation)
                        bindings.Remove(TextBoxWidget.MoveDown);
                        bindings.Remove(TextBoxWidget.MoveUp);
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
                        bindings.Ctrl().Key(Hex1bKey.V).Action(() =>
                        {
                            var clip = ClipboardService.GetText();
                            if (!string.IsNullOrEmpty(clip))
                            {
                                // Replace newlines with spaces for single-line input
                                state.InputText += clip.ReplaceLineEndings(" ");
                                state.UpdateSuggestions();
                            }
                        }, "Paste from clipboard");
                    });
                widgets.Add(promptTextBox);

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
