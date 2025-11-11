using System.Text.RegularExpressions;
using Core.SharedContracts;
using Extension.Resource.Models;

namespace Extension.Resource.Commands;

[CommandInfo(Name = "group show", Description = "Show details of a resource group.")]
public partial class ShowGroup : ICommand<ResourceGroup>
{
    [GeneratedRegex(@"^/subscriptions/(?<subscriptionId>[^/]+)/resourceGroups/(?<resourceGroupName>[^/]+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-AU")]
    private static partial Regex ResourceGroupIdPattern();

    public string SubscriptionId { get => _subscriptionId; set => _subscriptionId = value; }
    private string _subscriptionId = "";

    public string ResourceGroupName { get => _resourceGroupName; set => _resourceGroupName = value; }
    private string _resourceGroupName = "";

    public string Id
    {
        get => $"/subscriptions/{_subscriptionId}/resourceGroups/{_resourceGroupName}";
        set
        {
            var match = ResourceGroupIdPattern().Match(value);
            if (!match.Success) throw new ArgumentException($"Invalid resource group ID format: {value}");

            _subscriptionId = match.Groups["subscriptionId"].Value;
            _resourceGroupName = match.Groups["resourceGroupName"].Value;
        }
    }

    public Task<ResourceGroup> ExecuteAsync() => throw new NotImplementedException();
}
