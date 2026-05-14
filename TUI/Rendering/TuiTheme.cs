using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Core.Tui.Rendering;

/// <summary>
/// Controls where and how autocomplete suggestions appear relative to the input box.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
enum SuggestionStyle
{
    /// <summary>Bordered panel below the input box (default).</summary>
    Below,
    /// <summary>Bordered panel above the input box.</summary>
    Above,
    /// <summary>No border, inline below the input box.</summary>
    Inline,
}

/// <summary>
/// Customizable color theme for the TUI shell. Each color is stored as an
/// RGB hex string (e.g. "#A6E3A1"). A <c>null</c> value means "use the
/// terminal default".
/// </summary>
class TuiTheme
{
    // ── Prompt area ──────────────────────────────────────────────────
    public string? PromptPathColor { get; set; }
    public string? ChevronColor { get; set; }        // "> " symbol
    public string? InputTextColor { get; set; }

    // ── History ──────────────────────────────────────────────────────
    public string? UserEntryColor { get; set; }
    public string? OutputColor { get; set; }
    public string? ErrorColor { get; set; }

    // ── Footer / InfoBar ─────────────────────────────────────────────
    public string? FooterForeground { get; set; }
    public string? FooterBackground { get; set; }

    // ── Suggestions ──────────────────────────────────────────────────
    public SuggestionStyle SuggestionStyle { get; set; } = SuggestionStyle.Below;

    // ── Name (for display in the picker) ─────────────────────────────
    [JsonIgnore]
    public string Name { get; set; } = "Custom";

    // ── ANSI helpers ─────────────────────────────────────────────────

    public string AnsiPromptPath => ToFg(PromptPathColor) ?? Ansi.Foreground;
    public string AnsiChevron => ToFg(ChevronColor) ?? Ansi.AccentBlue;
    public string AnsiInputText => ToFg(InputTextColor) ?? "";
    public string AnsiUserEntry => ToFg(UserEntryColor) ?? Ansi.AccentGreen;
    public string AnsiOutput => ToFg(OutputColor) ?? "";
    public string AnsiError => ToFg(ErrorColor) ?? Ansi.AccentRed;

    // ── Preset themes ────────────────────────────────────────────────

    public static TuiTheme Default => new()
    {
        Name = "Default",
        PromptPathColor = "#CDD6F4",
        ChevronColor = "#89B4FA",
        InputTextColor = null,
        UserEntryColor = "#A6E3A1",
        OutputColor = null,
        ErrorColor = "#F38BA8",
        FooterForeground = null,
        FooterBackground = null,
    };

    public static TuiTheme Ocean => new()
    {
        Name = "Ocean",
        PromptPathColor = "#74C7EC",
        ChevronColor = "#89B4FA",
        InputTextColor = "#BAC2DE",
        UserEntryColor = "#94E2D5",
        OutputColor = "#A6ADC8",
        ErrorColor = "#F38BA8",
        FooterForeground = "#1E1E2E",
        FooterBackground = "#89B4FA",
    };

    public static TuiTheme Sunset => new()
    {
        Name = "Sunset",
        PromptPathColor = "#FAB387",
        ChevronColor = "#F9E2AF",
        InputTextColor = "#CDD6F4",
        UserEntryColor = "#F9E2AF",
        OutputColor = "#BAC2DE",
        ErrorColor = "#F38BA8",
        FooterForeground = "#1E1E2E",
        FooterBackground = "#FAB387",
    };

    public static TuiTheme HighContrast => new()
    {
        Name = "High Contrast",
        PromptPathColor = "#FFFFFF",
        ChevronColor = "#00FF00",
        InputTextColor = "#FFFFFF",
        UserEntryColor = "#00FF00",
        OutputColor = "#FFFFFF",
        ErrorColor = "#FF0000",
        FooterForeground = "#000000",
        FooterBackground = "#FFFFFF",
    };

    public static TuiTheme Monokai => new()
    {
        Name = "Monokai",
        PromptPathColor = "#F8F8F2",
        ChevronColor = "#A6E22E",
        InputTextColor = "#F8F8F2",
        UserEntryColor = "#66D9EF",
        OutputColor = "#F8F8F2",
        ErrorColor = "#F92672",
        FooterForeground = "#272822",
        FooterBackground = "#A6E22E",
    };

    public static readonly TuiTheme[] Presets = [Default, Ocean, Sunset, HighContrast, Monokai];

    // ── Color choices for per-element customization ──────────────────

    public static readonly (string Name, string Hex)[] ColorPalette =
    [
        ("Red",          "#F38BA8"),
        ("Orange",       "#FAB387"),
        ("Yellow",       "#F9E2AF"),
        ("Green",        "#A6E3A1"),
        ("Teal",         "#94E2D5"),
        ("Cyan",         "#89DCEB"),
        ("Blue",         "#89B4FA"),
        ("Purple",       "#CBA6F7"),
        ("Pink",         "#F5C2E7"),
        ("White",        "#FFFFFF"),
        ("Light Gray",   "#BAC2DE"),
        ("Gray",         "#6C7086"),
        ("Foreground",   "#CDD6F4"),
        ("Terminal Default", ""),
    ];

    // ── Persistence ──────────────────────────────────────────────────

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string ConfigPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "cirrus", "tui-theme.json");

    public void Save()
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, s_jsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    public static TuiTheme Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var theme = JsonSerializer.Deserialize<TuiTheme>(json, s_jsonOptions);
                if (theme != null)
                {
                    theme.Name = "Custom";
                    return theme;
                }
            }
        }
        catch
        {
            // Corrupt config — fall back to default
        }
        return Default;
    }

    public TuiTheme Clone() => new()
    {
        Name = Name,
        PromptPathColor = PromptPathColor,
        ChevronColor = ChevronColor,
        InputTextColor = InputTextColor,
        UserEntryColor = UserEntryColor,
        OutputColor = OutputColor,
        ErrorColor = ErrorColor,
        FooterForeground = FooterForeground,
        FooterBackground = FooterBackground,
        SuggestionStyle = SuggestionStyle,
    };

    // ── Internals ────────────────────────────────────────────────────

    private static string? ToFg(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        var (r, g, b) = ParseHex(hex);
        return Ansi.Fg(r, g, b);
    }

    internal static (int R, int G, int B) ParseHex(string hex)
    {
        var h = hex.TrimStart('#');
        return (
            Convert.ToInt32(h[..2], 16),
            Convert.ToInt32(h[2..4], 16),
            Convert.ToInt32(h[4..6], 16));
    }

    internal static Hex1bColor ToHex1bColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return Hex1bColor.Default;
        var (r, g, b) = ParseHex(hex);
        return Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
    }

    // ── Hex1b theme helpers ──────────────────────────────────────────

    // Menu bar palette
    private static readonly Hex1bColor MenuDark = Hex1bColor.FromRgb(30, 30, 46);
    private static readonly Hex1bColor MenuFg = Hex1bColor.FromRgb(166, 173, 200);
    private static readonly Hex1bColor MenuFocusBg = Hex1bColor.FromRgb(69, 71, 90);
    private static readonly Hex1bColor MenuHoverBg = Hex1bColor.FromRgb(49, 50, 68);
    private static readonly Hex1bColor MenuLightFg = Hex1bColor.FromRgb(205, 214, 244);

    /// <summary>
    /// Creates the base Hex1b theme used by the terminal.
    /// </summary>
    public static Hex1bTheme CreateBaseTheme() => Hex1bThemes.Ocean.Clone("cirrus");

    /// <summary>
    /// Wraps a widget in a ThemePanel that applies the standard menu bar /
    /// dropdown / menu-item colors.
    /// </summary>
    public static Hex1bWidget ApplyMenuBarTheme<TParent>(
        WidgetContext<TParent> ctx, Hex1bWidget inner) where TParent : Hex1bWidget
    {
        return ctx.ThemePanel(
            t => t
                  .Set(MenuBarTheme.BackgroundColor, MenuDark)
                  .Set(MenuBarTheme.ForegroundColor, MenuFg)
                  .Set(MenuBarTheme.FocusedBackgroundColor, MenuFocusBg)
                  .Set(MenuBarTheme.FocusedForegroundColor, Hex1bColor.White)
                  .Set(MenuBarTheme.HoveredBackgroundColor, MenuHoverBg)
                  .Set(MenuBarTheme.HoveredForegroundColor, MenuLightFg)
                  .Set(MenuBarTheme.AcceleratorForegroundColor, MenuFg)
                  .Set(MenuBarTheme.AcceleratorUnderline, false)
                  .Set(MenuTheme.BackgroundColor, MenuDark)
                  .Set(MenuTheme.ForegroundColor, MenuFg)
                  .Set(MenuTheme.BorderColor, Hex1bColor.FromRgb(69, 71, 90))
                  .Set(MenuItemTheme.BackgroundColor, MenuDark)
                  .Set(MenuItemTheme.ForegroundColor, MenuFg)
                  .Set(MenuItemTheme.FocusedBackgroundColor, MenuFocusBg)
                  .Set(MenuItemTheme.FocusedForegroundColor, Hex1bColor.White)
                  .Set(MenuItemTheme.HoveredBackgroundColor, MenuHoverBg)
                  .Set(MenuItemTheme.HoveredForegroundColor, MenuLightFg),
            inner);
    }

    /// <summary>
    /// Wraps a widget in a ThemePanel that overrides the foreground color.
    /// Returns the widget unchanged when the color is default.
    /// </summary>
    public static Hex1bWidget WithForeground<TParent>(
        WidgetContext<TParent> ctx, Hex1bColor color, Hex1bWidget inner) where TParent : Hex1bWidget
    {
        if (color.IsDefault)
            return inner;
        return ctx.ThemePanel(
            th => th.Set(GlobalTheme.ForegroundColor, color),
            inner);
    }
}
