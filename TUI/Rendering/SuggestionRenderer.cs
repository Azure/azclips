using Hex1b;
using Hex1b.Widgets;

namespace Core.Tui.Rendering;

static class SuggestionRenderer
{
    public static void Render(List<Hex1bWidget> widgets, WidgetContext<VStackWidget> v,
        List<Suggestion> suggestions, int selectedIndex)
    {
        if (suggestions.Count == 0) return;

        const int maxVisible = 8;
        var visibleCount = Math.Min(suggestions.Count, maxVisible);

        if (selectedIndex >= visibleCount)
            widgets.Add(v.Text($"  {Ansi.Comment}▲{Ansi.End}"));

        var scrollOffset = Math.Max(0, selectedIndex - maxVisible + 1);
        var visibleStart = Math.Min(scrollOffset, Math.Max(0, suggestions.Count - maxVisible));
        var visibleEnd = Math.Min(visibleStart + maxVisible, suggestions.Count);

        for (int i = visibleStart; i < visibleEnd; i++)
        {
            var s = suggestions[i];
            if (i == selectedIndex)
                widgets.Add(v.Text($"  {Ansi.AccentPurple}▸ {s.Text,-28}{Ansi.End} {Ansi.Foreground}{s.Description}{Ansi.End}"));
            else
                widgets.Add(v.Text($"  {Ansi.Comment}  {s.Text,-28} {s.Description}{Ansi.End}"));
        }

        if (visibleEnd < suggestions.Count)
            widgets.Add(v.Text($"  {Ansi.Comment}▼{Ansi.End}"));

        if (suggestions.Count > maxVisible)
            widgets.Add(v.Text($"  {Ansi.Comment}({selectedIndex + 1}/{suggestions.Count}){Ansi.End}"));
    }
}
