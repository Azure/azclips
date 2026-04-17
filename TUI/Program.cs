using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Tui;

static class Ansi
{
    const string Esc = "\x1b[";
    const string Reset = "\x1b[0m";

    public static string Fg(int r, int g, int b) => $"{Esc}38;2;{r};{g};{b}m";
    public static string Bg(int r, int g, int b) => $"{Esc}48;2;{r};{g};{b}m";
    public static string Bold => $"{Esc}1m";
    public static string Dim => $"{Esc}2m";
    public static string Italic => $"{Esc}3m";
    public static string End => Reset;

    // Color palette
    public static string Cyan => Fg(0, 200, 220);
    public static string Green => Fg(80, 220, 120);
    public static string Yellow => Fg(240, 200, 80);
    public static string Red => Fg(240, 80, 80);
    public static string Blue => Fg(100, 160, 255);
    public static string Gray => Fg(120, 120, 140);
    public static string White => Fg(220, 220, 230);
    public static string DimGray => Fg(80, 80, 100);
    public static string Magenta => Fg(200, 120, 255);
}

public static class Program
{
    public static async Task Main()
    {
        var state = new AppState();

        var theme = Hex1bThemes.Ocean.Clone("cirrus");
        theme.Set(TextBoxTheme.LeftBracket, "");
        theme.Set(TextBoxTheme.RightBracket, "");

        var options = new Hex1bAppOptions { EnableMouse = false, Theme = theme };
        var app = new Hex1bApp(ctx =>
            ctx.VStack(v =>
            {
                var widgets = new List<Hex1bWidget>();

                // ── Welcome banner (only if no output yet) ──
                if (state.OutputLines.Count == 0)
                {
                    widgets.Add(v.Text(""));
                    widgets.Add(v.Text($"  {Ansi.Bold}{Ansi.Cyan}☁  Cirrus CLI{Ansi.End}  {Ansi.DimGray}— interactive shell{Ansi.End}"));
                    widgets.Add(v.Text($"  {Ansi.DimGray}Type a command or start typing for suggestions{Ansi.End}"));
                    widgets.Add(v.Text(""));
                }
                else
                {
                    // ── Output history with manual scroll windowing ──
                    var reservedLines = 2 + state.Suggestions.Count; // prompt + status + suggestions
                    var availableHeight = Math.Max(4, Console.WindowHeight - reservedLines);
                    var totalLines = state.OutputLines.Count;

                    // scrollOffset=0 means "show the latest"; positive means "scroll up N lines"
                    var endIdx = Math.Max(0, totalLines - state.OutputScrollOffset);
                    var startIdx = Math.Max(0, endIdx - availableHeight);

                    if (startIdx > 0)
                        widgets.Add(v.Text($"  {Ansi.DimGray}↑ {startIdx} more lines above{Ansi.End}"));

                    for (int i = startIdx; i < endIdx; i++)
                        widgets.Add(v.Text(state.OutputLines[i]));

                    if (state.OutputScrollOffset > 0)
                        widgets.Add(v.Text($"  {Ansi.DimGray}↓ {state.OutputScrollOffset} more lines below{Ansi.End}"));
                }

                // ── Prompt + TextBox input ──
                widgets.Add(v.HStack(h => [
                    h.Text(state.Prompt),
                    h.TextBox(state.InputText)
                        .OnTextChanged(e =>
                        {
                            state.InputText = e.NewText;
                            state.UpdateSuggestions();
                        })
                        .WithInputBindings(bindings =>
                        {
                            bindings.Key(Hex1b.Input.Hex1bKey.Enter).Action(() =>
                            {
                                state.ExecuteCommand();
                            }, "Execute command");
                            bindings.Key(Hex1b.Input.Hex1bKey.Tab).Action(() =>
                            {
                                state.AcceptSuggestion();
                            }, "Accept suggestion");
                            bindings.Key(Hex1b.Input.Hex1bKey.DownArrow).Action(() =>
                            {
                                if (state.Suggestions.Count > 0)
                                    state.SelectedSuggestion = (state.SelectedSuggestion + 1) % state.Suggestions.Count;
                            }, "Next suggestion");
                            bindings.Key(Hex1b.Input.Hex1bKey.UpArrow).Action(() =>
                            {
                                if (state.Suggestions.Count > 0)
                                    state.SelectedSuggestion = (state.SelectedSuggestion - 1 + state.Suggestions.Count) % state.Suggestions.Count;
                            }, "Previous suggestion");
                            bindings.Key(Hex1b.Input.Hex1bKey.PageUp).Action(() =>
                            {
                                var pageSize = Math.Max(1, Console.WindowHeight / 2);
                                state.OutputScrollOffset = Math.Min(
                                    state.OutputScrollOffset + pageSize,
                                    Math.Max(0, state.OutputLines.Count - 1));
                            }, "Scroll output up");
                            bindings.Key(Hex1b.Input.Hex1bKey.PageDown).Action(() =>
                            {
                                var pageSize = Math.Max(1, Console.WindowHeight / 2);
                                state.OutputScrollOffset = Math.Max(0, state.OutputScrollOffset - pageSize);
                            }, "Scroll output down");
                            bindings.Ctrl().Key(Hex1b.Input.Hex1bKey.L).Action(() =>
                            {
                                state.OutputLines.Clear();
                                state.OutputScrollOffset = 0;
                            }, "Clear output");
                        }),
                ]));

                // ── Inline suggestions ──
                for (int i = 0; i < state.Suggestions.Count; i++)
                {
                    var s = state.Suggestions[i];
                    if (i == state.SelectedSuggestion)
                        widgets.Add(v.Text($"  {Ansi.Cyan}▸ {s.Text,-28}{Ansi.End} {Ansi.White}{s.Description}{Ansi.End}"));
                    else
                        widgets.Add(v.Text($"  {Ansi.DimGray}  {s.Text,-28} {s.Description}{Ansi.End}"));
                }

                // ── Status bar ──
                widgets.Add(v.Text($"  {Ansi.DimGray}↵ run  ⇥ complete  ↑↓ browse  PgUp/PgDn scroll  ^L clear{Ansi.End}"));

                return widgets.ToArray();
            }),
            options
        );

        await app.RunAsync();
    }
}

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
    public List<string> OutputLines { get; } = new();
    public List<Suggestion> Suggestions { get; private set; } = new();
    public int SelectedSuggestion { get; set; }
    public int OutputScrollOffset { get; set; }
    public string Prompt { get; private set; } = "/ > ";

    public AppState()
    {
        RefreshPrompt();
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

        OutputLines.Add($"{Ansi.Bold}{Ansi.Green}❯{Ansi.End} {Ansi.White}azv2 {command}{Ansi.End}");

        try
        {
            var (exitCode, stdout, stderr) = RunCli(command);

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                foreach (var line in stdout.Split('\n'))
                {
                    var trimmedLine = line.TrimEnd('\r');
                    if (IsDebugOrVerbose(trimmedLine)) continue;
                    OutputLines.Add(trimmedLine);
                }
            }
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                var stderrLines = stderr.Split('\n')
                    .Select(l => l.TrimEnd('\r'))
                    .Where(l => !IsDebugOrVerbose(l) && !string.IsNullOrWhiteSpace(l))
                    .ToList();
                if (stderrLines.Count > 0)
                {
                    OutputLines.Add($"{Ansi.Red}  stderr:{Ansi.End}");
                    foreach (var line in stderrLines)
                        OutputLines.Add($"{Ansi.Red}  {line}{Ansi.End}");
                }
            }
            if (exitCode == 0)
                OutputLines.Add($"{Ansi.DimGray}  ✓ done{Ansi.End}");
            else
                OutputLines.Add($"{Ansi.Yellow}  ✗ exit code {exitCode}{Ansi.End}");
        }
        catch (Exception ex)
        {
            OutputLines.Add($"{Ansi.Red}  ✗ {ex.Message}{Ansi.End}");
        }

        OutputLines.Add("");
        InputText = "";
        OutputScrollOffset = 0;
        RefreshPrompt();
        UpdateSuggestions();
    }

    private static bool IsDebugOrVerbose(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("[DEBUG]", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("[VERBOSE]", StringComparison.OrdinalIgnoreCase);
    }

    private static (int exitCode, string stdout, string stderr) RunCli(string args)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = CliPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit(30_000);
        Task.WaitAll(stdoutTask, stderrTask);

        return (process.ExitCode, stdoutTask.Result, stderrTask.Result);
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
