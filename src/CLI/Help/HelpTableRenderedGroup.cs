namespace Core.Cli.Help;

public class HelpTableRenderedGroup(string? groupName)
{
    public string? GroupName { get; } = groupName;
    public List<string> RenderedLines { get; } = [];
}
