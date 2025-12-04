using Core.Abstractions;

namespace Group;

public class GroupShowCommand : ICommand
{
    public Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Group show command is not yet implemented. This will show resource group details.");
    }
}
