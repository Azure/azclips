using Hex1b;
using Hex1b.Widgets;

namespace Core.Tui;

static class FooterRenderer
{
    public static Hex1bWidget Render(WidgetContext<VStackWidget> v, AppState state, int termWidth)
    {
        var branch = state.GitBranch;
        var branchPart = !string.IsNullOrEmpty(branch)
            ? $" {Ansi.AccentPurple} {branch}{Ansi.End}"
            : "";

        // Mode indicator
        var modeLabel = state.Mode == ShellMode.Azure
            ? $"{Ansi.AccentCyan}◈ Azure{Ansi.End}"
            : $"{Ansi.AccentGreen}▣ Local{Ansi.End}";

        // Show the "other" context path: Azure mode shows local cwd, Local mode shows Azure VFS path
        string contextPath;
        if (state.Mode == ShellMode.Azure)
        {
            var cwd = state.LocalCwd;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (cwd.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                cwd = "~" + cwd[home.Length..];
            if (cwd.Length > 50)
                cwd = "..." + cwd[^47..];
            contextPath = $"{Ansi.AccentBlue}{cwd}{Ansi.End}";
        }
        else
        {
            var vfsPath = state.VfsReady ? state.Vfs.CurrentPath : "/";
            contextPath = $"{Ansi.AccentCyan}{vfsPath}{Ansi.End}";
        }

        // Left: mode + other-context path + branch
        var left = $" {modeLabel}  {contextPath}{branchPart}";

        // Right: keyboard shortcuts
        var right = $"{Ansi.Comment}F2 mode  ↵ run  ⇥ complete  F1 help{Ansi.End} ";

        // Build the footer with flexible spacing
        var leftVisible = StripAnsi(left);
        var rightVisible = StripAnsi(right);
        var gap = Math.Max(1, termWidth - leftVisible.Length - rightVisible.Length);

        return v.Text($"{left}{new string(' ', gap)}{right}");
    }

    static string StripAnsi(string text)
        => System.Text.RegularExpressions.Regex.Replace(text, @"\x1b\[[0-9;]*m", "");
}
