// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Tui;
using Core.Tui.Azure;
using Core.Tui.Services;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;
using Moq;
using Tests.Common.Utilities;
using TuiProgram = Core.Tui.Program;

namespace Cli.Tests;

public class TuiSuggestionEditorTests
{
    [Fact]
    public async Task ArrowsTransferFocusAndAcceptanceReturnsToEditor()
    {
        await WithEditor(async (terminal, app, state, _) =>
        {
            await Sequence().Type("/")
                .WaitUntil(_ => state.Suggestions.Count > 1, AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.DownArrow)
                .WaitUntil(_ => app.FocusedNode is ListNode<Suggestion> && state.SelectedSuggestion == 0,
                    AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.DownArrow)
                .WaitUntil(_ => state.SelectedSuggestion == 1, AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.UpArrow)
                .WaitUntil(_ => state.SelectedSuggestion == 0, AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => app.FocusedNode is TextBoxNode && state.InputText == "/cd ",
                    AgentTestTimeouts.HangGuard)
                .Build().ApplyAsync(terminal);

            Assert.False(state.IsSuggestionSelected());
            Assert.Equal(state.InputText.Length, state.CursorPos);
        });
    }

    [Theory]
    [InlineData(Hex1bKey.Tab)]
    [InlineData(Hex1bKey.Enter)]
    public async Task SelectedSuggestionIsAcceptedWithoutSubmission(Hex1bKey key)
    {
        await WithEditor(async (terminal, app, state, _) =>
        {
            await Sequence().Type("/pw")
                .WaitUntil(_ => state.Suggestions.Count == 1, AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.DownArrow)
                .WaitUntil(_ => app.FocusedNode is ListNode<Suggestion>, AgentTestTimeouts.HangGuard)
                .Key(key)
                .WaitUntil(_ => app.FocusedNode is TextBoxNode && state.InputText == "/pwd ",
                    AgentTestTimeouts.HangGuard)
                .Build().ApplyAsync(terminal).WithAgentHangGuard();

            Assert.Equal(0, state.HistoryCount);
        });
    }

    [Fact]
    public async Task EscapeAndListBoundaryRestoreInputFocus()
    {
        await WithEditor(async (terminal, app, state, _) =>
        {
            await Sequence().Type("/")
                .WaitUntil(_ => state.Suggestions.Count > 1, AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.UpArrow)
                .WaitUntil(_ => app.FocusedNode is ListNode<Suggestion>
                    && state.SelectedSuggestion == state.Suggestions.Count - 1, AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.DownArrow)
                .WaitUntil(_ => app.FocusedNode is TextBoxNode, AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.DownArrow)
                .WaitUntil(_ => app.FocusedNode is ListNode<Suggestion>, AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.Escape)
                .WaitUntil(_ => app.FocusedNode is TextBoxNode && !state.IsSuggestionSelected(),
                    AgentTestTimeouts.HangGuard)
                .Type("pw")
                .WaitUntil(_ => state.InputText == "/pw", AgentTestTimeouts.HangGuard)
                .Build().ApplyAsync(terminal).WithAgentHangGuard();
        });
    }

    [Fact]
    public async Task RightArrowCompletesAtEndButMovesCursorInsideInput()
    {
        await WithEditor(async (terminal, app, state, _) =>
        {
            await Sequence().Type("/p")
                .WaitUntil(_ => state.InputText == "/p", AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.W)
                .WaitUntil(_ => state.InputText == "/pw", AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.LeftArrow)
                .WaitUntil(_ => state.CursorPos == 2, AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => state.CursorPos == 3, AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => state.InputText == "/pwd ", AgentTestTimeouts.HangGuard)
                .Build().ApplyAsync(terminal);
            Assert.IsType<TextBoxNode>(app.FocusedNode);
        });
    }

    [Fact]
    public async Task TypingWhileBrowsingSuggestionsReturnsToInput()
    {
        await WithEditor(async (terminal, app, state, _) =>
        {
            await Sequence().Type("/p")
                .WaitUntil(_ => state.InputText == "/p", AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.DownArrow)
                .WaitUntil(_ => app.FocusedNode is ListNode<Suggestion>, AgentTestTimeouts.HangGuard)
                .Type("w")
                .WaitUntil(_ => app.FocusedNode is TextBoxNode && state.InputText == "/pw",
                    AgentTestTimeouts.HangGuard)
                .Build().ApplyAsync(terminal).WithAgentHangGuard();
        });
    }

    [Fact]
    public async Task HistoryAndMultilineInputRemainEditable()
    {
        await WithEditor(async (terminal, app, state, history) =>
        {
            history.GetHistory(ShellMode.Azure).Add("zz-history");
            await Sequence().Key(Hex1bKey.UpArrow)
                .WaitUntil(_ => state.InputText == "zz-history", AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.DownArrow)
                .WaitUntil(_ => state.InputText == "", AgentTestTimeouts.HangGuard)
                .Enter()
                .Key(Hex1bKey.Enter, Hex1bModifiers.Control)
                .Type("zz")
                .WaitUntil(_ => state.InputText == "\nzz", AgentTestTimeouts.HangGuard)
                .Key(Hex1bKey.LeftArrow)
                .Key(Hex1bKey.RightArrow)
                .WaitUntil(_ => state.CursorPos == 3, AgentTestTimeouts.HangGuard)
                .Build().ApplyAsync(terminal).WithAgentHangGuard();
            Assert.IsType<TextBoxNode>(app.FocusedNode);
            Assert.Equal(2, TuiProgram.ComputeWordWrapLines(state.InputText, 78));
        });
    }

    private static Hex1bTerminalInputSequenceBuilder Sequence() => new();

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
        await WithEditor(async (terminal, app, state, _) =>
        {
            if (panel == 1)
                state.AddHistory(new HistoryEntry(HistoryItemType.Info, "Previous output"));
            state.ShowHelp = panel == 2;

            foreach (var text in new[] { "zz", "first\nsecond", new string('z', width + 5), "" })
            {
                foreach (var count in new[] { 0, 1, 8, 12, 24, 0 })
                {
                    var inputRows = TuiProgram.ComputeWordWrapLines(text, width - 2);
                    state.InputText = text;
                    state.Suggestions.Clear();
                    state.Suggestions.AddRange(Enumerable.Range(0, count)
                        .Select(i => new Suggestion($"suggestion-{i}", "test")));
                    app.Invalidate();

                    await Sequence().WaitUntil(_ =>
                    {
                        if (app.FocusedNode is not TextBoxNode editor) return false;
                        var group = Assert.IsType<VStackNode>(editor.Parent!.Parent);
                        return group.Children.OfType<ListNode<Suggestion>>().Single().Bounds.Height
                            == Math.Min(8, count) && editor.Text == text
                            && editor.IsMultiline == (text.Contains('\n') || inputRows > 1);
                    }, AgentTestTimeouts.HangGuard).Build().ApplyAsync(terminal).WithAgentHangGuard();

                    var editor = Assert.IsType<TextBoxNode>(app.FocusedNode);
                    Assert.Equal(inputRows, editor.Bounds.Height);
                    Assert.Equal(height - 2, editor.Bounds.Y + editor.Bounds.Height);
                    var group = Assert.IsType<VStackNode>(editor.Parent!.Parent);
                    var list = group.Children.OfType<ListNode<Suggestion>>().Single();
                    Assert.Equal(editor.Bounds.Y, list.Bounds.Y + list.Bounds.Height);
                    Assert.Equal(Math.Min(8, count) + inputRows, group.Bounds.Height);
                }
            }
        }, width, height);
    }

    private static async Task WithEditor(
        Func<Hex1bTerminal, Hex1bApp, AppState, HistoryService, Task> test,
        int width = 80, int height = 24)
    {
        using var arm = new ArmClient(Mock.Of<IAuthService>());
        var history = new HistoryService();
        var state = new AppState(new AzureVfs(arm, Mock.Of<IProfileService>()), history,
            new ProcessService(), new PromptService(Mock.Of<IProfileService>(), Mock.Of<IPersistenceStore>()));
        history.GetHistory(ShellMode.Azure).Clear();
        Hex1bApp? app = null;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless().WithDimensions(width, height)
            .WithHex1bApp(_ => { }, builtApp =>
            {
                app = builtApp;
                state.Invalidate = () => builtApp.Invalidate();
                state.RequestStop = () => builtApp.RequestStop();
                return ctx => TuiProgram.BuildUI(ctx, builtApp, state, width);
            }).Build();
        var runTask = terminal.RunAsync();
        try
        {
            await Sequence().WaitUntil(_ => app!.FocusedNode is TextBoxNode, AgentTestTimeouts.HangGuard)
                .Build().ApplyAsync(terminal).WithAgentHangGuard();
            try
            {
                await test(terminal, app!, state, history);
            }
            catch (TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"Input='{state.InputText}', selected={state.SelectedSuggestion}, focus={app!.FocusedNode?.GetType().Name}, " +
                    $"cursor={state.CursorPos}", ex);
            }
        }
        finally
        {
            app!.RequestStop();
            await runTask.WithAgentHangGuard();
        }
    }
}
