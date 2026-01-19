namespace Microsoft.Azure.Cirrus.Extensions.Resources.Models
{
    //
    // Summary:
    //     Result of the request to list resource groups. Contains a list of resource groups.
    // todo: support pagination
    public class ResourceGroupListResult
    {
        public required ResourceGroup[] Value { get; set; }

        public ResourceGroupListResult(ResourceGroup[]? value = null)
        {
            Value = value ?? Array.Empty<ResourceGroup>();
        }
    }
}
