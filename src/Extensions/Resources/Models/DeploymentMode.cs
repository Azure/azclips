using System.Text.Json.Serialization;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Models
{
    //
    // Summary:
    //     The mode that is used to deploy resources. In Incremental mode, resources are
    //     deployed without deleting existing resources that are not in the template. In
    //     Complete mode, resources not in the template are deleted.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DeploymentMode
    {
        Incremental,
        Complete
    }
}
