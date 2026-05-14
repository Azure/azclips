using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Core.Tui.Rendering;

static class SuggestionRenderer
{
    public static Hex1bWidget Render(WidgetContext<VStackWidget> v,
        List<Suggestion> suggestions, int selectedIndex)
    {
        const int maxVisible = 8;

        return v.VStack(inner =>
        {
            var widgets = new List<Hex1bWidget>();

            var scrollOffset = Math.Max(0, selectedIndex - maxVisible + 1);
            var visibleStart = Math.Min(scrollOffset, Math.Max(0, suggestions.Count - maxVisible));
            var visibleEnd = Math.Min(visibleStart + maxVisible, suggestions.Count);

            if (visibleStart > 0)
                widgets.Add(inner.Text($"  {Ansi.Comment}▲{Ansi.End}"));

            for (int i = visibleStart; i < visibleEnd; i++)
            {
                var s = suggestions[i];
                if (i == selectedIndex)
                    widgets.Add(inner.Text($"  {Ansi.AccentPurple}▸ {s.Text,-28}{Ansi.End} {Ansi.Foreground}{s.Description}{Ansi.End}"));
                else
                    widgets.Add(inner.Text($"  {Ansi.Comment}  {s.Text,-28} {s.Description}{Ansi.End}"));
            }

            if (visibleEnd < suggestions.Count)
                widgets.Add(inner.Text($"  {Ansi.Comment}▼{Ansi.End}"));

            if (suggestions.Count > maxVisible)
                widgets.Add(inner.Text($"  {Ansi.Comment}({selectedIndex + 1}/{suggestions.Count}){Ansi.End}"));

            return widgets.ToArray();
        }).Height(SizeHint.Content);
    }
}
