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
        RegisterCommand(new ExitCommand());

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
            VfsReady = await Vfs.TryAuthenticateAsync();
            if (!VfsReady) return;

            var (tenantId, subId) = await ResolveAzureContextAsync();
            if (!string.IsNullOrEmpty(tenantId))
                await Vfs.SetInitialPathAsync(tenantId, subId);

            RefreshPrompt();
            Invalidate?.Invoke();
        }
        catch
        {
            VfsReady = false;
        }
    }

    /// <summary>
    /// Resolve the current Azure tenant/subscription by trying:
    ///   1. az account show (lightweight CLI)
    ///   2. Get-AzContext (PowerShell module)
    ///   3. context.json (cached fallback)
    /// Updates context.json when a live source succeeds.
    /// </summary>
    private async Task<(string? tenantId, string? subscriptionId)> ResolveAzureContextAsync()
    {
        // 1. Try az account show
        var result = await RunCliJsonAsync("az", "account show -o json");
        if (result != null)
        {
            var tenantId = result.Value.TryGetProperty("tenantId", out var t) ? t.GetString() : null;
            var subId = result.Value.TryGetProperty("id", out var s) ? s.GetString() : null;
            if (!string.IsNullOrEmpty(tenantId))
            {
                await SaveContextAsync(tenantId, subId);
                return (tenantId, subId);
            }
        }

        // 2. Try Get-AzContext (PowerShell)
        result = await RunCliJsonAsync("pwsh",
            "-NoProfile -Command \"Get-AzContext | Select-Object -Property @{N='tenantId';E={$_.Tenant.Id}}, @{N='id';E={$_.Subscription.Id}} | ConvertTo-Json\"");
        if (result != null)
        {
            var tenantId = result.Value.TryGetProperty("tenantId", out var t) ? t.GetString() : null;
            var subId = result.Value.TryGetProperty("id", out var s) ? s.GetString() : null;
            if (!string.IsNullOrEmpty(tenantId))
            {
                await SaveContextAsync(tenantId, subId);
                return (tenantId, subId);
            }
        }

        // 3. Fall back to context.json
        if (File.Exists(ContextFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(ContextFilePath);
                using var doc = JsonDocument.Parse(json);
                var tenantId = doc.RootElement.TryGetProperty("TenantId", out var t) ? t.GetString() : null;
                var subId = doc.RootElement.TryGetProperty("SubscriptionId", out var s) ? s.GetString() : null;
                return (tenantId, subId);
            }
            catch { }
        }

        return (null, null);
    }

    private static async Task<JsonElement?> RunCliJsonAsync(string fileName, string arguments)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return null;

            using var doc = JsonDocument.Parse(output);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static async Task SaveContextAsync(string? tenantId, string? subscriptionId)
    {
        try
        {
            Directory.CreateDirectory(CirrusDir);
            var json = JsonSerializer.Serialize(new { TenantId = tenantId, SubscriptionId = subscriptionId });
            await File.WriteAllTextAsync(ContextFilePath, json);
        }
        catch { }
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
            AddHistory(new HistoryEntry(HistoryItemType.User, $"{Prompt}azv2 {command}"));
            _ = _processService.RunCliAsync(command, OnProcessLine, OnProcessDone);
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
