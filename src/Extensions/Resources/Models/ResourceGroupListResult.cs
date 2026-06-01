using System.Text.Json.Serialization;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Models
{
    //
    // Summary:
    //     Result of the request to list resource groups. Contains a list of resource groups.
    public class ResourceGroupListResult : IPagedResult<ResourceGroup>
    {
        [JsonPropertyName("value")]
        public ResourceGroup[]? Value { get; set; }

        //
        // Summary:
        //     The URL to use for getting the next set of results.
        [JsonPropertyName("nextLink")]
        public string? NextLink { get; set; }
    }
}