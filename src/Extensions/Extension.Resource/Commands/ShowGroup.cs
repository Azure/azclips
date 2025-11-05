using Core.SharedContracts;
using Extension.Resource.Models;

namespace Extension.Resource.Commands;

[CommandInfo(Name = "group create", Description = "Create a new resource group.")]
public class GroupCreate : ICommand<ResourceGroup>
{
    public Task<ResourceGroup> ExecuteAsync() => throw new NotImplementedException();
}
