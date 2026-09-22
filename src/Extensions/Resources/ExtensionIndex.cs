// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;
using NewManagementGroupDeploymentProcessor_Orchestrator = Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewManagementGroupDeploymentProcessor_Orchestrator;
using TestManagementGroupDeploymentProcessor_Orchestrator = Microsoft.Azure.Azclips.Extensions.Resources.Processors.TestManagementGroupDeploymentProcessor_Orchestrator;
using GetManagementGroupDeploymentProcessor = Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetAtManagementGroupScopeDeploymentsProcessor;
using ListManagementGroupDeploymentProcessor = Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListAtManagementGroupScopeDeploymentsProcessor;
using StopManagementGroupDeploymentProcessor = Microsoft.Azure.Azclips.Extensions.Resources.Processors.CancelAtManagementGroupScopeDeploymentsProcessor;
using RemoveManagementGroupDeploymentProcessor_Orchestrator = Microsoft.Azure.Azclips.Extensions.Resources.Customization.RemoveManagementGroupDeploymentProcessor_Orchestrator;
using ExportManagementGroupDeploymentProcessor = Microsoft.Azure.Azclips.Extensions.Resources.Processors.ExportTemplateAtManagementGroupScopeDeploymentsProcessor;
using NewManagementGroupDeploymentWhatIfProcessor_Orchestrator = Microsoft.Azure.Azclips.Extensions.Resources.Customization.NewManagementGroupDeploymentWhatIfProcessor_Orchestrator;
using WaitManagementGroupDeploymentProcessor_Orchestrator = Microsoft.Azure.Azclips.Extensions.Resources.Customization.WaitManagementGroupDeploymentProcessor_Orchestrator;

namespace Microsoft.Azure.Azclips.Extensions.Resources
{
    [ExtensionVerb("group")]
    [ExtensionVerb("deployment")]
    [ExtensionVerb("rest")]
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_newResourceGroup_PS = new(
            Name: "NewResourceGroupProcessor",
            InvocationName: "New-AzResourceGroup",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]           = new("Name", "Name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]       = new("Location", "Location", typeof(string), "Location for resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_setResourceGroup_PS = new(
            Name: "SetResourceGroupProcessor_Orchestrator",
            InvocationName: "Set-AzResourceGroup",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.SetResourceGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.SetResourceGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]           = new("ResourceGroupName", "Name", typeof(string), "The name of the resource group.", IsRequired: false, Aliases: ["ResourceGroupName"], AllowedValues: []),
                ["Id"]             = new("Id", "Id", typeof(string), "The resource group ID.", IsRequired: false, Aliases: ["ResourceGroupId", "ResourceId"], AllowedValues: []),
                ["Tag"]            = new("Tags", "Tag", typeof(Dictionary<string, string>), "The complete set of resource group tags.", IsRequired: true, Aliases: ["Tags"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceGroupName"] = "Name",
                ["ResourceGroupId"] = "Id",
                ["ResourceId"] = "Id",
                ["Tags"] = "Tag",
            }
        );
        private static readonly CommandMetadata s_removeResourceGroup_PS = new(
            Name: "DeleteResourceGroupProcessor_Orchestrator",
            InvocationName: "Remove-AzResourceGroup",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeleteResourceGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.DeleteResourceGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("ResourceGroupName", "Name", typeof(string), "The name of the resource group.", IsRequired: false, Aliases: ["ResourceGroupName"], AllowedValues: []),
                ["Id"]                = new("Id", "Id", typeof(string), "The resource group ID.", IsRequired: false, Aliases: ["ResourceGroupId", "ResourceId"], AllowedValues: []),
                ["ForceDeletionType"] = new("ForceDeletionTypes", "ForceDeletionType", typeof(string), "The resource types to force delete.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceGroupName"] = "Name",
                ["ResourceGroupId"] = "Id",
                ["ResourceId"] = "Id",
            }
        );
        private static readonly CommandMetadata s_getResourceGroup_PS = new(
            Name: "GetResourceGroupProcessor",
            InvocationName: "Get-AzResourceGroup_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.GetResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]           = new("Name", "Name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listResourceGroup_PS = new(
            Name: "ListResourceGroupProcessor",
            InvocationName: "Get-AzResourceGroup_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newResourceGroupDeployment_PS = new(
            Name: "NewResourceGroupDeploymentProcessor_Orchestrator",
            InvocationName: "New-AzResourceGroupDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The name of the resource group to deploy into.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["TemplateUri"]       = new("TemplateUri", "TemplateUri", typeof(string), "The URI of the ARM template to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateFile"]      = new("TemplateFile", "TemplateFile", typeof(string), "The path to a local ARM template file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateSpecId"]    = new("TemplateSpecId", "TemplateSpecId", typeof(string), "The resource id of a Template Spec to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["QueryString"]       = new("QueryString", "QueryString", typeof(string), "The query string (such as a SAS token) to use with the template URI and/or the parameters URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]     = new("ParametersUri", "ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersFile"]    = new("ParametersFile", "ParametersFile", typeof(string), "The path to a local parameters file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Mode"]              = new("Mode", "Mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["Parameter"]         = new("Parameter", "Parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RollbackOnError"]   = new("RollbackOnError", "RollbackOnError", typeof(bool), "Roll the resource group back to the last successful deployment on error.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RollbackToDeploymentName"] = new("RollbackToDeploymentName", "RollbackToDeploymentName", typeof(string), "Roll the resource group back to a specific named deployment on error. Mutually exclusive with RollbackOnError.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"]   = new("ValidationLevel", "ValidationLevel", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newManagementGroupDeployment_PS = new(
            Name: nameof(NewManagementGroupDeploymentProcessor_Orchestrator),
            InvocationName: "New-AzManagementGroupDeployment",
            Aliases: [],
            FullName: typeof(NewManagementGroupDeploymentProcessor_Orchestrator).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(NewManagementGroupDeploymentProcessor_Orchestrator),
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
                ["Mode"] = new("ModeInput", "Mode", typeof(string), "Incremental or Complete.", false, [], ["Incremental", "Complete"]),
                ["ValidationLevel"] = new("ValidationLevel", "ValidationLevel", typeof(string), "Template, Provider, or ProviderNoRbac.", false, [], ["Template", "Provider", "ProviderNoRbac"]),
                ["DeploymentDebugLogLevel"] = new("DeploymentDebugLogLevel", "DeploymentDebugLogLevel", typeof(string), "None, RequestContent, ResponseContent, or All.", false, [], ["None", "RequestContent", "ResponseContent", "All"]),
                ["Tag"] = new("Tag", "Tag", typeof(object), "Deployment tags.", false, [], []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_testManagementGroupDeployment_PS = new(
            Name: nameof(TestManagementGroupDeploymentProcessor_Orchestrator),
            InvocationName: "Test-AzManagementGroupDeployment",
            Aliases: [],
            FullName: typeof(TestManagementGroupDeploymentProcessor_Orchestrator).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(TestManagementGroupDeploymentProcessor_Orchestrator),
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
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getManagementGroupDeployment_PS = new(
            Name: nameof(GetManagementGroupDeploymentProcessor),
            InvocationName: "Get-AzManagementGroupDeployment_Get",
            Aliases: [],
            FullName: typeof(GetManagementGroupDeploymentProcessor).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(GetManagementGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ManagementGroupId"] = new("ManagementGroupId", "ManagementGroupId", typeof(string), "The management group ID.", true, [], []),
                ["Name"] = new("Name", "Name", typeof(string), "The deployment name.", true, ["DeploymentName"], []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listManagementGroupDeployment_PS = new(
            Name: nameof(ListManagementGroupDeploymentProcessor),
            InvocationName: "Get-AzManagementGroupDeployment_List",
            Aliases: [],
            FullName: typeof(ListManagementGroupDeploymentProcessor).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(ListManagementGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ManagementGroupId"] = new("ManagementGroupId", "ManagementGroupId", typeof(string), "The management group ID.", true, [], []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_stopManagementGroupDeployment_PS = new(
            Name: nameof(StopManagementGroupDeploymentProcessor),
            InvocationName: "Stop-AzManagementGroupDeployment",
            Aliases: [],
            FullName: typeof(StopManagementGroupDeploymentProcessor).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(StopManagementGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ManagementGroupId"] = new("ManagementGroupId", "ManagementGroupId", typeof(string), "The management group ID.", true, [], []),
                ["Name"] = new("Name", "Name", typeof(string), "The deployment name.", true, ["DeploymentName"], []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_removeManagementGroupDeployment_PS = new(
            Name: nameof(RemoveManagementGroupDeploymentProcessor_Orchestrator),
            InvocationName: "Remove-AzManagementGroupDeployment",
            Aliases: [],
            FullName: typeof(RemoveManagementGroupDeploymentProcessor_Orchestrator).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(RemoveManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ManagementGroupId"] = new("ManagementGroupId", "ManagementGroupId", typeof(string), "The management group ID.", true, [], []),
                ["Name"] = new("Name", "Name", typeof(string), "The deployment name.", true, ["DeploymentName"], []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_rest_PS = new(
            Name: "RestProcessor_Orchestrator",
            InvocationName: "Invoke-AzRestMethod",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.RestProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.RestProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Method"]  = new("Method", "Method", typeof(string), "HTTP method (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS). Defaults to GET.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Uri"]     = new("Uri", "Uri", typeof(string), "Absolute URL or relative ARM path. Relative paths are prefixed with https://management.azure.com. The token {subscriptionId} is replaced with the current subscription. Mutually exclusive with the piece-wise ByParameters inputs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Payload"] = new("Payload", "Payload", typeof(string), "Optional request body (typically JSON); sent whenever provided, for any method.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Headers"] = new("Headers", "Headers", typeof(List<string>), "Optional request headers in KEY=VALUE form.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubscriptionId"]       = new("SubscriptionId", "SubscriptionId", typeof(string), "Subscription id used to build an ARM URL from piece-wise parameters. Defaults to the current subscription. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]    = new("ResourceGroupName", "ResourceGroupName", typeof(string), "Resource group name segment for a piece-wise ARM URL. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceProviderName"] = new("ResourceProviderName", "ResourceProviderName", typeof(string), "Resource provider namespace (e.g. Microsoft.Resources) for a piece-wise ARM URL. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceType"]         = new("ResourceType", "ResourceType", typeof(List<string>), "Resource type segments, interleaved with Name, for a piece-wise ARM URL. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                 = new("Name", "Name", typeof(List<string>), "Resource name segments, interleaved with ResourceType (may have one fewer entry for a collection GET). Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ApiVersion"]           = new("ApiVersion", "ApiVersion", typeof(string), "api-version for a piece-wise ARM URL. Required when building from parameters. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UriParameters"]        = new("UriParameters", "UriParameters", typeof(List<string>), "Optional query-string parameters in KEY=VALUE form, appended to the final URL. Allowed in both URI and ByParameters modes.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newResourceGroupDeploymentWhatIf_PS = new(
            Name: "NewResourceGroupDeploymentWhatIfProcessor_Orchestrator",
            InvocationName: "New-AzResourceGroupDeploymentWhatIf",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupDeploymentWhatIfProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentWhatIfProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The name of the resource group to run the what-if against.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["TemplateUri"]       = new("TemplateUri", "TemplateUri", typeof(string), "The URI of the ARM template to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateFile"]      = new("TemplateFile", "TemplateFile", typeof(string), "The path to a local ARM template file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateSpecId"]    = new("TemplateSpecId", "TemplateSpecId", typeof(string), "The resource id of a Template Spec to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["QueryString"]       = new("QueryString", "QueryString", typeof(string), "The query string (such as a SAS token) to use with the template URI and/or the parameters URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]     = new("ParametersUri", "ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersFile"]    = new("ParametersFile", "ParametersFile", typeof(string), "The path to a local parameters file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Mode"]              = new("Mode", "Mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["Parameter"]         = new("Parameter", "Parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"]   = new("ValidationLevel", "ValidationLevel", typeof(string), "The validation level of the what-if operation.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["ResultFormat"]      = new("ResultFormat", "ResultFormat", typeof(string), "The format of the what-if results. FullResourcePayloads (default) or ResourceIdOnly.", IsRequired: false, Aliases: [], AllowedValues: ["FullResourcePayloads", "ResourceIdOnly"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_newResourceGroup_CLI = new(
            Name: "NewResourceGroupProcessor",
            InvocationName: "group create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]        = new("Location", "location", typeof(string), "Location for resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_updateResourceGroup_CLI = new(
            Name: "UpdateResourceGroupProcessor_Orchestrator",
            InvocationName: "group update",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.UpdateResourceGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.UpdateResourceGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("ResourceGroupName", "name", typeof(string), "Name of resource group.", IsRequired: true, Aliases: ["n", "resource-group", "g"], AllowedValues: []),
                ["tags"]            = new("TagsInput", "tags", typeof(string[]), "Space-separated tags in key[=value] format. Use an empty string to clear existing tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["set"]             = new("Set", "set", typeof(string[]), "Update a property by specifying a property path and value.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["force-string"]    = new("ForceString", "force-string", typeof(bool), "Preserve string literals when using --set instead of attempting to convert them to JSON.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["resource-group"] = "name",
                ["g"] = "name",
            }
        );
        private static readonly CommandMetadata s_getResourceGroup_CLI = new(
            Name: "GetResourceGroupProcessor",
            InvocationName: "group show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.GetResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_existsResourceGroup_CLI = new(
            Name: "ExistsResourceGroupProcessor_Orchestrator",
            InvocationName: "group exists",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ExistsResourceGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ExistsResourceGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("ResourceGroupName", "name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: ["n", "resource-group", "g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["resource-group"] = "name",
                ["g"] = "name",
            }
        );
        private static readonly CommandMetadata s_deleteResourceGroup_CLI = new(
            Name: "DeleteResourceGroupProcessor_Orchestrator",
            InvocationName: "group delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeleteResourceGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.DeleteResourceGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]      = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                 = new("ResourceGroupName", "name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: ["n", "resource-group", "g"], AllowedValues: []),
                ["force-deletion-types"] = new("ForceDeletionTypes", "force-deletion-types", typeof(string), "The resource type to force delete.", IsRequired: false, Aliases: ["f"], AllowedValues: ["Microsoft.Compute/virtualMachines", "Microsoft.Compute/virtualMachineScaleSets", "Microsoft.Databricks/workspaces"]),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["resource-group"] = "name",
                ["g"] = "name",
                ["f"] = "force-deletion-types",
            }
        );
        private static readonly CommandMetadata s_listResourceGroup_CLI = new(
            Name: "ListResourceGroupProcessor",
            InvocationName: "group list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newResourceGroupDeployment_CLI = new(
            Name: "NewResourceGroupDeploymentProcessor_Orchestrator",
            InvocationName: "deployment group create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]  = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]   = new("ResourceGroupName", "resource-group", typeof(string), "The name of the resource group to deploy into.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]             = new("Name", "name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["template-uri"]     = new("TemplateUri", "template-uri", typeof(string), "The URI of the ARM template to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-file"]    = new("TemplateFile", "template-file", typeof(string), "The path to a local ARM template file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-spec-id"] = new("TemplateSpecId", "template-spec-id", typeof(string), "The resource id of a Template Spec to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["query-string"]     = new("QueryString", "query-string", typeof(string), "The query string (such as a SAS token) to use with the template URI and/or the parameters URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersUri", "parameters-uri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", "parameters-file", typeof(string), "The path to a local parameters file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["mode"]             = new("Mode", "mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["parameter"]        = new("Parameter", "parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rollback-on-error"] = new("RollbackOnError", "rollback-on-error", typeof(bool), "Roll the resource group back to the last successful deployment on error.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rollback-to-deployment-name"] = new("RollbackToDeploymentName", "rollback-to-deployment-name", typeof(string), "Roll the resource group back to a specific named deployment on error. Mutually exclusive with rollback-on-error.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newManagementGroupDeployment_CLI = new(
            Name: nameof(NewManagementGroupDeploymentProcessor_Orchestrator),
            InvocationName: "deployment mg create",
            Aliases: [],
            FullName: typeof(NewManagementGroupDeploymentProcessor_Orchestrator).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(NewManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", true, ["m"], []),
                ["name"] = new("Name", "name", typeof(string), "The deployment name.", false, ["n"], []),
                ["location"] = new("Location", "location", typeof(string), "The location used to store deployment metadata.", true, ["l"], []),
                ["template-file"] = new("TemplateFile", "template-file", typeof(string), "A local ARM JSON or Bicep template.", false, ["f"], []),
                ["template-uri"] = new("TemplateUri", "template-uri", typeof(string), "The URI of an ARM template.", false, ["u"], []),
                ["template-spec"] = new("TemplateSpecId", "template-spec", typeof(string), "The resource ID of a Template Spec.", false, ["s"], []),
                ["query-string"] = new("QueryString", "query-string", typeof(string), "A query string used with template-uri.", false, [], []),
                ["parameters"] = new("Parameter", "parameters", typeof(string[]), "Parameter files, JSON, or KEY=VALUE overrides.", false, ["p"], []),
                ["mode"] = new("ModeInput", "mode", typeof(string), "Incremental or Complete.", false, [], ["Incremental", "Complete"]),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "Template, Provider, or ProviderNoRbac.", false, [], ["Template", "Provider", "ProviderNoRbac"]),
                ["no-wait"] = new("NoWait", "no-wait", typeof(bool), "Return after ARM accepts the deployment.", false, [], []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["m"] = "management-group-id",
                ["n"] = "name",
                ["l"] = "location",
                ["f"] = "template-file",
                ["u"] = "template-uri",
                ["s"] = "template-spec",
                ["p"] = "parameters",
            }
        );
        private static readonly CommandMetadata s_testManagementGroupDeployment_CLI = new(
            Name: nameof(TestManagementGroupDeploymentProcessor_Orchestrator),
            InvocationName: "deployment mg validate",
            Aliases: [],
            FullName: typeof(TestManagementGroupDeploymentProcessor_Orchestrator).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(TestManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", true, ["m"], []),
                ["name"] = new("Name", "name", typeof(string), "The deployment name.", false, ["n"], []),
                ["location"] = new("Location", "location", typeof(string), "The location used to store deployment metadata.", true, ["l"], []),
                ["template-file"] = new("TemplateFile", "template-file", typeof(string), "A local ARM JSON or Bicep template.", false, ["f"], []),
                ["template-uri"] = new("TemplateUri", "template-uri", typeof(string), "The URI of an ARM template.", false, ["u"], []),
                ["template-spec"] = new("TemplateSpecId", "template-spec", typeof(string), "The resource ID of a Template Spec.", false, ["s"], []),
                ["query-string"] = new("QueryString", "query-string", typeof(string), "A query string used with template-uri.", false, [], []),
                ["parameters"] = new("Parameter", "parameters", typeof(string[]), "Parameter files, JSON, or KEY=VALUE overrides.", false, ["p"], []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "Template, Provider, or ProviderNoRbac.", false, [], ["Template", "Provider", "ProviderNoRbac"]),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["m"] = "management-group-id",
                ["n"] = "name",
                ["l"] = "location",
                ["f"] = "template-file",
                ["u"] = "template-uri",
                ["s"] = "template-spec",
                ["p"] = "parameters",
            }
        );
        private static readonly CommandMetadata s_getManagementGroupDeployment_CLI = new(
            Name: nameof(GetManagementGroupDeploymentProcessor),
            InvocationName: "deployment mg show",
            Aliases: [],
            FullName: typeof(GetManagementGroupDeploymentProcessor).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(GetManagementGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", true, ["m"], []),
                ["name"] = new("Name", "name", typeof(string), "The deployment name.", true, ["n"], []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["m"] = "management-group-id",
                ["n"] = "name",
            }
        );
        private static readonly CommandMetadata s_listManagementGroupDeployment_CLI = new(
            Name: nameof(ListManagementGroupDeploymentProcessor),
            InvocationName: "deployment mg list",
            Aliases: [],
            FullName: typeof(ListManagementGroupDeploymentProcessor).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(ListManagementGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", true, ["m"], []),
                ["filter"] = new("Filter", "filter", typeof(string), "Filter by provisioning state.", false, [], []),
                ["top"] = new("Top", "top", typeof(int), "The maximum number of results per page.", false, [], []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["m"] = "management-group-id",
            }
        );
        private static readonly CommandMetadata s_stopManagementGroupDeployment_CLI = new(
            Name: nameof(StopManagementGroupDeploymentProcessor),
            InvocationName: "deployment mg cancel",
            Aliases: [],
            FullName: typeof(StopManagementGroupDeploymentProcessor).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(StopManagementGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", true, ["m"], []),
                ["name"] = new("Name", "name", typeof(string), "The deployment name.", true, ["n"], []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["m"] = "management-group-id",
                ["n"] = "name",
            }
        );
        private static readonly CommandMetadata s_removeManagementGroupDeployment_CLI = new(
            Name: nameof(RemoveManagementGroupDeploymentProcessor_Orchestrator),
            InvocationName: "deployment mg delete",
            Aliases: [],
            FullName: typeof(RemoveManagementGroupDeploymentProcessor_Orchestrator).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(RemoveManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", true, ["m"], []),
                ["name"] = new("Name", "name", typeof(string), "The deployment name.", true, ["n"], []),
                ["no-wait"] = new("NoWait", "no-wait", typeof(bool), "Return after ARM accepts the delete request.", false, [], []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["m"] = "management-group-id",
                ["n"] = "name",
            }
        );
        private static readonly CommandMetadata s_exportManagementGroupDeployment_CLI = new(
            Name: nameof(ExportManagementGroupDeploymentProcessor),
            InvocationName: "deployment mg export",
            Aliases: [],
            FullName: typeof(ExportManagementGroupDeploymentProcessor).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(ExportManagementGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", true, ["m"], []),
                ["name"] = new("Name", "name", typeof(string), "The deployment name.", true, ["n"], []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["m"] = "management-group-id",
                ["n"] = "name",
            }
        );
        private static readonly CommandMetadata s_newManagementGroupDeploymentWhatIf_CLI = new(
            Name: nameof(NewManagementGroupDeploymentWhatIfProcessor_Orchestrator),
            InvocationName: "deployment mg what-if",
            Aliases: [],
            FullName: typeof(NewManagementGroupDeploymentWhatIfProcessor_Orchestrator).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(NewManagementGroupDeploymentWhatIfProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", true, ["m"], []),
                ["name"] = new("Name", "name", typeof(string), "The deployment name.", false, ["n"], []),
                ["location"] = new("Location", "location", typeof(string), "The location used to store deployment metadata.", true, ["l"], []),
                ["template-file"] = new("TemplateFile", "template-file", typeof(string), "A local ARM JSON or Bicep template.", false, ["f"], []),
                ["template-uri"] = new("TemplateUri", "template-uri", typeof(string), "The URI of an ARM template.", false, ["u"], []),
                ["template-spec"] = new("TemplateSpecId", "template-spec", typeof(string), "The resource ID of a Template Spec.", false, ["s"], []),
                ["query-string"] = new("QueryString", "query-string", typeof(string), "A query string used with template-uri.", false, [], []),
                ["parameters"] = new("Parameter", "parameters", typeof(string[]), "Parameter files, JSON, or KEY=VALUE overrides.", false, ["p"], []),
                ["mode"] = new("ModeInput", "mode", typeof(string), "Incremental or Complete.", false, [], ["Incremental", "Complete"]),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "Template, Provider, or ProviderNoRbac.", false, [], ["Template", "Provider", "ProviderNoRbac"]),
                ["result-format"] = new("ResultFormatInput", "result-format", typeof(string), "FullResourcePayloads or ResourceIdOnly.", false, [], ["FullResourcePayloads", "ResourceIdOnly"]),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["m"] = "management-group-id",
                ["n"] = "name",
                ["l"] = "location",
                ["f"] = "template-file",
                ["u"] = "template-uri",
                ["s"] = "template-spec",
                ["p"] = "parameters",
            }
        );
        private static readonly CommandMetadata s_waitManagementGroupDeployment_CLI = new(
            Name: nameof(WaitManagementGroupDeploymentProcessor_Orchestrator),
            InvocationName: "deployment mg wait",
            Aliases: [],
            FullName: typeof(WaitManagementGroupDeploymentProcessor_Orchestrator).FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(WaitManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", true, ["m"], []),
                ["name"] = new("Name", "name", typeof(string), "The deployment name.", true, ["n"], []),
                ["created"] = new("Created", "created", typeof(bool), "Wait until provisioningState is Succeeded.", false, [], []),
                ["updated"] = new("Updated", "updated", typeof(bool), "Wait until provisioningState is Succeeded.", false, [], []),
                ["deleted"] = new("Deleted", "deleted", typeof(bool), "Wait until the deployment no longer exists.", false, [], []),
                ["exists"] = new("Exists", "exists", typeof(bool), "Wait until the deployment exists.", false, [], []),
                ["custom"] = new("Custom", "custom", typeof(string), "Wait for a provisioningState comparison using == or !=.", false, [], []),
                ["interval"] = new("Interval", "interval", typeof(int), "Polling interval in seconds.", false, [], []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Maximum wait in seconds.", false, [], []),
                ["profile"] = new("Profile", "profile", typeof(string), "Use the named profile.", false, [], []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_rest_CLI = new(
            Name: "RestProcessor_Orchestrator",
            InvocationName: "rest",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.RestProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.RestProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["method"]  = new("Method", "method", typeof(string), "HTTP method (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS). Defaults to GET.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["uri"]     = new("Uri", "uri", typeof(string), "Absolute URL or relative ARM path. Relative paths are prefixed with https://management.azure.com. The token {subscriptionId} is replaced with the current subscription. Mutually exclusive with the piece-wise ByParameters flags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["body"]    = new("Payload", "body", typeof(string), "Optional request body (typically JSON); sent whenever provided, for any method.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["headers"] = new("Headers", "headers", typeof(List<string>), "Optional request headers in KEY=VALUE form (space-separated).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subscription"]      = new("SubscriptionId", "subscription", typeof(string), "Subscription id used to build an ARM URL from piece-wise flags. Defaults to the current subscription. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]    = new("ResourceGroupName", "resource-group", typeof(string), "Resource group name segment for a piece-wise ARM URL. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-provider"] = new("ResourceProviderName", "resource-provider", typeof(string), "Resource provider namespace (e.g. Microsoft.Resources) for a piece-wise ARM URL. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-type"]     = new("ResourceType", "resource-type", typeof(List<string>), "Resource type segments (space-separated), interleaved with --name, for a piece-wise ARM URL. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]              = new("Name", "name", typeof(List<string>), "Resource name segments (space-separated), interleaved with --resource-type (may have one fewer entry for a collection GET). Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["api-version"]       = new("ApiVersion", "api-version", typeof(string), "api-version for a piece-wise ARM URL. Required when building from flags. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["uri-parameters"]    = new("UriParameters", "uri-parameters", typeof(List<string>), "Optional query-string parameters in KEY=VALUE form (space-separated), appended to the final URL. Allowed in both --uri and ByParameters modes.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newResourceGroupDeploymentWhatIf_CLI = new(
            Name: "NewResourceGroupDeploymentWhatIfProcessor_Orchestrator",
            InvocationName: "deployment group what-if",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupDeploymentWhatIfProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentWhatIfProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]  = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]   = new("ResourceGroupName", "resource-group", typeof(string), "The name of the resource group to run the what-if against.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]             = new("Name", "name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["template-uri"]     = new("TemplateUri", "template-uri", typeof(string), "The URI of the ARM template to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-file"]    = new("TemplateFile", "template-file", typeof(string), "The path to a local ARM template file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-spec-id"] = new("TemplateSpecId", "template-spec-id", typeof(string), "The resource id of a Template Spec to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["query-string"]     = new("QueryString", "query-string", typeof(string), "The query string (such as a SAS token) to use with the template URI and/or the parameters URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersUri", "parameters-uri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", "parameters-file", typeof(string), "The path to a local parameters file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["mode"]             = new("Mode", "mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["parameter"]        = new("Parameter", "parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "The validation level of the what-if operation.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["result-format"]    = new("ResultFormat", "result-format", typeof(string), "The format of the what-if results. FullResourcePayloads (default) or ResourceIdOnly.", IsRequired: false, Aliases: [], AllowedValues: ["FullResourcePayloads", "ResourceIdOnly"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["New-AzResourceGroup"]      = s_newResourceGroup_PS,
            ["Set-AzResourceGroup"]      = s_setResourceGroup_PS,
            ["Remove-AzResourceGroup"]   = s_removeResourceGroup_PS,
            ["Get-AzResourceGroup_Get"]  = s_getResourceGroup_PS,
            ["Get-AzResourceGroup_List"] = s_listResourceGroup_PS,
            ["New-AzResourceGroupDeployment"] = s_newResourceGroupDeployment_PS,
            ["New-AzResourceGroupDeploymentWhatIf"] = s_newResourceGroupDeploymentWhatIf_PS,
            ["New-AzManagementGroupDeployment"] = s_newManagementGroupDeployment_PS,
            ["Test-AzManagementGroupDeployment"] = s_testManagementGroupDeployment_PS,
            ["Get-AzManagementGroupDeployment_Get"] = s_getManagementGroupDeployment_PS,
            ["Get-AzManagementGroupDeployment_List"] = s_listManagementGroupDeployment_PS,
            ["Stop-AzManagementGroupDeployment"] = s_stopManagementGroupDeployment_PS,
            ["Remove-AzManagementGroupDeployment"] = s_removeManagementGroupDeployment_PS,
            ["Invoke-AzRestMethod"] = s_rest_PS,
        };
        // Full-path keys. Each key is `{verb} {path}` where `verb` matches
        // an [ExtensionVerb] declaration on this class; the dispatcher does longest-prefix match on the
        // (verb, path) tuple via Core.Extensions.LeafIndex. Resources contributes both the
        // "group" verb (resource group CRUD) and the "deployment" verb (ARM/Bicep deployments).
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["group create"]              = s_newResourceGroup_CLI,
            ["group delete"]              = s_deleteResourceGroup_CLI,
            ["group exists"]              = s_existsResourceGroup_CLI,
            ["group list"]                = s_listResourceGroup_CLI,
            ["group show"]                = s_getResourceGroup_CLI,
            ["group update"]              = s_updateResourceGroup_CLI,
            ["deployment group create"]   = s_newResourceGroupDeployment_CLI,
            ["deployment group what-if"]  = s_newResourceGroupDeploymentWhatIf_CLI,
            ["deployment mg create"]      = s_newManagementGroupDeployment_CLI,
            ["deployment mg validate"]    = s_testManagementGroupDeployment_CLI,
            ["deployment mg show"]        = s_getManagementGroupDeployment_CLI,
            ["deployment mg list"]        = s_listManagementGroupDeployment_CLI,
            ["deployment mg cancel"]      = s_stopManagementGroupDeployment_CLI,
            ["deployment mg delete"]      = s_removeManagementGroupDeployment_CLI,
            ["deployment mg export"]      = s_exportManagementGroupDeployment_CLI,
            ["deployment mg what-if"]     = s_newManagementGroupDeploymentWhatIf_CLI,
            ["deployment mg wait"]        = s_waitManagementGroupDeployment_CLI,
            // rest is a special command: the command group word itself is the verb (az rest ...).
            ["rest"] = s_rest_CLI,
        };

        // Per-processor --profile / -Profile override (workaround until a global-parameter framework
        // exists). Injected into every command so Resource processors accept the parameter; the Engine
        // resolves the named profile before the processor runs.
        static ExtensionIndex()
        {
            foreach (var metadata in s_cliTable.Values)
            {
                metadata.Parameters.TryAdd("profile", new ParameterMetadata("Profile", "profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
            }
            foreach (var metadata in s_psTable.Values)
            {
                metadata.Parameters.TryAdd("Profile", new ParameterMetadata("Profile", "Profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
            }
        }
        // ── Lookup ──
        public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
        {
            var table = clientType switch
            {
                ClientType.PowerShell => s_psTable,
                ClientType.CLI => s_cliTable,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
            return table.TryGetValue(name, out var metadata)
                ? metadata
                : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
        }
        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable,
                ClientType.CLI => s_cliTable,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }
        public IEnumerable<string> GetCommandKeys(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable.Keys,
                ClientType.CLI => s_cliTable.Keys,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
        {
            return GetCommandTable(clientType).Values;
        }
    }
}
