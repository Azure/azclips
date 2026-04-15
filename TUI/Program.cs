using System.Diagnostics;
using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

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

                // ── Output history ──
                foreach (var line in state.OutputLines)
                    widgets.Add(v.Text(line));

                // ── Prompt + TextBox input ──
                widgets.Add(v.HStack(h => [
                    h.Text("azv2 ❯ "),
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
                            bindings.Ctrl().Key(Hex1b.Input.Hex1bKey.L).Action(() =>
                            {
                                state.OutputLines.Clear();
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
                widgets.Add(v.Text($"  {Ansi.DimGray}↵ run  ⇥ complete  ↑↓ browse  ^L clear{Ansi.End}"));

                return widgets.ToArray();
            }),
            options
        );

        await app.RunAsync();
    }
}

/// <summary>
/// Represents an autocomplete suggestion with display text and description.
/// </summary>
record Suggestion(string Text, string Description);

/// <summary>
/// Static command tree for Cirrus CLI autocomplete.
/// Mirrors the command groups, subcommands, and arguments registered in the extension system.
/// </summary>
static class CommandTree
{
    public record ArgInfo(string Name, string Description, bool Required);

    public record CommandInfo(string Name, string Description, ArgInfo[] Args);

    public record GroupInfo(string Name, string Description, CommandInfo[] Commands);

    public static readonly GroupInfo[] Groups =
    [
        new("group", "Manage resource groups", [
            new("list", "List all resource groups", [
                new("--subscription-id", "Azure Subscription Id", false),
            ]),
            new("create", "Create a resource group", [
                new("--subscription-id", "Azure Subscription Id", true),
                new("--name", "The name of the resource group", true),
                new("--resource-id", "The resource id of the resource group", false),
                new("--location", "Location for resource group", true),
            ]),
            new("get", "Get a resource group", [
                new("--subscription-id", "Azure Subscription Id", false),
                new("--name", "The name of the resource group", false),
            ]),
        ]),
        new("login", "Manage authentication", [
            new("", "Login to Azure (default)", [
                new("--subscription-id", "Azure Subscription Id", true),
                new("--tenant-id", "Tenant ID to login to", false),
                new("--use-device-code", "Use device code flow for authentication", false),
            ]),
            new("logout", "Logout from Azure", []),
            new("show", "Show current auth context", []),
        ]),
        new("extension", "Manage extensions", [
            new("add", "Add an extension", [
                new("--name", "Extension name", false),
                new("--version", "Extension version", false),
                new("--feed-url", "NuGet feed URL", false),
                new("--path", "Local path to extension", false),
            ]),
        ]),
    ];

    /// <summary>
    /// Returns autocomplete suggestions based on current input tokens.
    /// </summary>
    public static List<Suggestion> GetSuggestions(string input)
    {
        var results = new List<Suggestion>();
        var trimmed = input.TrimStart();
        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Level 0: no tokens or typing a group name
        if (tokens.Length == 0 || (tokens.Length == 1 && !trimmed.EndsWith(' ')))
        {
            var prefix = tokens.Length == 1 ? tokens[0] : "";
            foreach (var g in Groups)
            {
                if (g.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    results.Add(new Suggestion(g.Name, g.Description));
            }
            return results;
        }

        // Find matching group
        var group = Array.Find(Groups, g => g.Name.Equals(tokens[0], StringComparison.OrdinalIgnoreCase));
        if (group == null) return results;

        // Check if this group has a default (unnamed) command
        var defaultCmd = Array.Find(group.Commands, c => c.Name == "");

        // Level 1: group typed, suggesting subcommand (+ default command args)
        if (tokens.Length == 1 || (tokens.Length == 2 && !trimmed.EndsWith(' ')))
        {
            var prefix = tokens.Length >= 2 ? tokens[1] : "";
            foreach (var cmd in group.Commands)
            {
                if (string.IsNullOrEmpty(cmd.Name)) continue;
                if (cmd.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    results.Add(new Suggestion(cmd.Name, cmd.Description));
            }
            // Also suggest default command's args (e.g. "login --subscription-id")
            if (defaultCmd != null)
            {
                foreach (var arg in defaultCmd.Args)
                {
                    if (arg.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var req = arg.Required ? " (required)" : "";
                        results.Add(new Suggestion(arg.Name, $"{arg.Description}{req}"));
                    }
                }
            }
            return results;
        }

        // Determine which command is being used
        var command = Array.Find(group.Commands, c => !string.IsNullOrEmpty(c.Name) && c.Name.Equals(tokens[1], StringComparison.OrdinalIgnoreCase));
        int argStartIndex;
        if (command != null)
        {
            // Named subcommand matched (e.g. "login logout", "group list")
            argStartIndex = 2;
        }
        else if (defaultCmd != null)
        {
            // No subcommand match — fall back to default command (e.g. "login --subscription-id ...")
            command = defaultCmd;
            argStartIndex = 1; // args start right after the group name
        }
        else
        {
            return results;
        }

        // Find which args are already used
        var usedArgs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = argStartIndex; i < tokens.Length; i++)
        {
            if (tokens[i].StartsWith("--"))
                usedArgs.Add(tokens[i]);
        }

        // The last token being typed
        var lastToken = trimmed.EndsWith(' ') ? "" : tokens[^1];
        var isTypingArg = lastToken.StartsWith("-");

        if (trimmed.EndsWith(' ') || isTypingArg)
        {
            var prefix = isTypingArg ? lastToken : "";
            foreach (var arg in command.Args)
            {
                if (usedArgs.Contains(arg.Name)) continue;
                if (arg.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var req = arg.Required ? " (required)" : "";
                    results.Add(new Suggestion(arg.Name, $"{arg.Description}{req}"));
                }
            }
        }

        return results;
    }
}

/// <summary>
/// Application state: manages input text, suggestions, output, and CLI execution.
/// </summary>
class AppState
{
    private static readonly string CliPath = FindCliPath();

    public string InputText { get; set; } = "";
    public List<string> OutputLines { get; } = new();
    public List<Suggestion> Suggestions { get; private set; } = new();
    public int SelectedSuggestion { get; set; }

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
