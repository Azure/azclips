using Core.Abstractions;

namespace Core.Cli.Exceptions;

/// <summary>
/// Thrown when the user requests help at the extension/group level (e.g., "azv2 group --help").
/// Carries enough context for the help template to enumerate commands.
/// </summary>
public sealed class CommandGroupHelpException(
    string groupName,
    string extensionName,
    IExtensionIndex extensionIndex) : ParserException("Group help requested.")
{
    public string GroupName { get; } = groupName;
    public string ExtensionName { get; } = extensionName;
    public IExtensionIndex ExtensionIndex { get; } = extensionIndex;
}
