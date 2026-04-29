using System.Text.Json;
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
    private static readonly string CirrusDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
    private static readonly string ContextFilePath = Path.Combine(CirrusDir, "context.json");

    // Azure Virtual File System
    private readonly ArmClient _armClient = new();
    public AzureVfs Vfs { get; }
    public bool VfsReady { get; private set; }

    // Shell mode
    public ShellMode Mode { get; private set; } = ShellMode.Azure;
    public string LocalCwd { get; private set; } = Directory.GetCurrentDirectory();

    // UI state
    public string InputText { get; set; } = "";
    public List<HistoryEntry> History { get; } = new();
    public List<Suggestion> Suggestions { get; private set; } = new();
    public int SelectedSuggestion { get; set; }
    public int OutputScrollOffset { get; set; }
    public string Prompt { get; private set; } = "/ > ";
    public bool ShowHelp { get; set; }
    public string GitBranch { get; private set; } = "";
    public bool IsRunning => _processService.IsRunning;
    public int ElapsedSeconds => _processService.ElapsedSeconds;
    public Action? Invalidate { get; set; }
    public bool FocusTextBox { get; set; } = true;

    // Services
    private readonly HistoryService _historyService = new();
    private readonly ProcessService _processService = new();
    private readonly PromptService _promptService = new();
    private readonly CompletionService _completionService;

    // Command registry
    private readonly Dictionary<string, ISlashCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public AppState()
    {
        Vfs = new AzureVfs(_armClient);

        RegisterCommand(new CdCommand());
        RegisterCommand(new LsCommand());
        RegisterCommand(new PwdCommand());
        RegisterCommand(new ModeCommand(ToggleMode, () => Mode));
        RegisterCommand(new RefreshCommand());
        RegisterCommand(new CopyCommand());

        _completionService = new CompletionService(_commands);

        _historyService.Load();
        RefreshPrompt();
        GitBranch = PromptService.GetGitBranch();
        _ = InitVfsAsync();
    }

    private void RegisterCommand(ISlashCommand cmd) => _commands[cmd.Verb] = cmd;

    // ── VFS initialization ───────────────────────────────────────────

    private async Task InitVfsAsync()
    {
        try
        {
            string? tenantId = null;
            string? subId = null;

            if (File.Exists(ContextFilePath))
            {
                var json = await File.ReadAllTextAsync(ContextFilePath);
                using var doc = JsonDocument.Parse(json);
                tenantId = doc.RootElement.TryGetProperty("TenantId", out var t) ? t.GetString() : null;
                subId = doc.RootElement.TryGetProperty("SubscriptionId", out var s) ? s.GetString() : null;
            }

            // Authenticate with the correct tenant so subscription lookups work
            VfsReady = await Vfs.TryAuthenticateAsync(tenantId);
            if (!VfsReady) return;

            await Vfs.SetInitialPathAsync(tenantId, subId);

            RefreshPrompt();
            Invalidate?.Invoke();
        }
        catch
        {
            VfsReady = false;
        }
    }

    // ── Scroll ───────────────────────────────────────────────────────

    public void ScrollOutput(int lines)
    {
        var totalLines = HistoryRenderer.Render(History).Count;
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
            InputText = result;
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
                OutputScrollOffset = 0;
                UpdateSuggestions();
                return;
            }
            History.Add(new HistoryEntry(HistoryItemType.Error, $"Unknown command: {command}"));
            History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
            Invalidate?.Invoke();
            InputText = "";
            OutputScrollOffset = 0;
            UpdateSuggestions();
            return;
        }

        InputText = "";
        OutputScrollOffset = 0;
        UpdateSuggestions();

        if (Mode == ShellMode.Local)
        {
            History.Add(new HistoryEntry(HistoryItemType.User, command));
            _ = _processService.RunLocalAsync(command, LocalCwd, OnProcessLine, OnProcessDone);
        }
        else
        {
            History.Add(new HistoryEntry(HistoryItemType.User, $"azv2 {command}"));
            _ = _processService.RunCliAsync(command, OnProcessLine, OnProcessDone);
        }
    }

    private void OnProcessLine(HistoryItemType type, string text)
    {
        History.Add(new HistoryEntry(type, text));
        OutputScrollOffset = 0;
        Invalidate?.Invoke();
    }

    private void OnProcessDone(ProcessResult result)
    {
        if (result.ExitCode == 0)
        {
            History.Add(new HistoryEntry(HistoryItemType.Success, "done"));
        }
        else if (result.ExitCode != -1) // -1 = exception already reported
        {
            foreach (var line in result.StderrLines)
                History.Add(new HistoryEntry(HistoryItemType.Error, line));
            History.Add(new HistoryEntry(HistoryItemType.Info, $"exit code {result.ExitCode}"));
        }

        History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
        OutputScrollOffset = 0;
        FocusTextBox = true;
        RefreshPrompt();
        Invalidate?.Invoke();
    }

    // ── Slash command dispatch ────────────────────────────────────────

    private bool TryHandleSlashCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            var verbs = string.Join(", ", _commands.Keys.Select(k => "/" + k));
            History.Add(new HistoryEntry(HistoryItemType.Info, $"Available slash commands: {verbs}"));
            History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
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
        History = History,
        Vfs = Vfs,
        VfsReady = VfsReady,
        Mode = Mode,
        LocalCwd = LocalCwd,
        Invalidate = Invalidate,
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
            History.Add(new HistoryEntry(HistoryItemType.Error, ex.Message));
        }
        History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
        OutputScrollOffset = 0;
        Invalidate?.Invoke();
    }
}
