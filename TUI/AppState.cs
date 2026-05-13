using Core.Abstractions;
using Core.Tui.Azure;
using Core.Tui.Commands;
using Core.Tui.Rendering;
using Core.Tui.Services;

namespace Core.Tui;

/// <summary>
/// Application state coordinator — delegates to HistoryService, ProcessService,
/// PromptService, and CompletionService.
/// </summary>
class AppState
{
    // Azure Virtual File System
    public AzureVfs Vfs { get; }
    public bool VfsReady { get; private set; }

    // Shell mode
    public ShellMode Mode { get; private set; } = ShellMode.Azure;
    public string LocalCwd { get; private set; } = Directory.GetCurrentDirectory();

    // UI state
    public string InputText { get; set; } = "";
    public int CursorPos { get; set; }
    private readonly List<HistoryEntry> _history = new();
    private readonly object _historyLock = new();
    public List<Suggestion> Suggestions { get; private set; } = new();
    public int SelectedSuggestion { get; set; }
    public int OutputScrollOffset { get; set; }
    public string Prompt { get; private set; } = "/ > ";
    public bool ShowHelp { get; set; }
    public string GitBranch { get; private set; } = "";
    public bool IsRunning => _processService.IsRunning;
    public int ElapsedSeconds => _processService.ElapsedSeconds;
    public Action? Invalidate { get; set; }
    public Action? RequestStop { get; set; }

    // ── Thread-safe History access ────────────────────────────────────

    public void AddHistory(HistoryEntry entry)
    {
        lock (_historyLock) _history.Add(entry);
    }

    public void ClearHistory()
    {
        lock (_historyLock) _history.Clear();
    }

    public int HistoryCount
    {
        get { lock (_historyLock) return _history.Count; }
    }

    /// <summary>
    /// Returns a snapshot of the history list for safe iteration during rendering.
    /// </summary>
    public List<HistoryEntry> GetHistorySnapshot()
    {
        lock (_historyLock) return new List<HistoryEntry>(_history);
    }

    // Services
    private readonly HistoryService _historyService;
    private readonly ProcessService _processService;
    private readonly PromptService _promptService;
    private readonly CompletionService _completionService;

    // Command registry
    private readonly Dictionary<string, ISlashCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public AppState(AzureVfs vfs, HistoryService historyService, ProcessService processService, PromptService promptService)
    {
        Vfs = vfs;
        _historyService = historyService;
        _processService = processService;
        _promptService = promptService;

        RegisterCommand(new CdCommand());
        RegisterCommand(new LsCommand());
        RegisterCommand(new PwdCommand());
        RegisterCommand(new ModeCommand(ToggleMode, () => Mode));
        RegisterCommand(new RefreshCommand());
        RegisterCommand(new CopyCommand());
        RegisterCommand(new ExitCommand());

        _completionService = new CompletionService(_commands);

        _historyService.Load();
        RefreshPrompt();
        GitBranch = PromptService.GetGitBranch();
    }

    private void RegisterCommand(ISlashCommand cmd) => _commands[cmd.Verb] = cmd;

    // ── VFS initialization ───────────────────────────────────────────

    public async Task InitVfsAsync()
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            VfsReady = await Vfs.TryAuthenticateAsync();
            Console.Error.WriteLine($"[startup]   TryAuthenticateAsync: {sw.ElapsedMilliseconds}ms");
            if (!VfsReady)
            {
                Invalidate?.Invoke();
                return;
            }

            sw.Restart();
            await Vfs.SetInitialPathAsync();
            Console.Error.WriteLine($"[startup]   SetInitialPathAsync: {sw.ElapsedMilliseconds}ms");
            RefreshPrompt();
            Invalidate?.Invoke();
        }
        catch (Exception ex)
        {
            VfsReady = false;
            AddHistory(new HistoryEntry(HistoryItemType.Error, $"Azure init failed: {ex.Message}"));
            Invalidate?.Invoke();
        }
    }

    // ── Scroll ───────────────────────────────────────────────────────

    public void ScrollOutput(int lines)
    {
        var totalLines = HistoryCount;
        var prev = OutputScrollOffset;
        OutputScrollOffset = Math.Clamp(
            OutputScrollOffset + lines,
            0,
            Math.Max(0, totalLines - 1));
        if (OutputScrollOffset != prev)
            Invalidate?.Invoke();
    }

    // ── History navigation ───────────────────────────────────────────

    public void NavigateHistory(int direction)
    {
        var result = _historyService.Navigate(direction, Mode);
        if (result != null)
        {
            InputText = result;
            CursorPos = result.Length;
        }
    }

    // ── Mode toggle ──────────────────────────────────────────────────

    public void ToggleMode()
    {
        Mode = Mode == ShellMode.Azure ? ShellMode.Local : ShellMode.Azure;
        RefreshPrompt();
        UpdateSuggestions();
        Invalidate?.Invoke();
    }

    // ── Prompt ───────────────────────────────────────────────────────

    public void RefreshPrompt()
    {
        Prompt = _promptService.BuildPrompt(Mode, LocalCwd, VfsReady, Vfs);
    }

    public void RefreshGitBranch()
    {
        GitBranch = PromptService.GetGitBranch();
    }

    // ── Suggestions (delegated to CompletionService) ─────────────────

    public void UpdateSuggestions()
    {
        var trimmed = InputText.TrimStart();

        if (trimmed.StartsWith("/", StringComparison.OrdinalIgnoreCase)
            && _completionService.TryStartSlashCommandCompletion(trimmed, BuildCommandContext(), OnAsyncSuggestions))
            return;

        var result = _completionService.GetSuggestions(
            InputText, Mode, _historyService.GetHistory(Mode), BuildCommandContext(), OnAsyncSuggestions);

        if (result != null)
        {
            Suggestions = result;
            SelectedSuggestion = 0;
        }
    }

    private void OnAsyncSuggestions(List<Suggestion> suggestions)
    {
        Suggestions = suggestions;
        SelectedSuggestion = 0;
        Invalidate?.Invoke();
    }

    public void AcceptSuggestion()
    {
        if (Suggestions.Count == 0 || SelectedSuggestion >= Suggestions.Count) return;

        var suggestion = Suggestions[SelectedSuggestion].Text;
        InputText = _completionService.AcceptSuggestion(InputText, suggestion);
        CursorPos = InputText.Length;
        UpdateSuggestions();
    }

    // ── Command execution ────────────────────────────────────────────

    public void ExecuteCommand()
    {
        var command = InputText.Trim();
        if (string.IsNullOrEmpty(command)) return;
        if (IsRunning) return;

        _historyService.Record(command, Mode);

        // Slash commands
        if (command.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        {
            if (TryHandleSlashCommand(command[1..]))
            {
                InputText = "";
                CursorPos = 0;
                OutputScrollOffset = 0;
                UpdateSuggestions();
                return;
            }
            AddHistory(new HistoryEntry(HistoryItemType.Error, $"Unknown command: {command}"));
            AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
            Invalidate?.Invoke();
            InputText = "";
            CursorPos = 0;
            OutputScrollOffset = 0;
            UpdateSuggestions();
            return;
        }

        InputText = "";
        CursorPos = 0;
        OutputScrollOffset = 0;
        UpdateSuggestions();

        if (Mode == ShellMode.Local)
        {
            AddHistory(new HistoryEntry(HistoryItemType.User, $"{Prompt}{command}"));
            _ = _processService.RunLocalAsync(command, LocalCwd, OnProcessLine, OnProcessDone);
        }
        else
        {
            var cliArgs = command;
            // Detect login commands (bare "login" or "login --tenant-id ..." etc.)
            if (command.Equals("login", StringComparison.OrdinalIgnoreCase) ||
                command.StartsWith("login ", StringComparison.OrdinalIgnoreCase))
            {
                RunLoginAsync(command);
                return;
            }
            AddHistory(new HistoryEntry(HistoryItemType.User, $"{Prompt}azv2 {cliArgs}"));
            _ = _processService.RunCliAsync(cliArgs, OnProcessLine, OnProcessDone);
        }
    }

    private void RunLoginAsync(string command)
    {
        AddHistory(new HistoryEntry(HistoryItemType.User, $"{Prompt}azv2 {command}"));
        _ = _processService.RunCliAsync(command, OnProcessLine, OnLoginDone);
    }

    private void OnLoginDone(ProcessResult result)
    {
        AddHistory(new HistoryEntry(HistoryItemType.Info, $"[login-done] exitCode={result.ExitCode}"));
        OnProcessDone(result);
        if (result.ExitCode == 0)
            _ = ReauthenticateAsync();
    }

    private async Task ReauthenticateAsync()
    {
        AddHistory(new HistoryEntry(HistoryItemType.Info, "[re-auth] starting..."));
        Invalidate?.Invoke();
        try
        {
            VfsReady = await Vfs.TryAuthenticateAsync();
            if (!VfsReady)
            {
                AddHistory(new HistoryEntry(HistoryItemType.Error, "[re-auth] TryAuthenticateAsync returned false"));
                Invalidate?.Invoke();
                return;
            }

            try
            {
                await Vfs.SetInitialPathAsync();
            }
            catch (Exception ex)
            {
                // Path setup failed but auth is still valid
                AddHistory(new HistoryEntry(HistoryItemType.Info, $"[re-auth] SetInitialPath failed: {ex.Message}"));
            }

            AddHistory(new HistoryEntry(HistoryItemType.Info, "[re-auth] success, VfsReady=true"));
            RefreshPrompt();
            Invalidate?.Invoke();
        }
        catch (Exception ex)
        {
            VfsReady = false;
            AddHistory(new HistoryEntry(HistoryItemType.Error, $"[re-auth] exception: {ex.Message}"));
            Invalidate?.Invoke();
        }
    }

    /// <summary>
    /// Cancel the currently running process (Ctrl+C / Escape).
    /// </summary>
    public void CancelRunning() => _processService.Cancel();

    private void OnProcessLine(HistoryItemType type, string text)
    {
        AddHistory(new HistoryEntry(type, text));
        OutputScrollOffset = 0;
        Invalidate?.Invoke();
    }

    private void OnProcessDone(ProcessResult result)
    {
        if (result.ExitCode == 0)
        {
            AddHistory(new HistoryEntry(HistoryItemType.Success, "done"));
        }
        else if (result.ExitCode != -1) // -1 = exception already reported
        {
            foreach (var line in result.StderrLines)
                AddHistory(new HistoryEntry(HistoryItemType.Error, line));
            AddHistory(new HistoryEntry(HistoryItemType.Info, $"exit code {result.ExitCode}"));
        }

        AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
        OutputScrollOffset = 0;
        RefreshPrompt();
        RefreshGitBranch();
        Invalidate?.Invoke();
    }

    // ── Slash command dispatch ────────────────────────────────────────

    private bool TryHandleSlashCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            var verbs = string.Join(", ", _commands.Keys.Select(k => "/" + k));
            AddHistory(new HistoryEntry(HistoryItemType.Info, $"Available slash commands: {verbs}"));
            AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
            Invalidate?.Invoke();
            return true;
        }

        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var verb = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1].Trim() : null;

        if (_commands.TryGetValue(verb, out var cmd))
            return cmd.Execute(arg, BuildCommandContext());

        return false;
    }

    private CommandContext BuildCommandContext() => new()
    {
        Prompt = Prompt,
        AddHistory = AddHistory,
        GetHistorySnapshot = GetHistorySnapshot,
        Vfs = Vfs,
        VfsReady = VfsReady,
        Mode = Mode,
        LocalCwd = LocalCwd,
        Invalidate = Invalidate,
        RequestStop = RequestStop,
        RefreshPrompt = RefreshPrompt,
        SetLocalCwd = cwd => LocalCwd = cwd,
        RunVfsAsync = RunVfsAsync,
    };

    private async Task RunVfsAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AddHistory(new HistoryEntry(HistoryItemType.Error, ex.Message));
        }
        AddHistory(new HistoryEntry(HistoryItemType.Separator, ""));
        OutputScrollOffset = 0;
        Invalidate?.Invoke();
    }
}
