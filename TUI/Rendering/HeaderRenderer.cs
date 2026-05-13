using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Core.Tui.Rendering;

static class HeaderRenderer
{
    // Sleek block-style logo
    static readonly string[] FullLogoLines =
    [
        "     ██████╗ ██╗██████╗ ██████╗ ██╗   ██╗███████╗",
        "    ██╔════╝ ██║██╔══██╗██╔══██╗██║   ██║██╔════╝",
        "    ██║      ██║██████╔╝██████╔╝██║   ██║███████╗",
        "    ██║      ██║██╔══██╗██╔══██╗██║   ██║╚════██║",
        "    ╚██████╗ ██║██║  ██║██║  ██║╚██████╔╝███████║",
        "     ╚═════╝ ╚═╝╚═╝  ╚═╝╚═╝  ╚═╝ ╚═════╝╚══════╝",
    ];

    public static Hex1bWidget RenderWelcome(WidgetContext<VStackWidget> v, int termWidth)
    {
        return v.VStack(inner =>
        {
            var widgets = new List<Hex1bWidget>();
            widgets.Add(inner.Text(""));

            if (termWidth >= 50)
            {
                for (int i = 0; i < FullLogoLines.Length; i++)
                    widgets.Add(inner.Text(Ansi.GradientLine(FullLogoLines[i], i, FullLogoLines.Length)));
            }
            else
            {
                widgets.Add(inner.Text(Ansi.GradientLine("  ◈  Cirrus", 0, 1)));
            }

            widgets.Add(inner.Text(""));
            widgets.Add(inner.Text($"  {Ansi.Foreground}Tips for getting started:{Ansi.End}"));
            widgets.Add(inner.Text($"  {Ansi.Foreground}1. Type a command or ask a natural-language question.{Ansi.End}"));
            widgets.Add(inner.Text($"  {Ansi.Foreground}2. Be specific for the best results.{Ansi.End}"));
            widgets.Add(inner.Text($"  {Ansi.Foreground}3. Press {Ansi.AccentPurple}/help{Ansi.Foreground} or {Ansi.AccentPurple}F1{Ansi.Foreground} for more information.{Ansi.End}"));
            widgets.Add(inner.Text(""));
            widgets.Add(inner.Text($"  {Ansi.Comment}↹ Tab{Ansi.End}  {Ansi.DimGray}autocomplete{Ansi.End}    {Ansi.Comment}↑↓{Ansi.End}  {Ansi.DimGray}suggestions / history{Ansi.End}    {Ansi.Comment}PgUp/PgDn{Ansi.End}  {Ansi.DimGray}scroll{Ansi.End}"));
            widgets.Add(inner.Text(""));

            return widgets.ToArray();
        }).Height(SizeHint.Content);
    }
}
