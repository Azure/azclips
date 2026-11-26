using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Core.Tui.Rendering;

static class SuggestionRenderer
{
    public static Hex1bWidget Build(WidgetContext<VStackWidget> v,
        AppState state, Hex1bApp app, int inputWidth)
    {
        const int maxVisible = 8;
        return v.VStack(inner =>
        {
            var widgets = new List<Hex1bWidget>();
            widgets.Add(inner.List(state.Suggestions)
                .FocusedIndex(Math.Max(0, state.SelectedSuggestion))
                .ItemTemplate(row => row.Text(
                    row.OwnerHasFocus && row.IsFocused
                        ? $"  {Ansi.AccentPurple}▸ {row.Item.Text,-28}{Ansi.End} {Ansi.Foreground}{row.Item.Description}{Ansi.End}"
                        : $"  {Ansi.Comment}  {row.Item.Text,-28} {row.Item.Description}{Ansi.End}"))
                .OnFocusChanged(e => state.SelectedSuggestion = e.FocusedIndex)
                .OnItemActivated(e =>
                {
                    state.SelectedSuggestion = e.ActivatedIndex;
                    state.AcceptSuggestion();
                    app.RequestFocus(n => n is TextBoxNode);
                    app.Invalidate();
                })
                .InputBindings(bindings => bindings.AnyCharacter().Action((text, _) =>
                {
                    state.EditorState.HandleInput(new Hex1bKeyEvent(
                        Hex1bKey.None, text, Hex1bModifiers.None));
                    state.UpdateSuggestions();
                    app.RequestFocus(n => n is TextBoxNode);
                    app.Invalidate();
                    return Task.CompletedTask;
                }, "Resume editing"))
                .Height(SizeHint.Fixed(Math.Min(maxVisible, state.Suggestions.Count))));

            var wrapLines = Program.ComputeWordWrapLines(state.InputText, inputWidth);
            var editor = inner.TextBox().State(state.EditorState)
                .Predict((text, _) => Task.FromResult(state.GetPrediction(text)))
                .WordWrap().Height(wrapLines)
                .OnTextChanged(e =>
                {
                    state.UpdateSuggestions();
                    app.Invalidate();
                });
            // Hex1b predictions are single-line only; retain multiline editing
            // when the command contains a newline or wraps across rows.
            if (state.InputText.Contains('\n') || wrapLines > 1)
                editor = editor.Multiline();

            widgets.Add(inner.HStack(h =>
            [
                TuiTheme.WithForeground(h, TuiTheme.ToHex1bColor(state.Theme.ChevronColor),
                    h.Text("> ")).ContentWidth(),
                editor,
            ]));
            return widgets.ToArray();
        }).Height(SizeHint.Content);
    }
}
