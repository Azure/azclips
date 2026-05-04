using Hex1b;
using Hex1b.Widgets;

namespace Core.Tui.Rendering;

static class FooterRenderer
{
    public static Hex1bWidget Render(WidgetContext<VStackWidget> v, AppState state, int termWidth)
    {
        // Mode indicator
        var modeLabel = state.Mode == ShellMode.Azure ? "◈ Azure" : "▣ Local";

        // Context path for the "other" mode
        string contextPath;
        if (state.Mode == ShellMode.Azure)
        {
            var cwd = state.LocalCwd;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (cwd.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                cwd = "~" + cwd[home.Length..];
            if (cwd.Length > 50)
                cwd = "..." + cwd[^47..];
            contextPath = cwd;
        }
        else
        {
            contextPath = state.VfsReady ? state.Vfs.CurrentPath : "/";
        }

        var branch = state.GitBranch;

        return v.InfoBar(s =>
        {
            var sections = new List<IInfoBarChild>();

            sections.Add(s.Section(modeLabel));
            sections.Add(s.Separator("  "));
            sections.Add(s.Section(contextPath));

            if (!string.IsNullOrEmpty(branch))
            {
                sections.Add(s.Separator("  "));
                sections.Add(s.Section($" {branch}"));
            }

            sections.Add(s.Spacer());
            sections.Add(s.Section("F2 mode"));
            sections.Add(s.Separator("  "));
            sections.Add(s.Section("↵ run"));
            sections.Add(s.Separator("  "));
            sections.Add(s.Section("⇥ complete"));
            sections.Add(s.Separator("  "));
            sections.Add(s.Section("F1 help"));

            return sections;
        });
    }
}
