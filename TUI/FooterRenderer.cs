using Hex1b;
using Hex1b.Widgets;

namespace Core.Tui;

static class FooterRenderer
{
    public static Hex1bWidget Render(WidgetContext<VStackWidget> v, AppState state, int termWidth)
    {
        var cwd = Directory.GetCurrentDirectory();
        // Shorten home directory with ~
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (cwd.StartsWith(home, StringComparison.OrdinalIgnoreCase))
            cwd = "~" + cwd[home.Length..];

        // Truncate long paths
        if (cwd.Length > 50)
            cwd = "..." + cwd[^47..];

        var branch = state.GitBranch;
        var branchPart = !string.IsNullOrEmpty(branch)
            ? $" {Ansi.AccentPurple} {branch}{Ansi.End}"
            : "";

        // Left: path + branch (like shell Footer.tsx)
        var left = $" {Ansi.AccentBlue}{cwd}{Ansi.End}{branchPart}";

        // Right: keyboard shortcuts (like shell Footer.tsx right section)
        var right = $"{Ansi.Comment}↵ run  ⇥ complete  ↑↓ navigate  F1 help{Ansi.End} ";

        // Build the footer with flexible spacing
        var leftVisible = StripAnsi(left);
        var rightVisible = StripAnsi(right);
        var gap = Math.Max(1, termWidth - leftVisible.Length - rightVisible.Length);

        return v.Text($"{left}{new string(' ', gap)}{right}");
    }

    static string StripAnsi(string text)
        => System.Text.RegularExpressions.Regex.Replace(text, @"\x1b\[[0-9;]*m", "");
}
