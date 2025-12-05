using Core.Abstractions;

namespace Group;

public class GroupGetCommand : ICommand
{
    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Group Get: Not implemented yet.");
        Console.ForegroundColor = previousColor;
        return Task.CompletedTask;
    }
}
