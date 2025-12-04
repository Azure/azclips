using Core.Abstractions;

namespace Group;

public class GroupListCommand : ICommand
{
    public Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Group list command is not yet implemented. This will list all resource groups.");
    }
}
