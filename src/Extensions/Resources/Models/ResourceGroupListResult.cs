using System.Text.Json.Serialization;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Models
{
    //
    // Summary:
    //     Result of the request to list resource groups. Contains a list of resource groups.
    // todo: support pagination
    public class ResourceGroupListResult
    {
        [JsonPropertyName("value")]
        public ResourceGroup[] Value { get; set; } = Array.Empty<ResourceGroup>();
    }
}