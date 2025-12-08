using System.Collections;

namespace Core.Cli.Help;

public class HelpLine
{
    public string[] Short { get; set; } = [];
    public string[] Names { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public string[] Description { get; set; } = [];
    public string[] Detail { get; set; } = [];
    public string? Group { get; set; } = null;

    public HelpLine(string[] shortContent, string[] namesContent, string[] tagsContent, string[] descriptionContent, string[] detailContent, string? group = null)
    {
        Short = shortContent;
        Names = namesContent;
        Tags = tagsContent;
        Description = descriptionContent;
        Detail = detailContent;
        Group = group;
    }

    public HelpLine(string key, string[] description, string? group = null)
    {
        Names = [key];
        Description = description;
        Group = group;
    }
}