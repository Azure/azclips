// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Tui;
using Core.Tui.Azure;
using Core.Tui.Rendering;
using Core.Tui.Services;
using Hex1b;
using Hex1b.Automation;
using Moq;

namespace Tui.Tests;

/// <summary>Hosts <see cref="SuggestionRenderer"/> on its own, without the rest of the TUI.</summary>
public class SuggestionRendererTests
{
    [Fact]
    public async Task SuggestionsRenderAboveInputRow()
    {
        using var arm = new ArmClient(Mock.Of<IAuthService>());
        var historyDir = Directory.CreateTempSubdirectory("azclips-tui-tests-");
        var state = TuiTestApp.CreateState(arm, new HistoryService(historyDir.FullName));
        state.InputText = "/p";
        state.Suggestions.AddRange([new Suggestion("pwd", "Show current path"), new Suggestion("ping", "test")]);

        Hex1bApp? app = null;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless().WithDimensions(40, 6)
            .WithHex1bApp(_ => { }, built =>
            {
                app = built;
                return ctx => ctx.VStack(v => [SuggestionRenderer.Build(v, state, built, 38)]);
            }).Build();
        var run = terminal.RunAsync();
        var auto = new Hex1bTerminalAutomator(terminal, TuiTestApp.DefaultTimeout);

        await auto.WaitUntilTextAsync("> /p");
        var lines = auto.CreateSnapshot().GetNonEmptyLines().ToList();
        Assert.Contains("pwd", lines[0]);
        Assert.Contains("ping", lines[1]);
        Assert.StartsWith("> /p", lines[2].TrimStart());

        app!.RequestStop();
        await run.WaitAsync(TuiTestApp.DefaultTimeout);
        historyDir.Delete(recursive: true);
    }
}
