namespace Core.Cli.Help;

public class HelpCell
{
    public string[] Content { get; } = [];
    public string[] LimitedContent
    {
        get;
        private set;
    }
    public uint? RenderWidth { get; set; } = null;
    public bool Repeatable { get; set; } = false;

    public HelpCell(params string[] content)
    {
        Content = content;
        LimitedContent = content;
    }

    public HelpCell(string[] content, uint? renderWidth)
    {
        Content = content;
        RenderWidth = renderWidth;
        LimitedContent = LimitWidth(content, renderWidth);
    }

    public static HelpCell Empty(uint renderWidth)
    {
        return new HelpCell([], renderWidth);
    }

    public static HelpCell Separator(params string[] repeatableContent)
    {
        return new HelpCell(repeatableContent) { Repeatable = true };
    }

    static string[] LimitWidth(string[] content, uint? maxWidth)
    {
        if (maxWidth == null) return content;
        
        var newLines = new List<string>();
        foreach (var line in content)
        {
            var words = line.Split(' ');
            var currentLine = string.Empty;
            foreach (var word in words[..^1]) // Exclude the last word to handle it separately
            {
                if (!string.IsNullOrEmpty(currentLine) && currentLine.Length + word.Length + 1 > maxWidth)
                {
                    newLines.Add(currentLine);
                    currentLine = string.Empty;
                }
                currentLine += word + " ";
            }
            // Handle the last word separately
            if (currentLine.Length + words[^1].Length > maxWidth)
            {
                newLines.Add(currentLine.Trim());
                currentLine = words[^1];
            }
            else
            {
                currentLine += words[^1];
            }
            if (!string.IsNullOrEmpty(currentLine))
            {
                newLines.Add(currentLine);
            }
        }
        return [..newLines];
    }

    public uint ContentWidth { get { return Content.Length > 0 ? Content.Max(line => (uint)line.Length) : 0; } }
    public uint RenderedWidth { get { return RenderWidth ?? ContentWidth; } }

    public static string[] RenderLine(HelpCell[] cells, uint offset)
    {
        var renderedLines = new List<string>();
        var maxHeight = cells.Select(cell => cell.Content.Length).Max();
        for (int i = 0; i < maxHeight; i++)
        {
            var line = new string(' ', (int)offset);
            foreach (var cell in cells)
            {
                var part = string.Empty;
                if (i < cell.Content.Length)
                {
                    part = cell.Content[i];
                }
                else if (cell.Repeatable && cell.Content.Length > 0)
                {
                    part = cell.Content[i % cell.Content.Length];
                }
                line += part.PadRight((int)cell.RenderedWidth);
            }
            renderedLines.Add(line);
        }
        return [.. renderedLines];
    }
}
