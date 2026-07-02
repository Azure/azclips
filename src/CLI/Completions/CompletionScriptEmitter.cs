// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;

namespace Core.Cli.Completions;

/// <summary>
/// Emits shell-specific completion scripts to stdout so that any az binary
/// supporting <c>--completions</c> can self-register tab completion.
/// Usage: <c>az --completions-script bash|zsh|powershell</c>
/// </summary>
internal static class CompletionScriptEmitter
{
    private static readonly Dictionary<string, string> ShellToFile = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bash"] = "az.completions.bash",
        ["zsh"] = "az.completions.zsh",
        ["powershell"] = "az.completions.ps1",
        ["pwsh"] = "az.completions.ps1",
        ["ps1"] = "az.completions.ps1",
    };

    /// <summary>
    /// Handles <c>--completions-script &lt;shell&gt;</c> and writes the completion
    /// script to stdout with the resolver patched to use the current binary path.
    /// Returns <see langword="null"/> when this handler should not run.
    /// </summary>
    public static int? TryHandle(string[] args)
    {
        if (args.Length < 2 || args[0] != "--completions-script")
            return null;

        var shell = args[1];
        if (!ShellToFile.TryGetValue(shell, out var scriptFileName))
        {
            Console.Error.WriteLine($"Unknown shell: {shell}");
            Console.Error.WriteLine($"Supported shells: {string.Join(", ", ShellToFile.Keys.Distinct(StringComparer.OrdinalIgnoreCase))}");
            return 1;
        }

        var scriptPath = Path.Combine(AppContext.BaseDirectory, scriptFileName);
        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine($"Completion script not found: {scriptPath}");
            return 1;
        }

        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath))
        {
            // Fall back: emit the script as-is (resolver will use PATH)
            Console.Write(File.ReadAllText(scriptPath));
            return 0;
        }

        // Derive the command name from the binary (e.g. "az", "myaz")
        var cmdName = Path.GetFileNameWithoutExtension(exePath);

        var script = File.ReadAllText(scriptPath);
        var patched = PatchResolver(script, shell, exePath);
        patched = PatchRegistration(patched, shell, cmdName);
        Console.Write(patched);
        return 0;
    }

    /// <summary>
    /// Replaces the resolver function in the script with one that returns
    /// the hardcoded path of the current binary.
    /// </summary>
    private static string PatchResolver(string script, string shell, string exePath)
    {
        // Normalize path for shell compatibility
        var escapedPath = exePath.Replace("\\", "/");

        if (shell is "bash")
        {
            return ReplaceFunction(script,
                "_az_resolve_exe()",
                $"_az_resolve_exe() {{\n    echo \"{escapedPath}\"\n}}");
        }

        if (shell is "zsh")
        {
            return ReplaceFunction(script,
                "_az_resolve_exe()",
                $"_az_resolve_exe() {{\n    echo \"{escapedPath}\"\n}}");
        }

        // PowerShell
        return ReplaceFunction(script,
            "function global:_azResolveExe {",
            $"function global:_azResolveExe {{\n    return '{exePath}'\n}}");
    }

    /// <summary>
    /// Replaces the command registration lines so the script registers
    /// completions for the actual binary name (e.g. "myaz" instead of "az").
    /// </summary>
    private static string PatchRegistration(string script, string shell, string cmdName)
    {
        if (cmdName.Equals("az", StringComparison.OrdinalIgnoreCase))
            return script; // default names, no patching needed

        var cmdExe = cmdName + ".exe";

        if (shell is "bash")
        {
            script = script.Replace(
                "complete -F _az_completions az az.exe",
                $"complete -F _az_completions {cmdName} {cmdExe}");
        }
        else if (shell is "zsh")
        {
            script = script.Replace(
                "compdef _az az az.exe",
                $"compdef _az {cmdName} {cmdExe}");
            script = script.Replace(
                "#compdef az",
                $"#compdef {cmdName}");
        }
        else
        {
            // PowerShell: patch both ArgumentCompleter and TabExpansion2 regex
            script = script.Replace(
                "foreach ($_cmd in @('az', 'az.exe'))",
                $"foreach ($_cmd in @('{cmdName}', '{cmdExe}'))");
            script = script.Replace(
                @"'^\s*az(\.exe)?\s'",
                $@"'^\s*{System.Text.RegularExpressions.Regex.Escape(cmdName)}(\.exe)?\s'");
        }

        return script;
    }

    /// <summary>
    /// Replaces a function body from its signature through the matching closing brace.
    /// </summary>
    private static string ReplaceFunction(string script, string signature, string replacement)
    {
        var startIdx = script.IndexOf(signature, StringComparison.Ordinal);
        if (startIdx < 0)
            return script;

        // Find the opening brace
        var braceStart = script.IndexOf('{', startIdx);
        if (braceStart < 0)
            return script;

        // Find matching closing brace (track nesting)
        int depth = 0;
        int endIdx = -1;
        for (int i = braceStart; i < script.Length; i++)
        {
            if (script[i] == '{') depth++;
            else if (script[i] == '}') depth--;

            if (depth == 0)
            {
                endIdx = i;
                break;
            }
        }

        if (endIdx < 0)
            return script;

        var sb = new StringBuilder(script.Length);
        sb.Append(script, 0, startIdx);
        sb.Append(replacement);
        sb.Append(script, endIdx + 1, script.Length - endIdx - 1);
        return sb.ToString();
    }
}
