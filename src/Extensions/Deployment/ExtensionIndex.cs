// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Deployment.Customization;

namespace Microsoft.Azure.Azclips.Extensions.Deployment;

public class ExtensionIndex : IExtensionIndex
{
    private const string AssemblyName = "Microsoft.Azure.Azclips.Extensions.Deployment";

    private static readonly CommandMetadata s_ps = new(
        Name: nameof(NewManagementGroupDeploymentProcessor),
        InvocationName: "New-AzManagementGroupDeployment",
        Aliases: [],
        FullName: typeof(NewManagementGroupDeploymentProcessor).FullName!,
        AssemblyName: AssemblyName,
        ProcessorType: typeof(NewManagementGroupDeploymentProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["ManagementGroupId"] = new("ManagementGroupId", "ManagementGroupId", typeof(string), "The management group ID.", true, [], []),
            ["Name"] = new("Name", "Name", typeof(string), "The deployment name.", false, ["DeploymentName"], []),
            ["Location"] = new("Location", "Location", typeof(string), "The location used to store deployment metadata.", true, [], []),
            ["TemplateFile"] = new("TemplateFile", "TemplateFile", typeof(string), "A local ARM JSON or Bicep template.", false, [], []),
            ["TemplateUri"] = new("TemplateUri", "TemplateUri", typeof(string), "The URI of an ARM template.", false, [], []),
            ["TemplateSpecId"] = new("TemplateSpecId", "TemplateSpecId", typeof(string), "The resource ID of a Template Spec.", false, [], []),
            ["TemplateObject"] = new("TemplateObject", "TemplateObject", typeof(object), "An inline ARM template object.", false, [], []),
            ["QueryString"] = new("QueryString", "QueryString", typeof(string), "A query string used with TemplateUri.", false, [], []),
            ["TemplateParameterFile"] = new("ParametersFile", "TemplateParameterFile", typeof(string), "A local JSON or Bicep parameters file.", false, [], []),
            ["TemplateParameterUri"] = new("ParametersUri", "TemplateParameterUri", typeof(string), "The URI of an ARM parameters file.", false, [], []),
            ["TemplateParameterObject"] = new("TemplateParameterObject", "TemplateParameterObject", typeof(object), "An inline template parameter object.", false, [], []),
            ["ValidationLevel"] = new("ValidationLevel", "ValidationLevel", typeof(string), "Template, Provider, or ProviderNoRbac.", false, [], ["Template", "Provider", "ProviderNoRbac"]),
            ["DeploymentDebugLogLevel"] = new("DeploymentDebugLogLevel", "DeploymentDebugLogLevel", typeof(string), "None, RequestContent, ResponseContent, or All.", false, [], ["None", "RequestContent", "ResponseContent", "All"]),
            ["Tag"] = new("Tag", "Tag", typeof(object), "Deployment tags.", false, [], []),
            ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile.", false, [], []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly CommandMetadata s_psWhatIf = new(
        Name: nameof(NewManagementGroupDeploymentWhatIfProcessor),
        InvocationName: "New-AzManagementGroupDeploymentWhatIf",
        Aliases: [],
        FullName: typeof(NewManagementGroupDeploymentWhatIfProcessor).FullName!,
        AssemblyName: AssemblyName,
        ProcessorType: typeof(NewManagementGroupDeploymentWhatIfProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["ManagementGroupId"] = new("ManagementGroupId", "ManagementGroupId", typeof(string), "The management group ID.", true, [], []),
            ["Name"] = new("Name", "Name", typeof(string), "The deployment name.", false, ["DeploymentName"], []),
            ["Location"] = new("Location", "Location", typeof(string), "The location used to store deployment metadata.", true, [], []),
            ["TemplateFile"] = new("TemplateFile", "TemplateFile", typeof(string), "A local ARM JSON or Bicep template.", false, [], []),
            ["TemplateUri"] = new("TemplateUri", "TemplateUri", typeof(string), "The URI of an ARM template.", false, [], []),
            ["TemplateSpecId"] = new("TemplateSpecId", "TemplateSpecId", typeof(string), "The resource ID of a Template Spec.", false, [], []),
            ["TemplateObject"] = new("TemplateObject", "TemplateObject", typeof(object), "An inline ARM template object.", false, [], []),
            ["QueryString"] = new("QueryString", "QueryString", typeof(string), "A query string used with TemplateUri.", false, [], []),
            ["TemplateParameterFile"] = new("ParametersFile", "TemplateParameterFile", typeof(string), "A local JSON or Bicep parameters file.", false, [], []),
            ["TemplateParameterUri"] = new("ParametersUri", "TemplateParameterUri", typeof(string), "The URI of an ARM parameters file.", false, [], []),
            ["TemplateParameterObject"] = new("TemplateParameterObject", "TemplateParameterObject", typeof(object), "An inline template parameter object.", false, [], []),
            ["ValidationLevel"] = new("ValidationLevel", "ValidationLevel", typeof(string), "Template, Provider, or ProviderNoRbac.", false, [], ["Template", "Provider", "ProviderNoRbac"]),
            ["ResultFormat"] = new("ResultFormatInput", "ResultFormat", typeof(string), "FullResourcePayloads or ResourceIdOnly.", false, [], ["FullResourcePayloads", "ResourceIdOnly"]),
            ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile.", false, [], []),
        },
        ParameterAliases: new Dictionary<string, string>());

    private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
    {
        ["New-AzManagementGroupDeployment"] = s_ps,
        ["New-AzManagementGroupDeploymentWhatIf"] = s_psWhatIf,
    };

    private static readonly Dictionary<string, CommandMetadata> s_cliTable = [];

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType) =>
        GetCommandTable(clientType).TryGetValue(name, out CommandMetadata? metadata)
            ? metadata
            : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) => clientType switch
    {
        ClientType.PowerShell => s_psTable,
        ClientType.CLI => s_cliTable,
        _ => throw new ArgumentException($"Client type '{clientType}' is not supported."),
    };

    public IEnumerable<string> GetCommandKeys(ClientType clientType) => GetCommandTable(clientType).Keys;

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => GetCommandTable(clientType).Values;
}
