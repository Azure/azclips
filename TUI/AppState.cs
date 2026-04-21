using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Tui;

/// <summary>
/// Application state: manages input text, suggestions, output, and CLI execution.
/// </summary>
class AppState
{
    private static readonly string CliPath = FindCliPath();
    private static readonly string CirrusDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
    private static readonly string ContextFilePath = Path.Combine(CirrusDir, "context.json");

    // Cached ARM-resolved names (keyed by ID)
    private string? _cachedTenantId;
    private string? _cachedSubId;
    private string? _cachedTenantName;
    private string? _cachedSubName;

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
        RefreshPrompt();
        RefreshGitBranch();
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

    public void RefreshPrompt()
    {
        try
        {
            if (!File.Exists(ContextFilePath)) { Prompt = "/ > "; return; }

            var json = File.ReadAllText(ContextFilePath);
            var doc = JsonDocument.Parse(json);
            var tenantId = doc.RootElement.TryGetProperty("TenantId", out var t) ? t.GetString() : null;
            var subId = doc.RootElement.TryGetProperty("SubscriptionId", out var s) ? s.GetString() : null;

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(subId))
            { Prompt = "/ > "; return; }

            // Use cached names if IDs haven't changed
            if (tenantId == _cachedTenantId && subId == _cachedSubId
                && _cachedTenantName != null && _cachedSubName != null)
            {
                Prompt = $"/{_cachedTenantName}/{_cachedSubName} > ";
                return;
            }

            // Try to resolve names from ARM (best-effort, silent token)
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
        Suggestions = CommandTree.GetSuggestions(InputText);
        SelectedSuggestion = 0;
    }

    public void AcceptSuggestion()
    {
        if (Suggestions.Count == 0 || SelectedSuggestion >= Suggestions.Count) return;

        var suggestion = Suggestions[SelectedSuggestion].Text;
        var trimmed = InputText.TrimEnd();

        // Replace the last partial token or append
        if (trimmed.Length > 0 && !InputText.EndsWith(' '))
        {
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

        // Record in input history
        _inputHistory.Add(command);
        _historyIndex = -1;

        History.Add(new HistoryEntry(HistoryItemType.User, $"azv2 {command}"));
        InputText = "";
        OutputScrollOffset = 0;
        UpdateSuggestions();

        // Launch async streaming execution
        IsRunning = true;
        _commandStopwatch.Restart();
        _ = RunCliStreamingAsync(command);
    }

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
