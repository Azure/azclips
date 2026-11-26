// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Tui;
using Core.Tui.Services;
using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;
using TuiProgram = Core.Tui.Program;

namespace Tui.Tests;

public class SuggestionEditorTests
{
    [Fact]
    public async Task ArrowsTransferFocusAndAcceptanceReturnsToEditor()
    {
        await using var tui = await TuiTestApp.StartAsync();
        var (auto, state, app) = (tui.Automator, tui.State, tui.App);

        await auto.TypeAsync("/");
        await tui.WaitUntilAsync(() => state.Suggestions.Count > 1, "slash suggestions");
        await auto.DownAsync();
        await tui.WaitUntilAsync(() => app.FocusedNode is ListNode<Suggestion> && state.SelectedSuggestion == 0,
            "list focused on first suggestion");
        await auto.DownAsync();
        await tui.WaitUntilAsync(() => state.SelectedSuggestion == 1, "second suggestion selected");
        await auto.UpAsync();
        await tui.WaitUntilAsync(() => state.SelectedSuggestion == 0, "first suggestion selected");
        await auto.RightAsync();
        await tui.WaitUntilAsync(() => app.FocusedNode is TextBoxNode && state.InputText == "/cd ",
            "completion accepted into input");

        Assert.False(state.IsSuggestionSelected());
        Assert.Equal(state.InputText.Length, state.CursorPos);
    }

    [Theory]
    [InlineData(Hex1bKey.Tab)]
    [InlineData(Hex1bKey.Enter)]
    public async Task SelectedSuggestionIsAcceptedWithoutSubmission(Hex1bKey key)
    {
        await using var tui = await TuiTestApp.StartAsync();
        var (auto, state, app) = (tui.Automator, tui.State, tui.App);

        await auto.TypeAsync("/pw");
        await tui.WaitUntilAsync(() => state.Suggestions.Count == 1, "single /pwd suggestion");
        await auto.DownAsync();
        await tui.WaitUntilAsync(() => app.FocusedNode is ListNode<Suggestion>, "list focused");
        await auto.KeyAsync(key);
        await tui.WaitUntilAsync(() => app.FocusedNode is TextBoxNode && state.InputText == "/pwd ",
            "suggestion accepted into input");

        Assert.Equal(0, state.HistoryCount);
    }

    [Fact]
    public async Task EscapeAndListBoundaryRestoreInputFocus()
    {
        await using var tui = await TuiTestApp.StartAsync();
        var (auto, state, app) = (tui.Automator, tui.State, tui.App);

        await auto.TypeAsync("/");
        await tui.WaitUntilAsync(() => state.Suggestions.Count > 1, "slash suggestions");
        await auto.UpAsync();
        await tui.WaitUntilAsync(() => app.FocusedNode is ListNode<Suggestion>
            && state.SelectedSuggestion == state.Suggestions.Count - 1, "list focused on last suggestion");
        await auto.DownAsync();
        await tui.WaitUntilAsync(() => app.FocusedNode is TextBoxNode, "input focused past list end");
        await auto.DownAsync();
        await tui.WaitUntilAsync(() => app.FocusedNode is ListNode<Suggestion>, "list focused");
        await auto.EscapeAsync();
        await tui.WaitUntilAsync(() => app.FocusedNode is TextBoxNode && !state.IsSuggestionSelected(),
            "escape returns to input");
        await auto.TypeAsync("pw");
        await tui.WaitUntilAsync(() => state.InputText == "/pw", "typing resumes");
    }

    [Fact]
    public async Task RightArrowCompletesAtEndButMovesCursorInsideInput()
    {
        await using var tui = await TuiTestApp.StartAsync();
        var (auto, state, app) = (tui.Automator, tui.State, tui.App);

        await auto.TypeAsync("/pw");
        await tui.WaitUntilAsync(() => state.InputText == "/pw", "input typed");
        await auto.LeftAsync();
        await tui.WaitUntilAsync(() => state.CursorPos == 2, "cursor moved left");
        await auto.RightAsync();
        await tui.WaitUntilAsync(() => state.CursorPos == 3, "cursor moved right without completing");
        await auto.RightAsync();
        await tui.WaitUntilAsync(() => state.InputText == "/pwd ", "completed at end of input");

        Assert.IsType<TextBoxNode>(app.FocusedNode);
    }

    [Fact]
    public async Task TypingWhileBrowsingSuggestionsReturnsToInput()
    {
        await using var tui = await TuiTestApp.StartAsync();
        var (auto, state, app) = (tui.Automator, tui.State, tui.App);

        await auto.TypeAsync("/p");
        await tui.WaitUntilAsync(() => state.InputText == "/p", "input typed");
        await auto.DownAsync();
        await tui.WaitUntilAsync(() => app.FocusedNode is ListNode<Suggestion>, "list focused");
        await auto.TypeAsync("w");
        await tui.WaitUntilAsync(() => app.FocusedNode is TextBoxNode && state.InputText == "/pw",
            "typing returns to input");
    }

    [Fact]
    public async Task HistoryAndMultilineInputRemainEditable()
    {
        await using var tui = await TuiTestApp.StartAsync(
            configure: (_, history) => history.GetHistory(ShellMode.Azure).Add("zz-history"));
        var (auto, state, app) = (tui.Automator, tui.State, tui.App);

        await auto.UpAsync();
        await tui.WaitUntilAsync(() => state.InputText == "zz-history", "history recalled");
        await auto.DownAsync();
        await tui.WaitUntilAsync(() => state.InputText == "", "history cleared");
        await auto.EnterAsync();
        await auto.KeyAsync(Hex1bKey.Enter, Hex1bModifiers.Control);
        await auto.TypeAsync("zz");
        await tui.WaitUntilAsync(() => state.InputText == "\nzz", "multiline input");
        await auto.LeftAsync();
        await auto.RightAsync();
        await tui.WaitUntilAsync(() => state.CursorPos == 3, "cursor at end of second line");

        Assert.IsType<TextBoxNode>(app.FocusedNode);
        Assert.Equal(2, TuiProgram.ComputeWordWrapLines(state.InputText, 78));
    }

    [Theory]
    [InlineData(40, 18, 0)]
    [InlineData(80, 24, 0)]
    [InlineData(120, 40, 0)]
    [InlineData(40, 18, 1)]
    [InlineData(80, 24, 1)]
    [InlineData(120, 40, 1)]
    [InlineData(40, 18, 2)]
    [InlineData(80, 24, 2)]
    [InlineData(120, 40, 2)]
    public async Task EditorStaysAboveFooterAsSuggestionsAndInputGrow(int width, int height, int panel)
    {
        await using var tui = await TuiTestApp.StartAsync(width, height, (state, _) =>
        {
            if (panel == 1)
                state.AddHistory(new HistoryEntry(HistoryItemType.Info, "Previous output"));
            state.ShowHelp = panel == 2;
        });
        var (state, app) = (tui.State, tui.App);

        foreach (var text in new[] { "zz", "first\nsecond", new string('z', width + 5), "" })
        {
            foreach (var count in new[] { 0, 1, 8, 12, 24, 0 })
            {
                var inputRows = TuiProgram.ComputeWordWrapLines(text, width - 2);
                state.InputText = text;
                state.Suggestions.Clear();
                state.Suggestions.AddRange(Enumerable.Range(0, count)
                    .Select(i => new Suggestion($"suggestion-{i}", "test")));

                await tui.WaitUntilAsync(() =>
                    app.FocusedNode is TextBoxNode editor && editor.Text == text
                    && editor.IsMultiline == (text.Contains('\n') || inputRows > 1)
                    && ListOf(editor).Bounds.Height == Math.Min(8, count),
                    $"{count} suggestions above '{text.Replace("\n", "\\n")}'");

                var editor = Assert.IsType<TextBoxNode>(app.FocusedNode);
                var list = ListOf(editor);
                Assert.Equal(inputRows, editor.Bounds.Height);
                Assert.Equal(height - 2, editor.Bounds.Y + editor.Bounds.Height);
                Assert.Equal(editor.Bounds.Y, list.Bounds.Y + list.Bounds.Height);
                Assert.Equal(Math.Min(8, count) + inputRows, Assert.IsType<VStackNode>(editor.Parent!.Parent).Bounds.Height);
            }
        }
    }

    private static ListNode<Suggestion> ListOf(TextBoxNode editor) =>
        Assert.IsType<VStackNode>(editor.Parent!.Parent).Children.OfType<ListNode<Suggestion>>().Single();
}
