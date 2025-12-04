using Core.Abstractions;

namespace Group;

public class GroupCreateCommand : ICommand
{
    public Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Group create command is not yet implemented. This will create a resource group.");
    }
}
