namespace Core.Abstractions;

/// <summary>
/// Represents a module that provides commands.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Gets the commands provided by this module.
    /// </summary>
    /// <returns>Collection of commands</returns>
    IEnumerable<ICommand> GetCommands();
}
