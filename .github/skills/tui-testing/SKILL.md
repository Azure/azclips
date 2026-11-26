---
name: tui-testing
description: Write, update, or run functional tests for the azclips terminal UI (TUI/Core.Tui.csproj) in tests/Tui.Tests using headless Hex1b terminals and Hex1bTerminalAutomator.
---

# TUI testing

TUI tests live in `tests/Tui.Tests`. That project references `TUI/Core.Tui.csproj`, which grants it `InternalsVisibleTo`. Run them with:

```bash
dotnet test tests/Tui.Tests/Tui.Tests.csproj
```

## Test styles

1. **Pure logic.** Plain xUnit tests that need no terminal (see `ClassifyLineTests`).
2. **Whole app.** `await using var tui = await TuiTestApp.StartAsync(width, height, configure: s => ...)` starts `TuiProgram.BuildUI` in a headless terminal and waits until the input TextBox has focus. Use `tui.Automator` for input and `tui.State` and `tui.History` for assertions (see `SuggestionEditorTests`).
3. **Single widget.** Build an `AppState` with `TuiTestApp.CreateState(arm, new HistoryService(tempDir))` and host only the widget with `Hex1bTerminal.CreateBuilder().WithHeadless().WithDimensions(w, h).WithHex1bApp(...)`. Drive it with a `Hex1bTerminalAutomator` (see `SuggestionRendererTests`).

## Rules

- Drive all input through the automator: `TypeAsync`, `EnterAsync`, `TabAsync`, `EscapeAsync`, `UpAsync`/`DownAsync`/`LeftAsync`/`RightAsync`, and `KeyAsync(key, modifiers)`. Don't write raw escape sequences.
- Never `Task.Delay`/sleep. Wait on observable state:
  - Screen text: `Automator.WaitUntilTextAsync` / `WaitUntilNoTextAsync`.
  - App state: `tui.WaitUntilAsync(() => condition, "description")`. It invalidates the app while the condition is false, so state changes that don't trigger a render are still observed.
  - Always pass a description. On timeout, `Hex1bAutomationException` reports it along with the step history and a terminal snapshot.
- Don't wrap TUI tests in `WithAgentHangGuard`. Automator waits use `TuiTestApp.DefaultTimeout`, and shutdown is bounded by the same timeout.
- Set the initial state with the `configure` callback instead of mutating state after startup.
- Keep tests isolated and offline. `TuiTestApp` uses a temporary history directory, so never touch the user's real `~/.azclips` history. Mock `IAuthService` and avoid network calls.
- For layout, assert node bounds (e.g. `TextBoxNode`, `ListNode<T>`) or `CreateSnapshot().GetNonEmptyLines()` after a wait. Cover layout regressions with a `[Theory]` over terminal sizes and suggestion counts.
- Text can be clipped by the terminal width. Wait for text that actually fits on screen.

## Manual PTY verification

Some behavior, such as real terminal escape handling, is worth checking in a real pseudo-terminal. Build first, then drive `dotnet artifacts/Debug/Core.Tui/Core.Tui.dll` with `TERM=xterm-256color` from Python `pexpect` and render the screen with `pyte`. This uses the real history files, so back them up first.
