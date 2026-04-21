using Hex1b;
using Hex1b.Widgets;

namespace Core.Tui;

static class HelpRenderer
{
    public static List<Hex1bWidget> Render(WidgetContext<VStackWidget> v)
    {
        // Dynamic box width matching terminal (like shell Help.tsx with borderStyle="round")
        const int innerWidth = 55;
        var bc = Ansi.Comment; // border color

        var widgets = new List<Hex1bWidget>();
        widgets.Add(v.Text(""));
        widgets.Add(v.Text($"  {Ansi.BoxTop(innerWidth, bc)}"));
        widgets.Add(v.Text($"  {Ansi.BoxRow("", innerWidth, bc)}"));

        // Basics section (like shell Help.tsx)
        widgets.Add(v.Text($"  {Ansi.BoxRow($"  {Ansi.Bold}{Ansi.Foreground}Basics:{Ansi.End}", innerWidth, bc)}"));
        widgets.Add(v.Text($"  {Ansi.BoxRow($"  {Ansi.AccentPurple}@{Ansi.End} {Ansi.Comment}Add file context (e.g. @src/myFile.ts){Ansi.End}", innerWidth, bc)}"));
        widgets.Add(v.Text($"  {Ansi.BoxRow($"  {Ansi.AccentPurple}!{Ansi.End} {Ansi.Comment}Execute shell command (e.g. !npm run start){Ansi.End}", innerWidth, bc)}"));
        widgets.Add(v.Text($"  {Ansi.BoxRow("", innerWidth, bc)}"));

        // Commands section
        widgets.Add(v.Text($"  {Ansi.BoxRow($"  {Ansi.Bold}{Ansi.Foreground}Commands:{Ansi.End}", innerWidth, bc)}"));
        AddCommand(widgets, v, "group list", "List resource groups", innerWidth, bc);
        AddCommand(widgets, v, "group create", "Create a resource group", innerWidth, bc);
        AddCommand(widgets, v, "login", "Login to Azure", innerWidth, bc);
        AddCommand(widgets, v, "login show", "Show current auth context", innerWidth, bc);
        AddCommand(widgets, v, "extension add", "Add an extension", innerWidth, bc);
        widgets.Add(v.Text($"  {Ansi.BoxRow("", innerWidth, bc)}"));

        // Keyboard shortcuts section (matching shell Help.tsx layout)
        widgets.Add(v.Text($"  {Ansi.BoxRow($"  {Ansi.Bold}{Ansi.Foreground}Keyboard Shortcuts:{Ansi.End}", innerWidth, bc)}"));
        AddShortcut(widgets, v, "Enter", "Execute command", innerWidth, bc);
        AddShortcut(widgets, v, "Tab", "Accept suggestion", innerWidth, bc);
        AddShortcut(widgets, v, "↑ / ↓", "Browse suggestions / history", innerWidth, bc);
        AddShortcut(widgets, v, "PgUp / PgDn", "Scroll output", innerWidth, bc);
        AddShortcut(widgets, v, "Mouse scroll", "Scroll output", innerWidth, bc);
        AddShortcut(widgets, v, "Ctrl+L", "Clear output", innerWidth, bc);
        AddShortcut(widgets, v, "F1", "Toggle this help screen", innerWidth, bc);
        AddShortcut(widgets, v, "Esc", "Close help / cancel", innerWidth, bc);
        widgets.Add(v.Text($"  {Ansi.BoxRow("", innerWidth, bc)}"));

        // Tips section
        widgets.Add(v.Text($"  {Ansi.BoxRow($"  {Ansi.Bold}{Ansi.Foreground}Tips:{Ansi.End}", innerWidth, bc)}"));
        widgets.Add(v.Text($"  {Ansi.BoxRow($"  {Ansi.Comment}• Type a natural language query for AI help{Ansi.End}", innerWidth, bc)}"));
        widgets.Add(v.Text($"  {Ansi.BoxRow($"  {Ansi.Comment}• Use tab completion for faster input{Ansi.End}", innerWidth, bc)}"));
        widgets.Add(v.Text($"  {Ansi.BoxRow($"  {Ansi.Comment}• Commands are forwarded to azv2 CLI{Ansi.End}", innerWidth, bc)}"));
        widgets.Add(v.Text($"  {Ansi.BoxRow("", innerWidth, bc)}"));

        widgets.Add(v.Text($"  {Ansi.BoxBottom(innerWidth, bc)}"));
        widgets.Add(v.Text(""));

        return widgets;
    }

    static void AddCommand(List<Hex1bWidget> widgets, WidgetContext<VStackWidget> v,
        string name, string desc, int innerWidth, string bc)
    {
        widgets.Add(v.Text($"  {Ansi.BoxRow($"  {Ansi.AccentPurple}{name,-18}{Ansi.End}{Ansi.Comment}{desc}{Ansi.End}", innerWidth, bc)}"));
    }

    static void AddShortcut(List<Hex1bWidget> widgets, WidgetContext<VStackWidget> v,
        string key, string desc, int innerWidth, string bc)
    {
        widgets.Add(v.Text($"  {Ansi.BoxRow($"  {Ansi.AccentYellow}{key,-16}{Ansi.End}{Ansi.Comment}{desc}{Ansi.End}", innerWidth, bc)}"));
    }
}
