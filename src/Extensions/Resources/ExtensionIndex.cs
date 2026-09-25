// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Resources
{
    [ExtensionVerb("group")]
    [ExtensionVerb("deployment")]
    [ExtensionVerb("rest")]
    public class ExtensionIndex : IExtensionIndex
    {
        private const string ResourcesAssembly = "Microsoft.Azure.Azclips.Extensions.Resources";

        private static Dictionary<string, ParameterMetadata> TenantPowerShellBodyParameters()
        {
            return new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", "Name", typeof(string), "The tenant deployment name. Derived when omitted.", false, ["DeploymentName"], []),
                ["Location"] = new("Location", "Location", typeof(string), "The location used to store tenant deployment metadata.", true, [], []),
                ["TemplateFile"] = new("TemplateFile", "TemplateFile", typeof(string), "Path to an ARM JSON or Bicep template.", false, [], []),
                ["TemplateUri"] = new("TemplateUri", "TemplateUri", typeof(string), "URI of an ARM template.", false, [], []),
                ["TemplateSpecId"] = new("TemplateSpecId", "TemplateSpecId", typeof(string), "Resource ID of a Template Spec.", false, [], []),
                ["TemplateObject"] = new("TemplateObject", "TemplateObject", typeof(object), "Inline ARM template object.", false, [], []),
                ["QueryString"] = new("QueryString", "QueryString", typeof(string), "Query string for a linked template or parameters URI.", false, [], []),
                ["TemplateParameterFile"] = new("ParametersFile", "TemplateParameterFile", typeof(string), "Path to an ARM JSON or Bicep parameter file.", false, [], []),
                ["TemplateParameterUri"] = new("ParametersUri", "TemplateParameterUri", typeof(string), "URI of an ARM parameter file.", false, [], []),
                ["TemplateParameterObject"] = new("ParametersObject", "TemplateParameterObject", typeof(object), "Inline deployment parameter object.", false, [], []),
                ["Parameter"] = new("Parameter", "Parameter", typeof(string[]), "Inline KEY=VALUE parameter overrides.", false, [], []),
                ["ValidationLevel"] = new("ValidationLevel", "ValidationLevel", typeof(string), "Template, Provider, or ProviderNoRbac.", false, [], ["Template", "Provider", "ProviderNoRbac"]),
            };
        }

        private static Dictionary<string, ParameterMetadata> TenantCliBodyParameters()
        {
            return new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", "name", typeof(string), "The tenant deployment name. Derived when omitted.", false, ["n"], []),
                ["location"] = new("Location", "location", typeof(string), "The location used to store tenant deployment metadata.", true, ["l"], []),
                ["template-file"] = new("TemplateFile", "template-file", typeof(string), "Path to an ARM JSON or Bicep template.", false, ["f"], []),
                ["template-uri"] = new("TemplateUri", "template-uri", typeof(string), "URI of an ARM template.", false, ["u"], []),
                ["template-spec"] = new("TemplateSpecId", "template-spec", typeof(string), "Resource ID of a Template Spec.", false, ["s"], []),
                ["query-string"] = new("QueryString", "query-string", typeof(string), "Query string for a linked template or parameters URI.", false, [], []),
                ["parameters"] = new("Parameter", "parameters", typeof(string[]), "Parameter files, JSON objects, URIs, or KEY=VALUE overrides.", false, ["p"], []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "Template, Provider, or ProviderNoRbac.", false, [], ["Template", "Provider", "ProviderNoRbac"]),
            };
        }

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
        private static readonly CommandMetadata s_newTenantDeployment_PS = new(
            "NewTenantDeploymentProcessor_Orchestrator",
            "New-AzTenantDeployment",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewTenantDeploymentProcessor_Orchestrator",
            ResourcesAssembly,
            typeof(Processors.NewTenantDeploymentProcessor_Orchestrator),
            TenantPowerShellBodyParameters()
                .Concat(new Dictionary<string, ParameterMetadata>
                {
                    ["Tag"] = new("Tags", "Tag", typeof(object), "Deployment tags.", false, [], []),
                    ["DeploymentDebugLogLevel"] = new("DeploymentDebugLogLevel", "DeploymentDebugLogLevel", typeof(string), "RequestContent, ResponseContent, All, or None.", false, [], ["RequestContent", "ResponseContent", "All", "None"]),
                    ["WhatIfResultFormat"] = new("WhatIfResultFormat", "WhatIfResultFormat", typeof(string), "What-if result format.", false, [], ["FullResourcePayloads", "ResourceIdOnly"]),
                    ["WhatIfExcludeChangeType"] = new("WhatIfExcludeChangeType", "WhatIfExcludeChangeType", typeof(string[]), "Change types excluded from what-if output.", false, [], []),
                    ["ProceedIfNoChange"] = new("ProceedIfNoChange", "ProceedIfNoChange", typeof(bool), "Skip confirmation when what-if contains no effective changes.", false, [], []),
                    ["WhatIf"] = new("WhatIf", "WhatIf", typeof(bool), "Run ARM what-if instead of creating the deployment.", false, [], []),
                }).ToDictionary(),
            new Dictionary<string, string> { ["DeploymentName"] = "Name" });
        private static readonly CommandMetadata s_getTenantDeployment_PS = new(
            "GetAtTenantScopeDeploymentsProcessor",
            "Get-AzTenantDeployment_Get",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetAtTenantScopeDeploymentsProcessor",
            ResourcesAssembly,
            typeof(Processors.GetAtTenantScopeDeploymentsProcessor),
            new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("DeploymentName", "Name", typeof(string), "The tenant deployment name.", true, ["DeploymentName"], []),
            },
            new Dictionary<string, string> { ["DeploymentName"] = "Name" });
        private static readonly CommandMetadata s_listTenantDeployment_PS = new(
            "ListAtTenantScopeDeploymentsProcessor",
            "Get-AzTenantDeployment_List",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListAtTenantScopeDeploymentsProcessor",
            ResourcesAssembly,
            typeof(Processors.ListAtTenantScopeDeploymentsProcessor),
            new Dictionary<string, ParameterMetadata>(),
            new Dictionary<string, string>());
        private static readonly CommandMetadata s_stopTenantDeployment_PS = new(
            "StopTenantDeploymentProcessor_Orchestrator",
            "Stop-AzTenantDeployment",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.StopTenantDeploymentProcessor_Orchestrator",
            ResourcesAssembly,
            typeof(Processors.StopTenantDeploymentProcessor_Orchestrator),
            new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("Name", "Name", typeof(string), "The tenant deployment name.", true, ["DeploymentName"], []),
            },
            new Dictionary<string, string> { ["DeploymentName"] = "Name" });
        private static readonly CommandMetadata s_removeTenantDeployment_PS = new(
            "DeleteAtTenantScopeDeploymentsProcessor_Customization",
            "Remove-AzTenantDeployment",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeleteAtTenantScopeDeploymentsProcessor_Customization",
            ResourcesAssembly,
            typeof(Processors.DeleteAtTenantScopeDeploymentsProcessor_Customization),
            new Dictionary<string, ParameterMetadata>
            {
                ["Name"] = new("DeploymentName", "Name", typeof(string), "The tenant deployment name.", true, ["DeploymentName"], []),
            },
            new Dictionary<string, string> { ["DeploymentName"] = "Name" });
        private static readonly CommandMetadata s_testTenantDeployment_PS = new(
            "TestTenantDeploymentProcessor_Orchestrator",
            "Test-AzTenantDeployment",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.TestTenantDeploymentProcessor_Orchestrator",
            ResourcesAssembly,
            typeof(Processors.TestTenantDeploymentProcessor_Orchestrator),
            TenantPowerShellBodyParameters(),
            new Dictionary<string, string> { ["DeploymentName"] = "Name" });

        private static readonly CommandMetadata s_newTenantDeployment_CLI = new(
            "NewTenantDeploymentProcessor_Orchestrator",
            "deployment tenant create",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewTenantDeploymentProcessor_Orchestrator",
            ResourcesAssembly,
            typeof(Processors.NewTenantDeploymentProcessor_Orchestrator),
            TenantCliBodyParameters()
                .Concat(new Dictionary<string, ParameterMetadata>
                {
                    ["what-if"] = new("WhatIf", "what-if", typeof(bool), "Run what-if instead of creating.", false, ["w"], []),
                    ["confirm-with-what-if"] = new("ConfirmWithWhatIf", "confirm-with-what-if", typeof(bool), "Run what-if and confirm before creating.", false, ["c"], []),
                    ["proceed-if-no-change"] = new("ProceedIfNoChange", "proceed-if-no-change", typeof(bool), "Skip confirmation when no effective changes are predicted.", false, [], []),
                    ["what-if-result-format"] = new("WhatIfResultFormat", "what-if-result-format", typeof(string), "What-if result format.", false, ["r"], ["FullResourcePayloads", "ResourceIdOnly"]),
                    ["what-if-exclude-change-types"] = new("WhatIfExcludeChangeType", "what-if-exclude-change-types", typeof(string[]), "Change types excluded from what-if output.", false, ["x"], []),
                }).ToDictionary(),
            new Dictionary<string, string>
            {
                ["n"] = "name", ["l"] = "location", ["f"] = "template-file", ["u"] = "template-uri",
                ["s"] = "template-spec", ["p"] = "parameters", ["w"] = "what-if",
                ["c"] = "confirm-with-what-if", ["r"] = "what-if-result-format",
                ["x"] = "what-if-exclude-change-types",
            });
        private static readonly CommandMetadata s_listTenantDeployment_CLI = new(
            "ListAtTenantScopeDeploymentsProcessor",
            "deployment tenant list",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListAtTenantScopeDeploymentsProcessor",
            ResourcesAssembly,
            typeof(Processors.ListAtTenantScopeDeploymentsProcessor),
            new Dictionary<string, ParameterMetadata>
            {
                ["filter"] = new("_filter", "filter", typeof(string), "OData deployment filter.", false, [], []),
            },
            new Dictionary<string, string>());
        private static readonly CommandMetadata s_getTenantDeployment_CLI = new(
            "GetAtTenantScopeDeploymentsProcessor",
            "deployment tenant show",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetAtTenantScopeDeploymentsProcessor",
            ResourcesAssembly,
            typeof(Processors.GetAtTenantScopeDeploymentsProcessor),
            new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("DeploymentName", "name", typeof(string), "The tenant deployment name.", true, ["n"], []),
            },
            new Dictionary<string, string> { ["n"] = "name" });
        private static readonly CommandMetadata s_cancelTenantDeployment_CLI = new(
            "CancelAtTenantScopeDeploymentsProcessor",
            "deployment tenant cancel",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.CancelAtTenantScopeDeploymentsProcessor",
            ResourcesAssembly,
            typeof(Processors.CancelAtTenantScopeDeploymentsProcessor),
            new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("DeploymentName", "name", typeof(string), "The tenant deployment name.", true, ["n"], []),
            },
            new Dictionary<string, string> { ["n"] = "name" });
        private static readonly CommandMetadata s_deleteTenantDeployment_CLI = new(
            "DeleteAtTenantScopeDeploymentsProcessor_Customization",
            "deployment tenant delete",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeleteAtTenantScopeDeploymentsProcessor_Customization",
            ResourcesAssembly,
            typeof(Processors.DeleteAtTenantScopeDeploymentsProcessor_Customization),
            new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("DeploymentName", "name", typeof(string), "The tenant deployment name.", true, ["n"], []),
            },
            new Dictionary<string, string> { ["n"] = "name" });
        private static readonly CommandMetadata s_exportTenantDeployment_CLI = new(
            "ExportTemplateAtTenantScopeDeploymentsProcessor_Customization",
            "deployment tenant export",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ExportTemplateAtTenantScopeDeploymentsProcessor_Customization",
            ResourcesAssembly,
            typeof(Processors.ExportTemplateAtTenantScopeDeploymentsProcessor_Customization),
            new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("DeploymentName", "name", typeof(string), "The tenant deployment name.", true, ["n"], []),
            },
            new Dictionary<string, string> { ["n"] = "name" });
        private static readonly CommandMetadata s_validateTenantDeployment_CLI = new(
            "ValidateTenantDeploymentProcessor_Orchestrator",
            "deployment tenant validate",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ValidateTenantDeploymentProcessor_Orchestrator",
            ResourcesAssembly,
            typeof(Processors.ValidateTenantDeploymentProcessor_Orchestrator),
            TenantCliBodyParameters(),
            new Dictionary<string, string>
            {
                ["n"] = "name", ["l"] = "location", ["f"] = "template-file",
                ["u"] = "template-uri", ["s"] = "template-spec", ["p"] = "parameters",
            });
        private static readonly CommandMetadata s_whatIfTenantDeployment_CLI = new(
            "WhatIfTenantDeploymentProcessor_Orchestrator",
            "deployment tenant what-if",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.WhatIfTenantDeploymentProcessor_Orchestrator",
            ResourcesAssembly,
            typeof(Processors.WhatIfTenantDeploymentProcessor_Orchestrator),
            TenantCliBodyParameters()
                .Concat(new Dictionary<string, ParameterMetadata>
                {
                    ["result-format"] = new("ResultFormat", "result-format", typeof(string), "What-if result format.", false, ["r"], ["FullResourcePayloads", "ResourceIdOnly"]),
                    ["exclude-change-types"] = new("ExcludeChangeTypes", "exclude-change-types", typeof(string[]), "Change types excluded from output.", false, ["x"], []),
                }).ToDictionary(),
            new Dictionary<string, string>
            {
                ["n"] = "name", ["l"] = "location", ["f"] = "template-file", ["u"] = "template-uri",
                ["s"] = "template-spec", ["p"] = "parameters", ["r"] = "result-format",
                ["x"] = "exclude-change-types",
            });
        private static readonly CommandMetadata s_waitTenantDeployment_CLI = new(
            "WaitTenantDeploymentProcessor",
            "deployment tenant wait",
            [],
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.WaitTenantDeploymentProcessor",
            ResourcesAssembly,
            typeof(Processors.WaitTenantDeploymentProcessor),
            new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", "name", typeof(string), "The tenant deployment name.", true, ["n"], []),
                ["created"] = new("Created", "created", typeof(bool), "Wait until the deployment exists.", false, [], []),
                ["deleted"] = new("Deleted", "deleted", typeof(bool), "Wait until the deployment is deleted.", false, [], []),
                ["exists"] = new("Exists", "exists", typeof(bool), "Wait until the deployment exists.", false, [], []),
                ["updated"] = new("Updated", "updated", typeof(bool), "Wait until provisioning succeeds.", false, [], []),
                ["custom"] = new("Custom", "custom", typeof(string), "Wait for properties.provisioningState==VALUE.", false, [], []),
                ["interval"] = new("Interval", "interval", typeof(int), "Polling interval in seconds.", false, [], []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Timeout in seconds.", false, [], []),
            },
            new Dictionary<string, string> { ["n"] = "name" });
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
            ["New-AzTenantDeployment"] = s_newTenantDeployment_PS,
            ["Get-AzTenantDeployment_Get"] = s_getTenantDeployment_PS,
            ["Get-AzTenantDeployment_List"] = s_listTenantDeployment_PS,
            ["Stop-AzTenantDeployment"] = s_stopTenantDeployment_PS,
            ["Remove-AzTenantDeployment"] = s_removeTenantDeployment_PS,
            ["Test-AzTenantDeployment"] = s_testTenantDeployment_PS,
            ["Invoke-AzRestMethod"] = s_rest_PS,
        };
        // Full-path keys. Each key is `{verb} {path}` where `verb` matches
        // an [ExtensionVerb] declaration on this class; the dispatcher does longest-prefix match on the
        // (verb, path) tuple via Core.Extensions.LeafIndex. Resources contributes both the
        // "group" verb (resource group CRUD) and the "deployment" verb (resource-group-scoped
        // ARM/Bicep deployments).
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
            ["deployment tenant create"] = s_newTenantDeployment_CLI,
            ["deployment tenant list"] = s_listTenantDeployment_CLI,
            ["deployment tenant show"] = s_getTenantDeployment_CLI,
            ["deployment tenant cancel"] = s_cancelTenantDeployment_CLI,
            ["deployment tenant delete"] = s_deleteTenantDeployment_CLI,
            ["deployment tenant export"] = s_exportTenantDeployment_CLI,
            ["deployment tenant validate"] = s_validateTenantDeployment_CLI,
            ["deployment tenant what-if"] = s_whatIfTenantDeployment_CLI,
            ["deployment tenant wait"] = s_waitTenantDeployment_CLI,
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
