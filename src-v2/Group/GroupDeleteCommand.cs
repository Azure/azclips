using Core.Abstractions;

namespace Group;

public class GroupDeleteCommand : ICommand
{
    public Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Group delete command is not yet implemented. This will delete a resource group.");
    }
}
