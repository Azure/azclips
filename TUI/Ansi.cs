namespace Core.Tui;

static class Ansi
{
    const string Esc = "\x1b[";
    const string Reset = "\x1b[0m";

    public static string Fg(int r, int g, int b) => $"{Esc}38;2;{r};{g};{b}m";
    public static string Bg(int r, int g, int b) => $"{Esc}48;2;{r};{g};{b}m";
    public static string Bold => $"{Esc}1m";
    public static string Dim => $"{Esc}2m";
    public static string Italic => $"{Esc}3m";
    public static string Underline => $"{Esc}4m";
    public static string End => Reset;

    // ── Base palette ──
    public static string Cyan => Fg(0, 200, 220);
    public static string Green => Fg(80, 220, 120);
    public static string Yellow => Fg(240, 200, 80);
    public static string Red => Fg(240, 80, 80);
    public static string Blue => Fg(100, 160, 255);
    public static string Gray => Fg(120, 120, 140);
    public static string White => Fg(220, 220, 230);
    public static string DimGray => Fg(80, 80, 100);
    public static string Magenta => Fg(200, 120, 255);
    public static string Purple => Fg(180, 120, 255);
    public static string Orange => Fg(255, 165, 80);

    // ── Accent colors (matching shell UI dark theme) ──
    public static string AccentBlue => Fg(137, 180, 250);    // #89B4FA
    public static string AccentPurple => Fg(203, 166, 247);  // #CBA6F7
    public static string AccentCyan => Fg(137, 220, 235);    // #89DCEB
    public static string AccentGreen => Fg(166, 227, 161);   // #A6E3A1
    public static string AccentYellow => Fg(249, 226, 175);  // #F9E2AF
    public static string AccentRed => Fg(243, 139, 168);     // #F38BA8
    public static string Foreground => Fg(205, 214, 244);    // #CDD6F4
    public static string Comment => Fg(108, 112, 134);       // #6C7086

    // ── Gradient: interpolate between colors for ASCII art ──
    static readonly (int R, int G, int B)[] GradientStops =
    [
        (71, 150, 228),   // #4796E4
        (132, 122, 206),  // #847ACE
        (195, 103, 127),  // #C3677F
    ];

    public static string GradientLine(string text, int lineIndex, int totalLines)
    {
        if (totalLines <= 1) return $"{Fg(GradientStops[0].R, GradientStops[0].G, GradientStops[0].B)}{text}{End}";
        var t = (double)lineIndex / (totalLines - 1);
        var segmentCount = GradientStops.Length - 1;
        var segment = (int)(t * segmentCount);
        if (segment >= segmentCount) segment = segmentCount - 1;
        var localT = t * segmentCount - segment;
        var (r1, g1, b1) = GradientStops[segment];
        var (r2, g2, b2) = GradientStops[segment + 1];
        var r = (int)(r1 + (r2 - r1) * localT);
        var g = (int)(g1 + (g2 - g1) * localT);
        var b = (int)(b1 + (b2 - b1) * localT);
        return $"{Fg(r, g, b)}{text}{End}";
    }

    // ── Box-drawing helpers (round style matching shell Help.tsx) ──
    public static string BoxTop(int innerWidth, string borderColor)
        => $"{borderColor}╭{new string('─', innerWidth)}╮{End}";
    public static string BoxBottom(int innerWidth, string borderColor)
        => $"{borderColor}╰{new string('─', innerWidth)}╯{End}";
    public static string BoxRow(string content, int innerWidth, string borderColor)
    {
        // Strip ANSI escapes to measure visible length
        var visible = System.Text.RegularExpressions.Regex.Replace(content, @"\x1b\[[0-9;]*m", "");
        var pad = Math.Max(0, innerWidth - visible.Length);
        return $"{borderColor}│{End}{content}{new string(' ', pad)}{borderColor}│{End}";
    }
}
