using Core.Abstractions;

namespace Core.Cli.Exceptions;

/// <summary>
/// Thrown when the user requests help for a specific command (e.g., "azv2 group list --help").
/// Carries the command metadata so the template can reflect on the command type.
/// </summary>
public sealed class CommandHelpException(
    string groupName,
    string extensionName,
    ICommandMetadata commandMetadata) : ParserException("Command help requested.")
{
    public string GroupName { get; } = groupName;
    public string ExtensionName { get; } = extensionName;
    public ICommandMetadata CommandMetadata { get; } = commandMetadata;
}
