using System.Text.Json.Serialization;
using Microsoft.Azure.Cirrus.ExtensionRuntime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Models
{
    //
    // Summary:
    //     Deployment on error behavior. Controls how ARM rolls back the resource group
    //     to a prior deployment when the current deployment fails.
    public class OnErrorDeployment
    {
        // The deployment on error behavior type. "LastSuccessful" or "SpecificDeployment".
        public string Type { get; set; } = string.Empty;

        // The deployment to be used on error case. Required when Type is "SpecificDeployment".
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DeploymentName { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
