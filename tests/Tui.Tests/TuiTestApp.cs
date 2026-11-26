// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Tui;
using Core.Tui.Azure;
using Core.Tui.Services;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Widgets;
using Moq;
using TuiProgram = Core.Tui.Program;

namespace Tui.Tests;

/// <summary>
/// Runs the real TUI (<see cref="TuiProgram.BuildUI"/>) in a headless Hex1b terminal and exposes a
/// <see cref="Hex1bTerminalAutomator"/> for driving it. Azure and profile services are mocked, so no
/// network or sign-in is needed, and command history uses a temporary directory rather than the
/// user's profile.
/// </summary>
internal sealed class TuiTestApp : IAsyncDisposable
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly Func<Hex1bApp> _app;
    private readonly ArmClient _arm;
    private readonly Task _runTask;
    private readonly string _historyDir;

    private TuiTestApp(Hex1bTerminal terminal, Func<Hex1bApp> app, AppState state, HistoryService history,
        string historyDir, ArmClient arm, int width, int height)
    {
        _historyDir = historyDir;
        Terminal = terminal;
        _app = app;
        State = state;
        History = history;
        _arm = arm;
        Width = width;
        Height = height;
        Automator = new Hex1bTerminalAutomator(terminal, DefaultTimeout);
        _runTask = terminal.RunAsync();
    }

    public Hex1bTerminal Terminal { get; }
    public Hex1bTerminalAutomator Automator { get; }
    public Hex1bApp App => _app();
    public AppState State { get; }
    public HistoryService History { get; }
    public int Width { get; }
    public int Height { get; }

    /// <summary>
    /// Starts the TUI and waits until the command input has focus. <paramref name="configure"/> runs
    /// before the first frame, so state set there needs no rebuild.
    /// </summary>
    public static async Task<TuiTestApp> StartAsync(int width = 80, int height = 24,
        Action<AppState, HistoryService>? configure = null)
    {
        var arm = new ArmClient(Mock.Of<IAuthService>());
        var historyDir = Directory.CreateTempSubdirectory("azclips-tui-tests-").FullName;
        var history = new HistoryService(historyDir);
        var state = CreateState(arm, history);
        configure?.Invoke(state, history);

        Hex1bApp? app = null;
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithHeadless().WithDimensions(width, height)
            .WithHex1bApp(_ => { }, builtApp =>
            {
                app = builtApp;
                state.Invalidate = () => builtApp.Invalidate();
                state.RequestStop = () => builtApp.RequestStop();
                return ctx => TuiProgram.BuildUI(ctx, builtApp, state, width);
            }).Build();

        var tui = new TuiTestApp(terminal, () => app!, state, history, historyDir, arm, width, height);
        await tui.Automator.WaitUntilAsync(_ => app?.FocusedNode is TextBoxNode,
            description: "command input to receive focus");
        return tui;
    }

    /// <summary>
    /// Creates app state with mocked Azure and profile services, for hosting individual widgets.
    /// </summary>
    public static AppState CreateState(ArmClient arm, HistoryService history) =>
        new(new AzureVfs(arm, Mock.Of<IProfileService>()), history, new ProcessService(),
            new PromptService(Mock.Of<IProfileService>(), Mock.Of<IPersistenceStore>()));

    /// <summary>
    /// Waits for an app-state condition. Re-renders while waiting, because state changed outside
    /// the input loop (or a panel swap that moves focus) only settles on a later frame.
    /// </summary>
    public Task WaitUntilAsync(Func<bool> condition, string description) =>
        Automator.WaitUntilAsync(_ =>
        {
            if (condition()) return true;
            App.Invalidate();
            return false;
        }, description: description);

    public async ValueTask DisposeAsync()
    {
        _app().RequestStop();
        await _runTask.WaitAsync(DefaultTimeout);
        await Terminal.DisposeAsync();
        _arm.Dispose();
        try { Directory.Delete(_historyDir, recursive: true); }
        catch (IOException) { } // A fire-and-forget history save may still hold the file.
    }
}
