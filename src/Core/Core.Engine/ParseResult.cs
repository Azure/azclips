namespace Core.Engine;

/// <summary>
/// Represents the result of parsing command-line arguments.
/// </summary>
public class ParseResult
{
    /// <summary>
    /// Gets or sets the command and subcommands (e.g., ["group", "create"]).
    /// </summary>
    public IEnumerable<string> Commands { get; set; } = [];

    /// <summary>
    /// Gets or sets the parsed options (e.g., ["name"] = "mygroup").
    /// </summary>
    public IDictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
}
