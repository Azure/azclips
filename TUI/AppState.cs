using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Core.Tui.Azure;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Tui;

enum ShellMode { Azure, Local }

/// <summary>
/// Application state: manages input text, suggestions, output, and CLI execution.
/// </summary>
class AppState
{
    private static readonly string CliPath = FindCliPath();
    private static readonly string CirrusDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
    private static readonly string ContextFilePath = Path.Combine(CirrusDir, "context.json");
    private static readonly string TuiHistoryPath = Path.Combine(CirrusDir, "tui_history.txt");

    // Cached ARM-resolved names (keyed by ID)
    private string? _cachedTenantId;
    private string? _cachedSubId;
    private string? _cachedTenantName;
    private string? _cachedSubName;

    // Azure Virtual File System
    private readonly ArmClient _armClient = new();
    public AzureVfs Vfs { get; }
    public bool VfsReady { get; private set; }

    // Shell mode: Azure VFS vs Local filesystem
    public ShellMode Mode { get; private set; } = ShellMode.Azure;
    public string LocalCwd { get; private set; } = Directory.GetCurrentDirectory();

    public string InputText { get; set; } = "";
    public List<HistoryEntry> History { get; } = new();
    public List<Suggestion> Suggestions { get; private set; } = new();
    public int SelectedSuggestion { get; set; }
    public int OutputScrollOffset { get; set; }
    public string Prompt { get; private set; } = "/ > ";
    public bool ShowHelp { get; set; }
    public string GitBranch { get; private set; } = "";
    public bool IsRunning { get; private set; }
    public int ElapsedSeconds => _commandStopwatch.IsRunning
        ? (int)_commandStopwatch.Elapsed.TotalSeconds : 0;

    /// <summary>
    /// Callback to trigger Hex1b re-render when streaming output arrives.
    /// Set by Program.cs after creating the Hex1bApp.
    /// </summary>
    public Action? Invalidate { get; set; }

    /// <summary>
    /// When true, Program.cs will call RequestFocus to move focus back to the TextBox.
    /// Set after command completion so the user can immediately type the next command.
    /// </summary>
    public bool FocusTextBox { get; set; } = true;

    // Input history (like shell useInputHistory hook)
    private readonly List<string> _inputHistory = new();
    private int _historyIndex = -1; // -1 = not browsing history
    private Process? _runningProcess;
    private readonly Stopwatch _commandStopwatch = new();

    public AppState()
    {
        Vfs = new AzureVfs(_armClient);
        LoadHistory();
        RefreshPrompt();
        RefreshGitBranch();
        // Try to authenticate VFS silently in background
        _ = InitVfsAsync();
    }

    private async Task InitVfsAsync()
    {
        try
        {
            VfsReady = await Vfs.TryAuthenticateAsync();
            if (!VfsReady) return;

            // Seed VFS path from context.json so the initial position
            // matches the user's current Azure context (tenant + subscription).
            if (File.Exists(ContextFilePath))
            {
                var json = await File.ReadAllTextAsync(ContextFilePath);
                using var doc = JsonDocument.Parse(json);
                var tenantId = doc.RootElement.TryGetProperty("TenantId", out var t) ? t.GetString() : null;
                var subId = doc.RootElement.TryGetProperty("SubscriptionId", out var s) ? s.GetString() : null;
                await Vfs.SetInitialPathAsync(tenantId, subId);
            }

            RefreshPrompt();
            Invalidate?.Invoke();
        }
        catch
        {
            VfsReady = false;
        }
    }

    /// <summary>
    /// Load TUI command history from disk (like PSReadLine).
    /// </summary>
    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(TuiHistoryPath)) return;

            var lines = File.ReadAllLines(TuiHistoryPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    _inputHistory.Add(trimmed);
            }
        }
        catch
        {
            // Silently ignore history load errors
        }
    }

    /// <summary>
    /// Save a command to the TUI history file (like PSReadLine).
    /// </summary>
    private async Task SaveHistoryAsync(string command)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(CirrusDir);

            // Append to history file
            await File.AppendAllTextAsync(TuiHistoryPath, command + Environment.NewLine);
        }
        catch
        {
            // Silently ignore history save errors
        }
    }

    /// <summary>
    /// Scroll output by a number of lines (positive = scroll up, negative = scroll down).
    /// </summary>
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

    /// <summary>
    /// Navigate through input history (like shell useInputHistory hook).
    /// direction: -1 = older, +1 = newer
    /// </summary>
    public void NavigateHistory(int direction)
    {
        if (_inputHistory.Count == 0) return;

        if (direction < 0) // older
        {
            if (_historyIndex < _inputHistory.Count - 1)
            {
                _historyIndex++;
                InputText = _inputHistory[_inputHistory.Count - 1 - _historyIndex];
            }
        }
        else // newer
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                InputText = _inputHistory[_inputHistory.Count - 1 - _historyIndex];
            }
            else if (_historyIndex == 0)
            {
                _historyIndex = -1;
                InputText = "";
            }
        }
    }

    /// <summary>
    /// Get the current git branch name (like shell useGitBranchName hook).
    /// </summary>
    public void RefreshGitBranch()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "branch --show-current",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            GitBranch = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(2000);
        }
        catch
        {
            GitBranch = "";
        }
    }

    public void ToggleMode()
    {
        Mode = Mode == ShellMode.Azure ? ShellMode.Local : ShellMode.Azure;
        RefreshPrompt();
        UpdateSuggestions();
        Invalidate?.Invoke();
    }

    public void RefreshPrompt()
    {
        // Local mode: show local cwd as prompt
        if (Mode == ShellMode.Local)
        {
            var cwd = LocalCwd;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (cwd.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                cwd = "~" + cwd[home.Length..];
            Prompt = $"{cwd} > ";
            return;
        }

        // When VFS is ready, use the VFS current path as the prompt.
        if (VfsReady)
        {
            Prompt = Vfs.CurrentPath == "/" ? "/ > " : $"{Vfs.CurrentPath} > ";
            return;
        }

        // Fallback: try to read tenant/subscription from context.json
        try
        {
            if (!File.Exists(ContextFilePath)) { Prompt = "/ > "; return; }

            var json = File.ReadAllText(ContextFilePath);
            var doc = JsonDocument.Parse(json);
            var tenantId = doc.RootElement.TryGetProperty("TenantId", out var t) ? t.GetString() : null;
            var subId = doc.RootElement.TryGetProperty("SubscriptionId", out var s) ? s.GetString() : null;

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(subId))
            { Prompt = "/ > "; return; }

            if (tenantId == _cachedTenantId && subId == _cachedSubId
                && _cachedTenantName != null && _cachedSubName != null)
            {
                Prompt = $"/{_cachedTenantName}/{_cachedSubName} > ";
                return;
            }

            var (tenantName, subName) = ResolveNamesFromArm(tenantId, subId);
            _cachedTenantId = tenantId;
            _cachedSubId = subId;
            _cachedTenantName = tenantName ?? tenantId[..Math.Min(8, tenantId.Length)];
            _cachedSubName = subName ?? subId[..Math.Min(8, subId.Length)];
            Prompt = $"/{_cachedTenantName}/{_cachedSubName} > ";
        }
        catch
        {
            Prompt = "/ > ";
        }
    }

    private static (string? tenantName, string? subName) ResolveNamesFromArm(string tenantId, string subId)
    {
        try
        {
            var app = PublicClientApplicationBuilder
                .Create("1950a258-227b-4e31-a9cf-717495945fc2")
                .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
                .WithRedirectUri("http://localhost")
                .Build();

            // Attach the same MSAL cache Cirrus uses
            var cacheFile = Path.Combine(CirrusDir, "MSAL.cache.plaintext");
            if (!File.Exists(cacheFile)) return (null, null);

            var storageProps = new StorageCreationPropertiesBuilder("MSAL.cache.plaintext", CirrusDir)
                .WithUnprotectedFile()
                .Build();
            var cacheHelper = MsalCacheHelper.CreateAsync(storageProps).GetAwaiter().GetResult();
            cacheHelper.RegisterCache(app.UserTokenCache);

            // Acquire token silently
            var accounts = app.GetAccountsAsync().GetAwaiter().GetResult();
            var account = accounts.FirstOrDefault();
            if (account == null) return (null, null);

            var result = app.AcquireTokenSilent(
                ["https://management.core.windows.net//.default"], account)
                .WithForceRefresh(false)
                .ExecuteAsync().GetAwaiter().GetResult();

            // Call ARM
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            var armBase = "https://management.azure.com";

            string? subName = null;
            string? tenantName = null;

            // Subscription name
            try
            {
                var subJson = http.GetStringAsync($"{armBase}/subscriptions/{subId}?api-version=2022-12-01").GetAwaiter().GetResult();
                using var subDoc = JsonDocument.Parse(subJson);
                if (subDoc.RootElement.TryGetProperty("displayName", out var sn))
                    subName = sn.GetString();
            }
            catch { }

            // Tenant name
            try
            {
                var tenantsJson = http.GetStringAsync($"{armBase}/tenants?api-version=2022-12-01").GetAwaiter().GetResult();
                using var tenantsDoc = JsonDocument.Parse(tenantsJson);
                if (tenantsDoc.RootElement.TryGetProperty("value", out var tenants))
                {
                    foreach (var tenant in tenants.EnumerateArray())
                    {
                        if (tenant.TryGetProperty("tenantId", out var tid) && tid.GetString() == tenantId)
                        {
                            if (tenant.TryGetProperty("displayName", out var tn))
                                tenantName = tn.GetString();
                            break;
                        }
                    }
                }
            }
            catch { }

            return (tenantName, subName);
        }
        catch
        {
            return (null, null);
        }
    }

    public void UpdateSuggestions()
    {
        // No suggestions when input is empty
        if (string.IsNullOrWhiteSpace(InputText))
        {
            Suggestions = [];
            SelectedSuggestion = 0;
            return;
        }

        var trimmed = InputText.TrimStart();

        // Handle slash commands (/cd, /ls, /pwd, /mode, /tree, /refresh)
        if (trimmed.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        {
            TryGetSlashCommandSuggestions(trimmed);
            return;
        }

        // Local mode: provide local filesystem completions for cd/ls
        if (Mode == ShellMode.Local && TryStartLocalCompletion(trimmed))
            return;

        // Azure mode: provide VFS path completions for cd/ls
        if (Mode == ShellMode.Azure && VfsReady && TryStartVfsCompletion(trimmed))
            return;

        Suggestions = CommandTree.GetSuggestions(InputText);
        SelectedSuggestion = 0;
    }

    private void TryGetSlashCommandSuggestions(string input)
    {
        // Extract the partial command after /
        var slashIndex = input.IndexOf('/');
        var partial = slashIndex >= 0 ? input[(slashIndex + 1)..].TrimStart() : "";

        // Available slash commands
        var allCommands = new[]
        {
            new Suggestion("cd", "Navigate to path"),
            new Suggestion("ls", "List contents"),
            new Suggestion("pwd", "Show current path"),
            new Suggestion("mode", "Toggle Azure/Local mode"),
            new Suggestion("refresh", "Refresh cache"),
        };

        // Filter by partial match
        if (string.IsNullOrEmpty(partial))
        {
            Suggestions = allCommands.ToList();
        }
        else
        {
            Suggestions = allCommands
                .Where(s => s.Text.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        SelectedSuggestion = 0;
    }

    private bool TryStartLocalCompletion(string trimmed)
    {
        string? verb = null;
        string partial = "";
        foreach (var v in new[] { "cd", "ls" })
        {
            if (trimmed.StartsWith(v + " ", StringComparison.OrdinalIgnoreCase))
            {
                verb = v;
                partial = trimmed[(v.Length + 1)..].TrimStart();
                break;
            }
        }
        if (verb == null) return false;

        // Resolve partial path relative to LocalCwd
        var basePath = string.IsNullOrEmpty(partial) ? LocalCwd
            : Path.IsPathRooted(partial) ? partial
            : Path.Combine(LocalCwd, partial);

        try
        {
            string searchDir;
            string prefix;
            if (Directory.Exists(basePath))
            {
                searchDir = basePath;
                prefix = "";
            }
            else
            {
                searchDir = Path.GetDirectoryName(basePath) ?? LocalCwd;
                prefix = Path.GetFileName(basePath);
            }

            if (!Directory.Exists(searchDir))
            {
                Suggestions = [];
                SelectedSuggestion = 0;
                return true;
            }

            var entries = new List<Suggestion>();
            foreach (var dir in Directory.GetDirectories(searchDir))
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    entries.Add(new Suggestion(name, "▸ dir"));
            }
            if (verb == "ls")
            {
                foreach (var file in Directory.GetFiles(searchDir))
                {
                    var name = Path.GetFileName(file);
                    if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        entries.Add(new Suggestion(name, "· file"));
                }
            }
            Suggestions = entries;
            SelectedSuggestion = 0;
        }
        catch
        {
            Suggestions = [];
            SelectedSuggestion = 0;
        }
        return true;
    }

    /// <summary>
    /// If input is "cd <partial>" or "ls <partial>", fetch VFS path completions async.
    /// Returns true if a VFS completion was started (suggestions will arrive via Invalidate).
    /// </summary>
    private bool TryStartVfsCompletion(string trimmed)
    {
        // Match: "cd ", "cd foo", "ls ", "ls /ten" etc.
        var isVfsCmd = false;
        string partial = "";
        foreach (var verb in new[] { "cd", "ls" })
        {
            if (trimmed.Equals(verb, StringComparison.OrdinalIgnoreCase)
                || !trimmed.StartsWith(verb + " ", StringComparison.OrdinalIgnoreCase))
                continue;
            isVfsCmd = true;
            partial = trimmed[(verb.Length + 1)..].TrimStart();
            break;
        }
        if (!isVfsCmd) return false;

        // Fire async completion; update Suggestions when done
        _ = FetchVfsCompletionsAsync(partial);
        return true;
    }

    private async Task FetchVfsCompletionsAsync(string partial)
    {
        try
        {
            // If partial is empty, list children of current path
            List<VfsEntry> entries;
            List<string> completions;

            if (string.IsNullOrEmpty(partial))
            {
                entries = await Vfs.LsAsync();
                Suggestions = entries.Select(e =>
                {
                    var icon = e.Kind == VfsEntryKind.Directory ? "▸" : "·";
                    return new Suggestion(e.Name, $"{icon} {e.Detail}");
                }).ToList();
            }
            else
            {
                completions = await Vfs.GetCompletionsAsync(partial);
                Suggestions = completions.Select(c => new Suggestion(c, "Azure resource")).ToList();
            }

            SelectedSuggestion = 0;
            Invalidate?.Invoke();
        }
        catch
        {
            // Silently fall back to static suggestions on error
            Suggestions = CommandTree.GetSuggestions(InputText);
            SelectedSuggestion = 0;
            Invalidate?.Invoke();
        }
    }

    public void AcceptSuggestion()
    {
        if (Suggestions.Count == 0 || SelectedSuggestion >= Suggestions.Count) return;

        var suggestion = Suggestions[SelectedSuggestion].Text;
        var trimmed = InputText.TrimEnd();

        // Handle slash command suggestions (/cd, /ls, etc.)
        if (trimmed.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        {
            // Replace from the / onwards with /suggestion 
            var slashIndex = trimmed.IndexOf('/');
            var before = slashIndex > 0 ? trimmed[..slashIndex] : "";
            InputText = before + "/" + suggestion + " ";
        }
        else if (trimmed.Length > 0 && !InputText.EndsWith(' '))
        {
            // Replace the last partial token or append
            var lastSpace = InputText.LastIndexOf(' ');
            InputText = (lastSpace >= 0 ? InputText[..(lastSpace + 1)] : "") + suggestion + " ";
        }
        else
        {
            InputText = trimmed + (trimmed.Length > 0 ? " " : "") + suggestion + " ";
        }

        UpdateSuggestions();
    }

    public void ExecuteCommand()
    {
        var command = InputText.Trim();
        if (string.IsNullOrEmpty(command)) return;
        if (IsRunning) return; // don't allow concurrent commands

        // Record in input history and save to disk
        _inputHistory.Add(command);
        _historyIndex = -1;
        _ = SaveHistoryAsync(command);

        // Check for slash commands (/cd, /ls, /pwd, /mode, /tree, /refresh)
        if (command.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        {
            if (TryHandleSlashCommand(command[1..])) // strip the leading /
            {
                InputText = "";
                OutputScrollOffset = 0;
                UpdateSuggestions();
                return;
            }
            // Unknown slash command - show error
            History.Add(new HistoryEntry(HistoryItemType.Error, $"Unknown command: {command}"));
            History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
            Invalidate?.Invoke();
            InputText = "";
            OutputScrollOffset = 0;
            UpdateSuggestions();
            return;
        }

        // Azure command (no / prefix) - send to CLI
        History.Add(new HistoryEntry(HistoryItemType.User, $"azv2 {command}"));
        InputText = "";
        OutputScrollOffset = 0;
        UpdateSuggestions();

        // Launch async streaming execution
        IsRunning = true;
        _commandStopwatch.Restart();
        _ = RunCliStreamingAsync(command);
    }

    /// <summary>
    /// Handle slash commands (cd, ls, pwd, mode, tree, refresh).
    /// Input should NOT include the leading /.
    /// Returns true if the command was handled, false if unknown.
    /// </summary>
    private bool TryHandleSlashCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            History.Add(new HistoryEntry(HistoryItemType.Info, "Available slash commands: /cd, /ls, /pwd, /mode, /refresh"));
            History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
            Invalidate?.Invoke();
            return true;
        }

        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var verb = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1].Trim() : null;

        // In Local mode, handle cd/ls/pwd locally
        if (Mode == ShellMode.Local)
        {
            switch (verb)
            {
                case "cd":
                    return HandleLocalCd(arg);
                case "ls":
                    return HandleLocalLs(arg);
                case "pwd":
                    History.Add(new HistoryEntry(HistoryItemType.User, "pwd"));
                    History.Add(new HistoryEntry(HistoryItemType.Output, LocalCwd));
                    History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
                    Invalidate?.Invoke();
                    return true;
                case "mode":
                    ToggleMode();
                    History.Add(new HistoryEntry(HistoryItemType.Info, $"Switched to {Mode} mode"));
                    History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
                    Invalidate?.Invoke();
                    return true;
            }
            return false;
        }

        // Azure mode
        switch (verb)
        {
            case "mode":
                ToggleMode();
                History.Add(new HistoryEntry(HistoryItemType.Info, $"Switched to {Mode} mode"));
                History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
                Invalidate?.Invoke();
                return true;

            case "cd":
                History.Add(new HistoryEntry(HistoryItemType.User, $"cd {arg ?? "/"}"));
                if (!VfsReady)
                {
                    History.Add(new HistoryEntry(HistoryItemType.Error, "Not authenticated. Run 'login' first."));
                    break;
                }
                _ = RunVfsAsync(async () =>
                {
                    var error = await Vfs.CdAsync(arg ?? "/");
                    if (error != null)
                        History.Add(new HistoryEntry(HistoryItemType.Error, error));
                    else
                        History.Add(new HistoryEntry(HistoryItemType.Info, Vfs.CurrentPath));
                    RefreshVfsPrompt();
                });
                return true;

            case "ls":
                History.Add(new HistoryEntry(HistoryItemType.User, $"ls {arg ?? ""}".TrimEnd()));
                if (!VfsReady)
                {
                    History.Add(new HistoryEntry(HistoryItemType.Error, "Not authenticated. Run 'login' first."));
                    break;
                }
                _ = RunVfsAsync(async () =>
                {
                    var entries = await Vfs.LsAsync(arg);
                    if (entries.Count == 0)
                    {
                        History.Add(new HistoryEntry(HistoryItemType.Info, "(empty)"));
                    }
                    else
                    {
                        foreach (var e in entries)
                        {
                            var icon = e.Kind == VfsEntryKind.Directory ? "▸" : "·";
                            History.Add(new HistoryEntry(HistoryItemType.Output, $"  {icon} {e.Name,-40} {e.Detail}"));
                        }
                        History.Add(new HistoryEntry(HistoryItemType.Info, $"{entries.Count} items"));
                    }
                });
                return true;

            case "pwd":
                History.Add(new HistoryEntry(HistoryItemType.User, "pwd"));
                History.Add(new HistoryEntry(HistoryItemType.Output, Vfs.CurrentPath));
                History.Add(new HistoryEntry(HistoryItemType.Info, Vfs.GetResourceId()));
                History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
                Invalidate?.Invoke();
                return true;

            case "refresh":
                History.Add(new HistoryEntry(HistoryItemType.User, $"refresh {arg ?? "all"}"));
                var scope = (arg?.ToLowerInvariant()) switch
                {
                    "tenants" => AzureVfs.RefreshScope.Tenants,
                    "subscriptions" or "subs" => AzureVfs.RefreshScope.Subscriptions,
                    "resourcegroups" or "rgs" => AzureVfs.RefreshScope.ResourceGroups,
                    "resources" => AzureVfs.RefreshScope.Resources,
                    _ => AzureVfs.RefreshScope.All,
                };
                Vfs.Refresh(scope);
                History.Add(new HistoryEntry(HistoryItemType.Success, $"Cache refreshed: {scope}"));
                History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
                Invalidate?.Invoke();
                return true;

            default:
                return false;
        }

        // Fell through a case that wrote an error but didn't return
        History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
        Invalidate?.Invoke();
        return true;
    }

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

    // ── Local filesystem handlers ──

    private bool HandleLocalCd(string? arg)
    {
        History.Add(new HistoryEntry(HistoryItemType.User, $"cd {arg ?? "~"}"));
        var target = arg;
        if (string.IsNullOrEmpty(target) || target == "~")
            target = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        else if (target == "-")
            target = LocalCwd; // no-op for simplicity
        else if (target == "..")
            target = Path.GetDirectoryName(LocalCwd) ?? LocalCwd;
        else if (!Path.IsPathRooted(target))
            target = Path.GetFullPath(Path.Combine(LocalCwd, target));

        if (Directory.Exists(target))
        {
            LocalCwd = Path.GetFullPath(target);
            History.Add(new HistoryEntry(HistoryItemType.Info, LocalCwd));
            RefreshPrompt();
        }
        else
        {
            History.Add(new HistoryEntry(HistoryItemType.Error, $"Directory not found: {target}"));
        }
        History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
        Invalidate?.Invoke();
        return true;
    }

    private bool HandleLocalLs(string? arg)
    {
        History.Add(new HistoryEntry(HistoryItemType.User, $"ls {arg ?? ""}".TrimEnd()));
        var target = string.IsNullOrEmpty(arg) ? LocalCwd
            : Path.IsPathRooted(arg) ? arg
            : Path.GetFullPath(Path.Combine(LocalCwd, arg));

        if (!Directory.Exists(target))
        {
            History.Add(new HistoryEntry(HistoryItemType.Error, $"Directory not found: {target}"));
            History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
            Invalidate?.Invoke();
            return true;
        }

        var count = 0;
        foreach (var dir in Directory.GetDirectories(target))
        {
            History.Add(new HistoryEntry(HistoryItemType.Output, $"  ▸ {Path.GetFileName(dir)}/"));
            count++;
        }
        foreach (var file in Directory.GetFiles(target))
        {
            var info = new FileInfo(file);
            var size = info.Length < 1024 ? $"{info.Length}B" : $"{info.Length / 1024}KB";
            History.Add(new HistoryEntry(HistoryItemType.Output, $"  · {Path.GetFileName(file),-40} {size}"));
            count++;
        }
        History.Add(new HistoryEntry(HistoryItemType.Info, count == 0 ? "(empty)" : $"{count} items"));
        History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
        Invalidate?.Invoke();
        return true;
    }

    private void RefreshVfsPrompt() => RefreshPrompt();

    private async Task RunCliStreamingAsync(string args)
    {
        try
        {
            _runningProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = CliPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            _runningProcess.Start();

            // Stream stdout and stderr concurrently
            var stdoutTask = StreamLinesAsync(_runningProcess.StandardOutput, HistoryItemType.Output);
            var stderrTask = StreamLinesAsync(_runningProcess.StandardError, HistoryItemType.Error);

            await Task.WhenAll(stdoutTask, stderrTask);
            await _runningProcess.WaitForExitAsync();

            var exitCode = _runningProcess.ExitCode;
            if (exitCode == 0)
                History.Add(new HistoryEntry(HistoryItemType.Success, "done"));
            else
                History.Add(new HistoryEntry(HistoryItemType.Info, $"exit code {exitCode}"));
        }
        catch (Exception ex)
        {
            History.Add(new HistoryEntry(HistoryItemType.Error, ex.Message));
        }
        finally
        {
            _runningProcess?.Dispose();
            _runningProcess = null;
            _commandStopwatch.Stop();
            IsRunning = false;
            History.Add(new HistoryEntry(HistoryItemType.Separator, ""));
            OutputScrollOffset = 0;
            FocusTextBox = true;
            RefreshPrompt();
            Invalidate?.Invoke();
        }
    }

    private async Task StreamLinesAsync(StreamReader reader, HistoryItemType type)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            var trimmed = line.TrimEnd('\r');
            if (IsDebugOrVerbose(trimmed)) continue;
            if (type == HistoryItemType.Error && string.IsNullOrWhiteSpace(trimmed)) continue;
            History.Add(new HistoryEntry(type, trimmed));
            OutputScrollOffset = 0; // auto-scroll to bottom on new output
            Invalidate?.Invoke();
        }
    }

    private static bool IsDebugOrVerbose(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("[DEBUG]", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("[VERBOSE]", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindCliPath()
    {
        // Search relative to the TUI executable
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);

        // Walk up to find artifacts/Debug/Core.Cli/azv2.exe
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "artifacts", "Debug", "Core.Cli", "azv2.exe");
            if (File.Exists(candidate)) return candidate;

            // Also check bin/ for published builds
            candidate = Path.Combine(dir.FullName, "bin", "azv2.exe");
            if (File.Exists(candidate)) return candidate;

            dir = dir.Parent;
        }

        // Fallback: assume on PATH
        return "azv2";
    }
}
