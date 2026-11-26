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
        var suggestionRows = Math.Min(maxVisible, state.Suggestions.Count);
        var inputRows = Program.ComputeWordWrapLines(state.InputText, inputWidth);
        // List measurement reserves at least one row and ignores its capped height.
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
                .Height(SizeHint.Fixed(suggestionRows)));

            var editor = inner.TextBox().State(state.EditorState)
                .WordWrap().Height(inputRows)
                .OnTextChanged(e =>
                {
                    state.TypedInputText = state.InputText;
                    state.UpdateSuggestions();
                    app.Invalidate();
                });
            // Hex1b only refreshes predictions on typed input. After programmatic
            // edits (submit, history, completion) omit the predictor for one frame,
            // which makes the TextBox discard any stale ghost text.
            if (state.TypedInputText == state.InputText)
            {
                editor = editor.Predict((text, _) => Task.FromResult(state.GetPrediction(text)));
            }
            else
            {
                state.TypedInputText = state.InputText;
                app.Invalidate();
            }
            // Hex1b predictions are single-line only; retain multiline editing
            // when the command contains a newline or wraps across rows.
            if (state.InputText.Contains('\n') || inputRows > 1)
                editor = editor.Multiline();

            widgets.Add(inner.HStack(h =>
            [
                TuiTheme.WithForeground(h, TuiTheme.ToHex1bColor(state.Theme.ChevronColor),
                    h.Text("> ")).ContentWidth(),
                editor.FillWidth(),
            ]).Height(SizeHint.Fixed(inputRows)));
            return widgets.ToArray();
        }).Height(SizeHint.Fixed(suggestionRows + inputRows));
    }
}
