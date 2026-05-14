using Hex1b;
using Hex1b.Widgets;

namespace Core.Tui.Rendering;

static class MenuBarRenderer
{
    public static Hex1bWidget Render<TParent>(WidgetContext<TParent> ctx, AppState state, Action invalidate)
        where TParent : Hex1bWidget
    {
        return ctx.MenuBar(m =>
        [
            // ── Preset themes ──
            m.Menu("Theme", m =>
            {
                var items = new List<IMenuChild>();
                foreach (var preset in TuiTheme.Presets)
                {
                    var p = preset; // capture
                    var label = state.Theme.Name == p.Name ? $"* {p.Name}" : $"  {p.Name}";
                    items.Add(m.MenuItem(label).NoAccelerator().OnActivated(_ =>
                    {
                        state.Theme = p.Clone();
                        state.Theme.Save();
                        invalidate();
                    }));
                }
                return items;
            }).NoAccelerator(),

            // ── Per-element color submenus ──
            m.Menu("Color", m => new IMenuChild[]
            {
                m.Menu("Prompt Path", m => BuildColorPicker(m, state,
                    () => state.Theme.PromptPathColor,
                    v => { state.Theme.PromptPathColor = v; state.Theme.Name = "Custom"; },
                    invalidate)).NoAccelerator(),

                m.Menu("Chevron", m => BuildColorPicker(m, state,
                    () => state.Theme.ChevronColor,
                    v => { state.Theme.ChevronColor = v; state.Theme.Name = "Custom"; },
                    invalidate)).NoAccelerator(),

                m.Menu("Input Text", m => BuildColorPicker(m, state,
                    () => state.Theme.InputTextColor,
                    v => { state.Theme.InputTextColor = v; state.Theme.Name = "Custom"; },
                    invalidate)).NoAccelerator(),

                m.Menu("User Entry", m => BuildColorPicker(m, state,
                    () => state.Theme.UserEntryColor,
                    v => { state.Theme.UserEntryColor = v; state.Theme.Name = "Custom"; },
                    invalidate)).NoAccelerator(),

                m.Menu("Output", m => BuildColorPicker(m, state,
                    () => state.Theme.OutputColor,
                    v => { state.Theme.OutputColor = v; state.Theme.Name = "Custom"; },
                    invalidate)).NoAccelerator(),

                m.Menu("Error", m => BuildColorPicker(m, state,
                    () => state.Theme.ErrorColor,
                    v => { state.Theme.ErrorColor = v; state.Theme.Name = "Custom"; },
                    invalidate)).NoAccelerator(),
            }).NoAccelerator(),

            // ── Footer colors ──
            m.Menu("Footer", m => BuildFooterSubmenu(m, state, invalidate)).NoAccelerator(),

            // ── Suggestion style ──
            m.Menu("Suggestions", m => BuildSuggestionStylePicker(m, state, invalidate)).NoAccelerator()
        ]);
    }

    private static IEnumerable<IMenuChild> BuildColorPicker(
        MenuContext m, AppState state,
        Func<string?> getCurrent, Action<string?> setCurrent,
        Action invalidate)
    {
        var current = getCurrent();
        foreach (var (name, hex) in TuiTheme.ColorPalette)
        {
            var h = string.IsNullOrEmpty(hex) ? null : hex;
            var isSelected = string.Equals(current, h, StringComparison.OrdinalIgnoreCase)
                || (current == null && h == null);
            var label = isSelected ? $"* {name}" : $"  {name}";
            var capturedHex = h;
            yield return m.MenuItem(label).NoAccelerator().OnActivated(_ =>
            {
                setCurrent(capturedHex);
                state.Theme.Save();
                invalidate();
            });
        }
    }

    private static IEnumerable<IMenuChild> BuildFooterSubmenu(
        MenuContext m, AppState state, Action invalidate)
    {
        yield return m.Menu("Foreground", m => BuildColorPicker(m, state,
            () => state.Theme.FooterForeground,
            v => { state.Theme.FooterForeground = v; state.Theme.Name = "Custom"; },
            invalidate)).NoAccelerator();

        yield return m.Menu("Background", m => BuildColorPicker(m, state,
            () => state.Theme.FooterBackground,
            v => { state.Theme.FooterBackground = v; state.Theme.Name = "Custom"; },
            invalidate)).NoAccelerator();
    }

    private static IEnumerable<IMenuChild> BuildSuggestionStylePicker(
        MenuContext m, AppState state, Action invalidate)
    {
        var styles = new (SuggestionStyle Style, string Label)[]
        {
            (SuggestionStyle.Below, "Below (bordered)"),
            (SuggestionStyle.Above, "Above (bordered)"),
            (SuggestionStyle.Inline, "Below (no border)"),
        };
        foreach (var (style, label) in styles)
        {
            var s = style;
            var prefix = state.Theme.SuggestionStyle == s ? "* " : "  ";
            yield return m.MenuItem($"{prefix}{label}").NoAccelerator().OnActivated(_ =>
            {
                state.Theme.SuggestionStyle = s;
                state.Theme.Save();
                invalidate();
            });
        }
    }
}
