using System;

namespace Core.Cli.Help;

public class MultiGroupHelpTable
{
    public List<HelpLine> Lines { get; } = [];

    public void AddLine(HelpLine line)
    {
        Lines.Add(line);
    }

    public HelpTableRenderedGroup[] Render(uint indent = 0, uint offset = 0, uint? maxWidth = null, uint minDescriptionWidth = 20)
    {
        var groups = new Dictionary<string, HelpTableRenderedGroup>();
        HelpTableRenderedGroup? defaultGroup = null;
        var groupList = new List<HelpTableRenderedGroup>();
        
        var shortWidth = Lines.SelectMany(line => line.Short).DefaultIfEmpty(string.Empty).Max(content => (uint)content.Length);
        var namesWidth = Lines.SelectMany(line => line.Names).DefaultIfEmpty(string.Empty).Max(content => (uint)content.Length);
        var tagsWidth = Lines.SelectMany(line => line.Tags).DefaultIfEmpty(string.Empty).Max(content => (uint)content.Length);
        var usedWidth = offset + shortWidth + namesWidth + tagsWidth + 3; // 3 for " : "
        if (shortWidth > 0) usedWidth += 1; // 1 for space after short
        if (tagsWidth > 0) usedWidth += 1; // 1 for space after tags
        uint? descriptionWidth = maxWidth != null && maxWidth >= usedWidth + minDescriptionWidth ? maxWidth - usedWidth : null;

        foreach (var line in Lines)
        {
            HelpTableRenderedGroup group;
            if (line.Group == null)
            {
                if (defaultGroup == null)
                {
                    defaultGroup = new HelpTableRenderedGroup(null);
                    groupList.Add(defaultGroup);
                }
                group = defaultGroup;
            }
            else
            {
                if (!groups.TryGetValue(line.Group, out HelpTableRenderedGroup? value))
                {
                    var newGroup = new HelpTableRenderedGroup(line.Group);
                    value = newGroup;
                    groups[line.Group] = value;
                    groupList.Add(newGroup);
                }
                group = value;
            }
            var cells = new List<HelpCell>();
            if (shortWidth > 0)
            {
                cells.Add(new HelpCell(line.Short, shortWidth));
                cells.Add(HelpCell.Empty(1)); // space after short
            }
            if (namesWidth > 0)
            {
                cells.Add(new HelpCell(line.Names, namesWidth));
            }
            if (tagsWidth > 0)
            {
                cells.Add(HelpCell.Empty(1)); // space before tags
                cells.Add(new HelpCell(line.Tags, tagsWidth));
            }
            cells.Add(HelpCell.Separator(" : "));
            cells.Add(new HelpCell(line.Description, descriptionWidth));
            group.RenderedLines.AddRange(HelpCell.RenderLine([.. cells], offset));

            if (line.Detail.Length > 0)
            {
                uint? detailWidth = maxWidth != null && maxWidth >= offset + indent + minDescriptionWidth ? maxWidth - offset - indent : null;
                group.RenderedLines.AddRange(HelpCell.RenderLine([new HelpCell(line.Detail, detailWidth)], offset + indent));
            }
        }
        return [.. groupList];
    }
}
