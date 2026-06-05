using Microsoft.Azure.Cirrus.ExtensionRuntime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Models
{
    //
    // Summary:
    //     Entity representing the reference to the template.
    public class TemplateLink
    {
        // The URI of the template to deploy. Use either the uri or id property, but not both.
        public string? Uri { get; set; }

        // The resource id of a Template Spec. Use either the id or uri property, but not both.
        public string? Id { get; set; }

        // The query string (for example, a SAS token) to be used with the templateLink URI.
        public string? QueryString { get; set; }

        // The relativePath property can be used to deploy a linked template at a location relative to the parent.
        public string? RelativePath { get; set; }

        // If included, must match the ContentVersion in the template.
        public string? ContentVersion { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
