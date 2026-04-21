using Hex1b;
using Hex1b.Widgets;

namespace Core.Tui;

static class HeaderRenderer
{
    // Short ASCII logo for narrow terminals
    static readonly string ShortLogoText = "  ☁  Cirrus";

    // Full ASCII art logo lines (raw text for gradient coloring)
    static readonly string[] FullLogoLines =
    [
        "   _____ _                        ",
        "  / ____(_)                       ",
        " | |     _ _ __ _ __ _   _ ___    ",
        " | |    | | '__| '__| | | / __|   ",
        " | |____| | |  | |  | |_| \\__ \\  ",
        "  \\_____|_|_|  |_|   \\__,_|___/  ",
    ];

    public static List<Hex1bWidget> RenderWelcome(WidgetContext<VStackWidget> v, int termWidth)
    {
        var widgets = new List<Hex1bWidget>();
        widgets.Add(v.Text(""));

        if (termWidth >= 50)
        {
            // Gradient-colored full logo (like shell Header.tsx with ink-gradient)
            for (int i = 0; i < FullLogoLines.Length; i++)
                widgets.Add(v.Text(Ansi.GradientLine(FullLogoLines[i], i, FullLogoLines.Length)));
        }
        else
        {
            widgets.Add(v.Text(Ansi.GradientLine(ShortLogoText, 0, 1)));
        }

        widgets.Add(v.Text(""));

        // Tips section (inspired by Tips.tsx)
        widgets.Add(v.Text($"  {Ansi.Foreground}Tips for getting started:{Ansi.End}"));
        widgets.Add(v.Text($"  {Ansi.Foreground}1. Type a command or ask a natural-language question.{Ansi.End}"));
        widgets.Add(v.Text($"  {Ansi.Foreground}2. Be specific for the best results.{Ansi.End}"));
        widgets.Add(v.Text($"  {Ansi.Foreground}3. Press {Ansi.AccentPurple}/help{Ansi.Foreground} or {Ansi.AccentPurple}F1{Ansi.Foreground} for more information.{Ansi.End}"));
        widgets.Add(v.Text(""));

        // Keyboard hints
        widgets.Add(v.Text($"  {Ansi.Comment}↹ Tab{Ansi.End}  {Ansi.DimGray}autocomplete{Ansi.End}    {Ansi.Comment}↑↓{Ansi.End}  {Ansi.DimGray}suggestions / history{Ansi.End}    {Ansi.Comment}PgUp/PgDn{Ansi.End}  {Ansi.DimGray}scroll{Ansi.End}"));
        widgets.Add(v.Text(""));

        return widgets;
    }
}
