// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Resources
{
    [ExtensionVerb("group")]
    [ExtensionVerb("deployment")]
    [ExtensionVerb("role")]
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
        private static readonly CommandMetadata s_listRoleAssignment_PS = new(
            Name: "ListRoleAssignmentProcessor_Orchestrator",
            InvocationName: "Get-AzRoleAssignment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListRoleAssignmentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListRoleAssignmentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Scope"]             = new("Scope", "Scope", typeof(string), "Fully qualified scope to list role assignments at. Mutually exclusive with -ResourceGroupName. Defaults to the subscription scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "List role assignments scoped to this resource group under the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ObjectId"]          = new("AssigneeObjectId", "ObjectId", typeof(string), "Filter to role assignments for this principal's object (principal) ID (GUID).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RoleDefinitionId"]  = new("RoleDefinitionId", "RoleDefinitionId", typeof(string), "Filter to role assignments for this role definition ID (GUID). Role name resolution is not yet supported.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IncludeInherited"]  = new("IncludeInherited", "IncludeInherited", typeof(bool), "Include role assignments inherited from ancestor scopes. By default only assignments at the exact scope are listed.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ExpandPrincipalGroups"] = new("IncludeGroups", "ExpandPrincipalGroups", typeof(bool), "When filtering by -ObjectId, also include assignments granted through groups the principal belongs to (transitive).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IncludeNestedScopes"] = new("All", "IncludeNestedScopes", typeof(bool), "List role assignments across the whole subscription, including every nested scope. Cannot be combined with -Scope or -ResourceGroupName.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newRoleAssignment_PS = new(
            Name: "NewRoleAssignmentProcessor_Orchestrator",
            InvocationName: "New-AzRoleAssignment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewRoleAssignmentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewRoleAssignmentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Scope"]             = new("Scope", "Scope", typeof(string), "Fully qualified scope to create the role assignment at. Mutually exclusive with -ResourceGroupName. Defaults to the subscription scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "Create the role assignment scoped to this resource group under the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ObjectId"]          = new("AssigneeObjectId", "ObjectId", typeof(string), "The principal's object (principal) ID (GUID) to assign the role to.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["RoleDefinitionId"]  = new("RoleDefinitionId", "RoleDefinitionId", typeof(string), "The role to assign: a role definition ID (GUID) or a fully-qualified role definition ID. Role name resolution is not yet supported.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ObjectType"]        = new("PrincipalType", "ObjectType", typeof(string), "The principal type of the assigned principal (User, Group, ServicePrincipal, or ForeignGroup).", IsRequired: false, Aliases: [], AllowedValues: ["User", "Group", "ServicePrincipal", "ForeignGroup"]),
                ["Name"]              = new("Name", "Name", typeof(string), "The role assignment name (a GUID). Generated when omitted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Description"]       = new("Description", "Description", typeof(string), "Description of the role assignment.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Condition"]         = new("Condition", "Condition", typeof(string), "Condition under which the principal is granted permission.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ConditionVersion"]  = new("ConditionVersion", "ConditionVersion", typeof(string), "Version of the condition syntax (currently only '2.0').", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteRoleAssignment_PS = new(
            Name: "DeleteRoleAssignmentProcessor_Orchestrator",
            InvocationName: "Remove-AzRoleAssignment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeleteRoleAssignmentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.DeleteRoleAssignmentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Scope"]             = new("Scope", "Scope", typeof(string), "Fully qualified scope to delete the role assignment from. Mutually exclusive with -ResourceGroupName. Defaults to the subscription scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "Delete the role assignment scoped to this resource group under the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The role assignment name (a GUID) to delete.", IsRequired: true, Aliases: [], AllowedValues: []),
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
                ["QueryString"]       = new("QueryString", "QueryString", typeof(string), "The query string (such as a SAS token) to use with the template URI. Only valid together with a template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]     = new("ParametersUri", "ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersFile"]    = new("ParametersFile", "ParametersFile", typeof(string), "The path to a local parameters file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Mode"]              = new("Mode", "Mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["Parameter"]         = new("Parameter", "Parameter", typeof(string[]), "Deployment parameter(s) (repeatable): KEY=VALUE, a raw JSON object, or a path to a JSON parameters file (optionally prefixed with '@'). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
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
                ["QueryString"]       = new("QueryString", "QueryString", typeof(string), "The query string (such as a SAS token) to use with the template URI. Only valid together with a template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]     = new("ParametersUri", "ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersFile"]    = new("ParametersFile", "ParametersFile", typeof(string), "The path to a local parameters file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Mode"]              = new("Mode", "Mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["Parameter"]         = new("Parameter", "Parameter", typeof(string[]), "Deployment parameter(s) (repeatable): KEY=VALUE, a raw JSON object, or a path to a JSON parameters file (optionally prefixed with '@'). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"]   = new("ValidationLevel", "ValidationLevel", typeof(string), "The validation level of the what-if operation.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["ResultFormat"]      = new("ResultFormat", "ResultFormat", typeof(string), "The format of the what-if results. FullResourcePayloads (default) or ResourceIdOnly.", IsRequired: false, Aliases: [], AllowedValues: ["FullResourcePayloads", "ResourceIdOnly"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_testResourceGroupDeployment_PS = new(
            Name: "TestResourceGroupDeploymentProcessor_Orchestrator",
            InvocationName: "Test-AzResourceGroupDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.TestResourceGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.TestResourceGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The name of the resource group to validate the deployment against.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the deployment. Defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateUri"]       = new("TemplateUri", "TemplateUri", typeof(string), "The URI of the ARM template to validate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateFile"]      = new("TemplateFile", "TemplateFile", typeof(string), "The path to a local ARM template file to validate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateSpecId"]    = new("TemplateSpecId", "TemplateSpecId", typeof(string), "The resource id of a Template Spec to validate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateObject"]    = new("TemplateObject", "TemplateObject", typeof(Hashtable), "A hash table holding the ARM template to validate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["QueryString"]       = new("QueryString", "QueryString", typeof(string), "The query string (such as a SAS token) to use with the template URI. Only valid together with a template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]     = new("ParametersUri", "ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: ["TemplateParameterUri"], AllowedValues: []),
                ["ParametersFile"]    = new("ParametersFile", "ParametersFile", typeof(string), "The path to a local parameters file to evaluate inline.", IsRequired: false, Aliases: ["TemplateParameterFile"], AllowedValues: []),
                ["Mode"]              = new("Mode", "Mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["Parameter"]         = new("Parameter", "Parameter", typeof(string[]), "Deployment parameter(s) (repeatable): KEY=VALUE, a raw JSON object, or a path to a JSON parameters file (optionally prefixed with '@'). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"]   = new("ValidationLevel", "ValidationLevel", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["RollbackOnError"]   = new("RollbackOnError", "RollbackOnError", typeof(bool), "Roll the resource group back to the last successful deployment on error.", IsRequired: false, Aliases: ["RollbackToLastDeployment"], AllowedValues: []),
                ["RollbackToDeploymentName"] = new("RollbackToDeploymentName", "RollbackToDeploymentName", typeof(string), "Roll the resource group back to a specific named deployment on error. Mutually exclusive with RollbackOnError.", IsRequired: false, Aliases: ["RollBackDeploymentName"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["TemplateParameterUri"] = "ParametersUri",
                ["TemplateParameterFile"] = "ParametersFile",
                ["RollbackToLastDeployment"] = "RollbackOnError",
                ["RollBackDeploymentName"] = "RollbackToDeploymentName",
            }
        );
        private static readonly CommandMetadata s_getResourceGroupDeployment_PS = new(
            Name: "GetResourceGroupDeploymentProcessor_Orchestrator",
            InvocationName: "Get-AzResourceGroupDeployment_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetResourceGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.GetResourceGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The name of the resource group that contains the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: ["DeploymentName"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["DeploymentName"] = "Name" }
        );
        private static readonly CommandMetadata s_getResourceGroupDeploymentById_PS = new(
            Name: "GetResourceGroupDeploymentProcessor_Orchestrator",
            InvocationName: "Get-AzResourceGroupDeployment_GetById",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetResourceGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.GetResourceGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Id"] = new("Id", "Id", typeof(string), "The fully qualified resource id of the deployment.", IsRequired: true, Aliases: ["DeploymentId", "ResourceId"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["DeploymentId"] = "Id", ["ResourceId"] = "Id" }
        );
        private static readonly CommandMetadata s_listResourceGroupDeployment_PS = new(
            Name: "ListResourceGroupDeploymentProcessor",
            InvocationName: "Get-AzResourceGroupDeployment_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListResourceGroupDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListResourceGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The name of the resource group whose deployments to list.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_stopResourceGroupDeployment_PS = new(
            Name: "StopResourceGroupDeploymentProcessor",
            InvocationName: "Stop-AzResourceGroupDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.StopResourceGroupDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.StopResourceGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The name of the resource group that contains the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the deployment to cancel.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_removeResourceGroupDeployment_PS = new(
            Name: "RemoveResourceGroupDeploymentProcessor_Orchestrator",
            InvocationName: "Remove-AzResourceGroupDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.RemoveResourceGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.RemoveResourceGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The name of the resource group that contains the deployment.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the deployment to delete.", IsRequired: false, Aliases: ["DeploymentName"], AllowedValues: []),
                ["Id"]                = new("Id", "Id", typeof(string), "The fully qualified resource id of the deployment. Provide either -Id or -ResourceGroupName and -Name.", IsRequired: false, Aliases: ["DeploymentId", "ResourceId"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["DeploymentName"] = "Name", ["DeploymentId"] = "Id", ["ResourceId"] = "Id" }
        );
        private static readonly CommandMetadata s_newSubscriptionDeployment_PS = new(
            Name: "NewSubscriptionDeploymentProcessor_Orchestrator",
            InvocationName: "New-AzDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewSubscriptionDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewSubscriptionDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]  = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]            = new("DeploymentName", "Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: ["DeploymentName"], AllowedValues: []),
                ["Location"]        = new("Location", "Location", typeof(string), "The location to store the deployment metadata.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Mode"]            = new("Mode", "Mode", typeof(string), "The mode that is used to deploy resources.", IsRequired: true, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["TemplateUri"]     = new("TemplateLinkUri", "TemplateUri", typeof(string), "The URI of the ARM template to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateFile"]    = new("TemplateFile", "TemplateFile", typeof(string), "The path to a local ARM JSON or Bicep template. Bicep files require the Bicep CLI for local compilation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateSpecId"]  = new("TemplateLinkId", "TemplateSpecId", typeof(string), "The resource id of a Template Spec to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateObject"]  = new("Template", "TemplateObject", typeof(Hashtable), "A hash table holding the ARM template to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["QueryString"]     = new("TemplateLinkQueryString", "QueryString", typeof(string), "The query string (such as a SAS token) to use with the template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]   = new("ParametersLinkUri", "ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: ["TemplateParameterUri"], AllowedValues: []),
                ["ParametersFile"]  = new("ParametersFile", "ParametersFile", typeof(string), "The path to a local ARM JSON or .bicepparam parameters file. A .bicepparam file can supply its own template and requires the Bicep CLI. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: ["TemplateParameterFile"], AllowedValues: []),
                ["Parameter"]       = new("Parameter", "Parameter", typeof(string[]), "A string array of template parameters: KEY=VALUE, a raw JSON object, or a JSON parameters file path (optionally prefixed with '@'). Specify Parameter once with all values. Values are parsed as JSON when possible, otherwise as strings. Later values override earlier values and same-named keys from ParametersFile. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"] = new("ValidationLevel", "ValidationLevel", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["DeploymentName"] = "Name",
                ["TemplateParameterUri"] = "ParametersUri",
                ["TemplateParameterFile"] = "ParametersFile",
            }
        );
        private static readonly CommandMetadata s_testSubscriptionDeployment_PS = new(
            Name: "TestSubscriptionDeploymentProcessor_Orchestrator",
            InvocationName: "Test-AzDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.TestSubscriptionDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.TestSubscriptionDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]  = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]            = new("DeploymentName", "Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: ["DeploymentName"], AllowedValues: []),
                ["Location"]        = new("Location", "Location", typeof(string), "The location to store the deployment metadata.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Mode"]            = new("Mode", "Mode", typeof(string), "The mode that is used to deploy resources.", IsRequired: true, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["TemplateUri"]     = new("TemplateLinkUri", "TemplateUri", typeof(string), "The URI of the ARM template to validate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateFile"]    = new("TemplateFile", "TemplateFile", typeof(string), "The path to a local ARM JSON or Bicep template. Bicep files require the Bicep CLI for local compilation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateSpecId"]  = new("TemplateLinkId", "TemplateSpecId", typeof(string), "The resource id of a Template Spec to validate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateObject"]  = new("Template", "TemplateObject", typeof(Hashtable), "A hash table holding the ARM template to validate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["QueryString"]     = new("TemplateLinkQueryString", "QueryString", typeof(string), "The query string (such as a SAS token) to use with the template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]   = new("ParametersLinkUri", "ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: ["TemplateParameterUri"], AllowedValues: []),
                ["ParametersFile"]  = new("ParametersFile", "ParametersFile", typeof(string), "The path to a local ARM JSON or .bicepparam parameters file. A .bicepparam file can supply its own template and requires the Bicep CLI. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: ["TemplateParameterFile"], AllowedValues: []),
                ["Parameter"]       = new("Parameter", "Parameter", typeof(string[]), "A string array of template parameters: KEY=VALUE, a raw JSON object, or a JSON parameters file path (optionally prefixed with '@'). Specify Parameter once with all values. Values are parsed as JSON when possible, otherwise as strings. Later values override earlier values and same-named keys from ParametersFile. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"] = new("ValidationLevel", "ValidationLevel", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["DeploymentName"] = "Name",
                ["TemplateParameterUri"] = "ParametersUri",
                ["TemplateParameterFile"] = "ParametersFile",
            }
        );
        private static readonly CommandMetadata s_getSubscriptionDeployment_PS = new(
            Name: "GetSubscriptionDeploymentProcessor",
            InvocationName: "Get-AzDeployment_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetSubscriptionDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.GetSubscriptionDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]           = new("Name", "Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: ["DeploymentName"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["DeploymentName"] = "Name" }
        );
        private static readonly CommandMetadata s_listSubscriptionDeployment_PS = new(
            Name: "ListSubscriptionDeploymentProcessor",
            InvocationName: "Get-AzDeployment_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListSubscriptionDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListSubscriptionDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_stopSubscriptionDeployment_PS = new(
            Name: "StopSubscriptionDeploymentProcessor",
            InvocationName: "Stop-AzDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.StopSubscriptionDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.StopSubscriptionDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]           = new("Name", "Name", typeof(string), "The name of the deployment to cancel.", IsRequired: true, Aliases: ["DeploymentName"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["DeploymentName"] = "Name" }
        );
        private static readonly CommandMetadata s_removeSubscriptionDeployment_PS = new(
            Name: "RemoveSubscriptionDeploymentProcessor",
            InvocationName: "Remove-AzDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.RemoveSubscriptionDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.RemoveSubscriptionDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]           = new("Name", "Name", typeof(string), "The name of the deployment to delete.", IsRequired: true, Aliases: ["DeploymentName"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["DeploymentName"] = "Name" }
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
        private static readonly CommandMetadata s_listRoleAssignment_CLI = new(
            Name: "ListRoleAssignmentProcessor_Orchestrator",
            InvocationName: "role assignment list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListRoleAssignmentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListRoleAssignmentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]    = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["scope"]              = new("Scope", "scope", typeof(string), "Fully qualified scope to list role assignments at. Mutually exclusive with --resource-group. Defaults to the subscription scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]     = new("ResourceGroupName", "resource-group", typeof(string), "List role assignments scoped to this resource group under the subscription.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
                ["assignee"]           = new("Assignee", "assignee", typeof(string), "Filter to role assignments for this principal. Accepts an object (principal) ID; name/UPN/service-principal resolution is not yet supported.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["assignee-object-id"] = new("AssigneeObjectId", "assignee-object-id", typeof(string), "Filter to role assignments for this principal's object (principal) ID (GUID).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["role"]               = new("RoleDefinitionId", "role", typeof(string), "Filter to role assignments for this role definition ID (GUID). Role name resolution is not yet supported.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["include-inherited"]  = new("IncludeInherited", "include-inherited", typeof(bool), "Include role assignments inherited from ancestor scopes. By default only assignments at the exact scope are listed.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["all"]                = new("All", "all", typeof(bool), "List role assignments across the whole subscription, including every nested scope. Cannot be combined with --scope or --resource-group.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["include-groups"]     = new("IncludeGroups", "include-groups", typeof(bool), "When filtering by assignee, also include assignments granted through groups the principal belongs to (transitive).", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_newRoleAssignment_CLI = new(
            Name: "NewRoleAssignmentProcessor_Orchestrator",
            InvocationName: "role assignment create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewRoleAssignmentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewRoleAssignmentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]         = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["scope"]                   = new("Scope", "scope", typeof(string), "Fully qualified scope to create the role assignment at (the grant target), e.g. a subscription, resource group, or resource ID. Mutually exclusive with --resource-group. Defaults to the subscription scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]          = new("ResourceGroupName", "resource-group", typeof(string), "Create the role assignment scoped to this resource group under the subscription. Mutually exclusive with --scope.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
                ["assignee"]                = new("Assignee", "assignee", typeof(string), "The principal to assign the role to. Accepts an object (principal) ID; name/UPN/service-principal resolution is not yet supported.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["assignee-object-id"]      = new("AssigneeObjectId", "assignee-object-id", typeof(string), "The principal's object (principal) ID (GUID) to assign the role to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["role"]                    = new("RoleDefinitionId", "role", typeof(string), "The role to assign: a role definition ID (GUID) or a fully-qualified role definition ID. Role name resolution is not yet supported.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["assignee-principal-type"] = new("PrincipalType", "assignee-principal-type", typeof(string), "The principal type of the assignee (User, Group, ServicePrincipal, or ForeignGroup).", IsRequired: false, Aliases: [], AllowedValues: ["User", "Group", "ServicePrincipal", "ForeignGroup"]),
                ["name"]                    = new("Name", "name", typeof(string), "The role assignment name (a GUID). Generated when omitted.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["description"]             = new("Description", "description", typeof(string), "Description of the role assignment.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["condition"]               = new("Condition", "condition", typeof(string), "Condition under which the principal is granted permission.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["condition-version"]       = new("ConditionVersion", "condition-version", typeof(string), "Version of the condition syntax (currently only '2.0').", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
                ["n"] = "name",
            }
        );
        private static readonly CommandMetadata s_deleteRoleAssignment_CLI = new(
            Name: "DeleteRoleAssignmentProcessor_Orchestrator",
            InvocationName: "role assignment delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeleteRoleAssignmentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.DeleteRoleAssignmentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["scope"]           = new("Scope", "scope", typeof(string), "Fully qualified scope to delete the role assignment from. Mutually exclusive with --resource-group. Defaults to the subscription scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "Delete the role assignment scoped to this resource group under the subscription. Mutually exclusive with --scope.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The role assignment name (a GUID) to delete.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
                ["n"] = "name",
            }
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
                ["query-string"]     = new("QueryString", "query-string", typeof(string), "The query string (such as a SAS token) to use with the template URI. Only valid together with a template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersUri", "parameters-uri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", "parameters-file", typeof(string), "The path to a local parameters file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["mode"]             = new("Mode", "mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["parameter"]        = new("Parameter", "parameter", typeof(string[]), "Deployment parameter(s) (repeatable): KEY=VALUE, a raw JSON object, or a path to a JSON parameters file (optionally prefixed with '@'). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
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
                ["query-string"]     = new("QueryString", "query-string", typeof(string), "The query string (such as a SAS token) to use with the template URI. Only valid together with a template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersUri", "parameters-uri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", "parameters-file", typeof(string), "The path to a local parameters file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["mode"]             = new("Mode", "mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["parameter"]        = new("Parameter", "parameter", typeof(string[]), "Deployment parameter(s) (repeatable): KEY=VALUE, a raw JSON object, or a path to a JSON parameters file (optionally prefixed with '@'). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "The validation level of the what-if operation.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["result-format"]    = new("ResultFormat", "result-format", typeof(string), "The format of the what-if results. FullResourcePayloads (default) or ResourceIdOnly.", IsRequired: false, Aliases: [], AllowedValues: ["FullResourcePayloads", "ResourceIdOnly"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Deployment operation commands (PS) ──
        // Each cmdlet maps one entry per parameter set; the cmdlet appends "_{ParameterSetName}"
        // to its own name, so Get/List dispatch without a separate orchestrator per set.
        // PS entries route to the PowerShell-only "_PSOutput" processors, which output PSDeploymentOperation;
        // the CLI entries below keep the full DeploymentOperation.
        private static CommandMetadata DeploymentPS(string invocationName, string processorName, Type processorType, Dictionary<string, ParameterMetadata> parameters, Dictionary<string, string>? aliases = null) => new(
            Name: processorName,
            InvocationName: invocationName,
            Aliases: [],
            FullName: $"Microsoft.Azure.Azclips.Extensions.Resources.Processors.{processorName}",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: processorType,
            Parameters: parameters,
            ParameterAliases: aliases ?? new Dictionary<string, string>()
        );

        private static ParameterMetadata PsSubscriptionId() => new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []);
        private static ParameterMetadata PsResourceGroupName() => new("ResourceGroupName", "ResourceGroupName", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []);
        private static ParameterMetadata PsManagementGroupId() => new("ManagementGroupId", "ManagementGroupId", typeof(string), "The management group id.", IsRequired: true, Aliases: [], AllowedValues: []);
        private static ParameterMetadata PsDeploymentName() => new("DeploymentName", "DeploymentName", typeof(string), "The deployment name.", IsRequired: true, Aliases: ["Name"], AllowedValues: []);
        private static ParameterMetadata PsOperationIds() => new("OperationIds", "OperationIds", typeof(List<string>), "One or more deployment operation Ids to get.", IsRequired: true, Aliases: ["OperationId"], AllowedValues: []);
        private static Dictionary<string, string> PsDeploymentNameAliases() => new() { ["Name"] = "DeploymentName" };
        private static Dictionary<string, string> PsOperationAliases() => new() { ["Name"] = "DeploymentName", ["OperationId"] = "OperationIds" };

        private static readonly CommandMetadata s_getResourceGroupDeploymentOperation_PS = DeploymentPS("Get-AzResourceGroupDeploymentOperation_Get", "GetResourceGroupDeploymentOperationProcessor_PSOutput", typeof(Processors.GetResourceGroupDeploymentOperationProcessor_PSOutput),
            new() { ["SubscriptionId"] = PsSubscriptionId(), ["ResourceGroupName"] = PsResourceGroupName(), ["DeploymentName"] = PsDeploymentName(), ["OperationIds"] = PsOperationIds() }, PsOperationAliases());
        private static readonly CommandMetadata s_listResourceGroupDeploymentOperation_PS = DeploymentPS("Get-AzResourceGroupDeploymentOperation_List", "ListResourceGroupDeploymentOperationProcessor_PSOutput", typeof(Processors.ListResourceGroupDeploymentOperationProcessor_PSOutput),
            new() { ["SubscriptionId"] = PsSubscriptionId(), ["ResourceGroupName"] = PsResourceGroupName(), ["DeploymentName"] = PsDeploymentName() }, PsDeploymentNameAliases());

        private static readonly CommandMetadata s_getSubscriptionDeploymentOperation_PS = DeploymentPS("Get-AzDeploymentOperation_Get", "GetSubscriptionDeploymentOperationProcessor_PSOutput", typeof(Processors.GetSubscriptionDeploymentOperationProcessor_PSOutput),
            new() { ["SubscriptionId"] = PsSubscriptionId(), ["DeploymentName"] = PsDeploymentName(), ["OperationIds"] = PsOperationIds() }, PsOperationAliases());
        private static readonly CommandMetadata s_listSubscriptionDeploymentOperation_PS = DeploymentPS("Get-AzDeploymentOperation_List", "ListSubscriptionDeploymentOperationProcessor_PSOutput", typeof(Processors.ListSubscriptionDeploymentOperationProcessor_PSOutput),
            new() { ["SubscriptionId"] = PsSubscriptionId(), ["DeploymentName"] = PsDeploymentName() }, PsDeploymentNameAliases());

        private static readonly CommandMetadata s_getManagementGroupDeploymentOperation_PS = DeploymentPS("Get-AzManagementGroupDeploymentOperation_Get", "GetManagementGroupDeploymentOperationProcessor_PSOutput", typeof(Processors.GetManagementGroupDeploymentOperationProcessor_PSOutput),
            new() { ["ManagementGroupId"] = PsManagementGroupId(), ["DeploymentName"] = PsDeploymentName(), ["OperationIds"] = PsOperationIds() }, PsOperationAliases());
        private static readonly CommandMetadata s_listManagementGroupDeploymentOperation_PS = DeploymentPS("Get-AzManagementGroupDeploymentOperation_List", "ListManagementGroupDeploymentOperationProcessor_PSOutput", typeof(Processors.ListManagementGroupDeploymentOperationProcessor_PSOutput),
            new() { ["ManagementGroupId"] = PsManagementGroupId(), ["DeploymentName"] = PsDeploymentName() }, PsDeploymentNameAliases());

        private static readonly CommandMetadata s_getTenantDeploymentOperation_PS = DeploymentPS("Get-AzTenantDeploymentOperation_Get", "GetTenantDeploymentOperationProcessor_PSOutput", typeof(Processors.GetTenantDeploymentOperationProcessor_PSOutput),
            new() { ["DeploymentName"] = PsDeploymentName(), ["OperationIds"] = PsOperationIds() }, PsOperationAliases());
        private static readonly CommandMetadata s_listTenantDeploymentOperation_PS = DeploymentPS("Get-AzTenantDeploymentOperation_List", "ListTenantDeploymentOperationProcessor_PSOutput", typeof(Processors.ListTenantDeploymentOperationProcessor_PSOutput),
            new() { ["DeploymentName"] = PsDeploymentName() }, PsDeploymentNameAliases());

        // The DeploymentObject parameter set lists the operations of a piped deployment. The cmdlet resolves the
        // deployment name (and the management group id) from the object; the subscription comes from the active profile.
        private static readonly CommandMetadata s_listSubscriptionDeploymentOperationByObject_PS = DeploymentPS("Get-AzDeploymentOperation_DeploymentObject", "ListSubscriptionDeploymentOperationProcessor_PSOutput", typeof(Processors.ListSubscriptionDeploymentOperationProcessor_PSOutput),
            new() { ["DeploymentName"] = PsDeploymentName() }, PsDeploymentNameAliases());
        private static readonly CommandMetadata s_listManagementGroupDeploymentOperationByObject_PS = DeploymentPS("Get-AzManagementGroupDeploymentOperation_DeploymentObject", "ListManagementGroupDeploymentOperationProcessor_PSOutput", typeof(Processors.ListManagementGroupDeploymentOperationProcessor_PSOutput),
            new() { ["ManagementGroupId"] = PsManagementGroupId(), ["DeploymentName"] = PsDeploymentName() }, PsDeploymentNameAliases());
        private static readonly CommandMetadata s_listTenantDeploymentOperationByObject_PS = DeploymentPS("Get-AzTenantDeploymentOperation_DeploymentObject", "ListTenantDeploymentOperationProcessor_PSOutput", typeof(Processors.ListTenantDeploymentOperationProcessor_PSOutput),
            new() { ["DeploymentName"] = PsDeploymentName() }, PsDeploymentNameAliases());

        // ── Deployment operation commands (CLI) ──
        private static CommandMetadata DeploymentCli(string invocationName, string processorName, Type processorType, Dictionary<string, ParameterMetadata> parameters, Dictionary<string, string>? aliases = null) => new(
            Name: processorName,
            InvocationName: invocationName,
            Aliases: [],
            FullName: $"Microsoft.Azure.Azclips.Extensions.Resources.Processors.{processorName}",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: processorType,
            Parameters: parameters,
            ParameterAliases: aliases ?? new Dictionary<string, string>()
        );

        private static ParameterMetadata CliSubscriptionId() => new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []);
        private static ParameterMetadata CliResourceGroup() => new("ResourceGroupName", "resource-group", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []);
        private static ParameterMetadata CliManagementGroupId() => new("ManagementGroupId", "management-group-id", typeof(string), "The management group id.", IsRequired: true, Aliases: ["m"], AllowedValues: []);
        private static ParameterMetadata CliDeploymentName() => new("DeploymentName", "name", typeof(string), "The deployment name.", IsRequired: true, Aliases: ["n"], AllowedValues: []);
        private static ParameterMetadata CliOperationIds() => new("OperationIds", "operation-ids", typeof(List<string>), "A space-separated list of operation ids to show.", IsRequired: true, Aliases: [], AllowedValues: []);
        private static Dictionary<string, string> CliAliases(params string[] pairs)
        {
            var map = new Dictionary<string, string>();
            for (int i = 0; i < pairs.Length; i += 2) { map[pairs[i]] = pairs[i + 1]; }
            return map;
        }

        private static readonly CommandMetadata s_getResourceGroupDeploymentOperation_CLI = DeploymentCli("deployment operation group show", "GetResourceGroupDeploymentOperationProcessor_Orchestrator", typeof(Processors.GetResourceGroupDeploymentOperationProcessor_Orchestrator),
            new() { ["subscription-id"] = CliSubscriptionId(), ["resource-group"] = CliResourceGroup(), ["name"] = CliDeploymentName(), ["operation-ids"] = CliOperationIds() }, CliAliases("g", "resource-group", "n", "name"));
        private static readonly CommandMetadata s_listResourceGroupDeploymentOperation_CLI = DeploymentCli("deployment operation group list", "ListResourceGroupDeploymentOperationProcessor", typeof(Processors.ListResourceGroupDeploymentOperationProcessor),
            new() { ["subscription-id"] = CliSubscriptionId(), ["resource-group"] = CliResourceGroup(), ["name"] = CliDeploymentName() }, CliAliases("g", "resource-group", "n", "name"));

        private static readonly CommandMetadata s_getSubscriptionDeploymentOperation_CLI = DeploymentCli("deployment operation sub show", "GetSubscriptionDeploymentOperationProcessor_Orchestrator", typeof(Processors.GetSubscriptionDeploymentOperationProcessor_Orchestrator),
            new() { ["subscription-id"] = CliSubscriptionId(), ["name"] = CliDeploymentName(), ["operation-ids"] = CliOperationIds() }, CliAliases("n", "name"));
        private static readonly CommandMetadata s_listSubscriptionDeploymentOperation_CLI = DeploymentCli("deployment operation sub list", "ListSubscriptionDeploymentOperationProcessor", typeof(Processors.ListSubscriptionDeploymentOperationProcessor),
            new() { ["subscription-id"] = CliSubscriptionId(), ["name"] = CliDeploymentName() }, CliAliases("n", "name"));

        private static readonly CommandMetadata s_getManagementGroupDeploymentOperation_CLI = DeploymentCli("deployment operation mg show", "GetManagementGroupDeploymentOperationProcessor_Orchestrator", typeof(Processors.GetManagementGroupDeploymentOperationProcessor_Orchestrator),
            new() { ["management-group-id"] = CliManagementGroupId(), ["name"] = CliDeploymentName(), ["operation-ids"] = CliOperationIds() }, CliAliases("m", "management-group-id", "n", "name"));
        private static readonly CommandMetadata s_listManagementGroupDeploymentOperation_CLI = DeploymentCli("deployment operation mg list", "ListManagementGroupDeploymentOperationProcessor", typeof(Processors.ListManagementGroupDeploymentOperationProcessor),
            new() { ["management-group-id"] = CliManagementGroupId(), ["name"] = CliDeploymentName() }, CliAliases("m", "management-group-id", "n", "name"));

        private static readonly CommandMetadata s_getTenantDeploymentOperation_CLI = DeploymentCli("deployment operation tenant show", "GetTenantDeploymentOperationProcessor_Orchestrator", typeof(Processors.GetTenantDeploymentOperationProcessor_Orchestrator),
            new() { ["name"] = CliDeploymentName(), ["operation-ids"] = CliOperationIds() }, CliAliases("n", "name"));
        private static readonly CommandMetadata s_listTenantDeploymentOperation_CLI = DeploymentCli("deployment operation tenant list", "ListTenantDeploymentOperationProcessor", typeof(Processors.ListTenantDeploymentOperationProcessor),
            new() { ["name"] = CliDeploymentName() }, CliAliases("n", "name"));

        // ── Management-group deployments (az deployment mg * / *-AzManagementGroupDeployment) ──
        private static CommandMetadata ManagementGroupCommand(string invocationName, Type processorType, Dictionary<string, ParameterMetadata> parameters, Dictionary<string, string>? aliases = null) => new(
            Name: processorType.Name,
            InvocationName: invocationName,
            Aliases: [],
            FullName: processorType.FullName!,
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: processorType,
            Parameters: parameters,
            ParameterAliases: aliases ?? new Dictionary<string, string>()
        );

        // New-/Test-AzManagementGroupDeployment: the shared deployment inputs plus ManagementGroupId and Location.
        private static Dictionary<string, ParameterMetadata> ManagementGroupDeploymentParameters_PS() => new()
        {
            ["ManagementGroupId"] = new("ManagementGroupId", "ManagementGroupId", typeof(string), "The management group id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Name"]              = new("Name", "Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Location"]          = new("Location", "Location", typeof(string), "The location to store the deployment data.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["TemplateUri"]       = new("TemplateUri", "TemplateUri", typeof(string), "The URI of a remote ARM JSON template. Supply one template source unless ParametersFile is a .bicepparam file that supplies its own template.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["TemplateFile"]      = new("TemplateFile", "TemplateFile", typeof(string), "The path to a local ARM JSON or Bicep template. Bicep files require the Bicep CLI for local compilation.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["TemplateSpecId"]    = new("TemplateSpecId", "TemplateSpecId", typeof(string), "The resource id of a Template Spec version. Use instead of another template source.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["QueryString"]       = new("QueryString", "QueryString", typeof(string), "A query string or SAS token for TemplateUri only. Requires TemplateUri; does not modify ParametersUri. A leading '?' or '&' is removed.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["ParametersUri"]     = new("ParametersUri", "ParametersUri", typeof(string), "The URI of a remote ARM JSON parameters file, including any required SAS token. Cannot be combined with ParametersFile or Parameter.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["ParametersFile"]    = new("ParametersFile", "ParametersFile", typeof(string), "The path to a local ARM JSON or .bicepparam parameters file. A .bicepparam file can supply its own template and requires the Bicep CLI. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["Mode"]              = new("Mode", "Mode", typeof(string), "The mode sent to ARM for the actual deployment: Incremental (default) or Complete. Validation before deployment always uses Incremental.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
            ["Parameter"]         = new("Parameter", "Parameter", typeof(string[]), "A string array of template parameters: KEY=VALUE, a raw JSON object, or a JSON parameters file path (optionally prefixed with '@'). Specify Parameter once with all values. Values are parsed as JSON when possible, otherwise as strings. Later values override earlier values and same-named keys from ParametersFile. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["ValidationLevel"]   = new("ValidationLevel", "ValidationLevel", typeof(string), "The validation level sent to ARM. When omitted, ARM applies its default.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
        };

        // deployment mg create/validate/what-if: the shared deployment inputs plus management-group-id and location.
        private static Dictionary<string, ParameterMetadata> ManagementGroupDeploymentParameters_CLI() => new()
        {
            ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["name"]                = new("Name", "name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["location"]            = new("Location", "location", typeof(string), "The location to store the deployment data.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["template-uri"]        = new("TemplateUri", "template-uri", typeof(string), "The URI of a remote ARM JSON template. Supply one template source unless --parameters-file is a .bicepparam file that supplies its own template.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["template-file"]       = new("TemplateFile", "template-file", typeof(string), "The path to a local ARM JSON or Bicep template. Bicep files require the Bicep CLI for local compilation.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["template-spec-id"]    = new("TemplateSpecId", "template-spec-id", typeof(string), "The resource id of a Template Spec version. Use instead of another template source.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["query-string"]        = new("QueryString", "query-string", typeof(string), "A query string or SAS token for --template-uri only. Requires --template-uri; does not modify --parameters-uri. A leading '?' or '&' is removed.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["parameters-uri"]      = new("ParametersUri", "parameters-uri", typeof(string), "The URI of a remote ARM JSON parameters file, including any required SAS token. Cannot be combined with --parameters-file or --parameter.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["parameters-file"]     = new("ParametersFile", "parameters-file", typeof(string), "The path to a local ARM JSON or .bicepparam parameters file. A .bicepparam file can supply its own template and requires the Bicep CLI. Cannot be combined with --parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["mode"]                = new("Mode", "mode", typeof(string), "The mode sent to ARM for the actual deployment: Incremental (default) or Complete. Validation before deployment always uses Incremental.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
            ["parameter"]           = new("Parameter", "parameter", typeof(string[]), "Deployment parameter(s) (repeatable): KEY=VALUE, a raw JSON object, or a path to a JSON parameters file (optionally prefixed with '@'). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["validation-level"]    = new("ValidationLevel", "validation-level", typeof(string), "The validation level sent to ARM. When omitted, ARM applies its default.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
        };

        private static Dictionary<string, ParameterMetadata> ManagementGroupDeploymentName_PS() => new()
        {
            ["ManagementGroupId"] = new("GroupId", "ManagementGroupId", typeof(string), "The management group id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Name"] = new("DeploymentName", "Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
        };

        // Stop/Remove: either -Id or -ManagementGroupId and -Name.
        private static Dictionary<string, ParameterMetadata> ManagementGroupDeploymentNameOrId_PS() => new()
        {
            ["ManagementGroupId"] = new("GroupId", "ManagementGroupId", typeof(string), "The management group id. Use with Name when Id is not supplied.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["Name"] = new("DeploymentName", "Name", typeof(string), "The deployment name. Use with ManagementGroupId when Id is not supplied.", IsRequired: false, Aliases: [], AllowedValues: []),
            ["Id"] = new("Id", "Id", typeof(string), "The full management-group deployment resource id. Supplies both the management group id and deployment name.", IsRequired: false, Aliases: [], AllowedValues: []),
        };

        private static Dictionary<string, ParameterMetadata> ManagementGroupDeploymentName_CLI() => new()
        {
            ["management-group-id"] = new("GroupId", "management-group-id", typeof(string), "The management group id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["name"] = new("DeploymentName", "name", typeof(string), "The deployment name.", IsRequired: true, Aliases: [], AllowedValues: []),
        };

        private static readonly CommandMetadata s_newManagementGroupDeployment_PS = ManagementGroupCommand("New-AzManagementGroupDeployment", typeof(Processors.CreateManagementGroupDeploymentProcessor_Orchestrator), ManagementGroupDeploymentParameters_PS());
        // Like Test-AzResourceGroupDeployment: Name is optional (defaults from the template), TemplateObject is accepted, and the legacy parameter file names are aliases.
        private static readonly CommandMetadata s_testManagementGroupDeployment_PS = ManagementGroupCommand("Test-AzManagementGroupDeployment", typeof(Processors.ValidateManagementGroupDeploymentProcessor_Orchestrator),
            new(ManagementGroupDeploymentParameters_PS())
            {
                ["Name"]           = new("Name", "Name", typeof(string), "The name of the deployment. Defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Mode"]           = new("Mode", "Mode", typeof(string), "Accepted for compatibility with New. Does not change validation mode: validation always uses Incremental, even when Complete is supplied.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["TemplateObject"] = new("TemplateObject", "TemplateObject", typeof(Hashtable), "A nonempty hash table holding the ARM template to validate. Use instead of another template source.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]  = new("ParametersUri", "ParametersUri", typeof(string), "The URI of a remote ARM JSON parameters file, including any required SAS token. Cannot be combined with ParametersFile or Parameter.", IsRequired: false, Aliases: ["TemplateParameterUri"], AllowedValues: []),
                ["ParametersFile"] = new("ParametersFile", "ParametersFile", typeof(string), "The path to a local ARM JSON or .bicepparam parameters file. A .bicepparam file can supply its own template and requires the Bicep CLI. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: ["TemplateParameterFile"], AllowedValues: []),
            },
            new() { ["TemplateParameterUri"] = "ParametersUri", ["TemplateParameterFile"] = "ParametersFile" });
        private static readonly CommandMetadata s_getManagementGroupDeployment_PS = ManagementGroupCommand("Get-AzManagementGroupDeployment_Get", typeof(Processors.GetManagementGroupDeploymentProcessor_Orchestrator), ManagementGroupDeploymentName_PS());
        private static readonly CommandMetadata s_getManagementGroupDeploymentById_PS = ManagementGroupCommand("Get-AzManagementGroupDeployment_GetById", typeof(Processors.GetManagementGroupDeploymentProcessor_Orchestrator), new()
        {
            ["Id"] = new("Id", "Id", typeof(string), "The fully qualified resource Id of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
        });
        private static readonly CommandMetadata s_listManagementGroupDeployment_PS = ManagementGroupCommand("Get-AzManagementGroupDeployment_List", typeof(Processors.ListManagementGroupDeploymentProcessor_Orchestrator), new()
        {
            ["ManagementGroupId"] = new("GroupId", "ManagementGroupId", typeof(string), "The management group id.", IsRequired: true, Aliases: [], AllowedValues: []),
        });
        private static readonly CommandMetadata s_stopManagementGroupDeployment_PS = ManagementGroupCommand("Stop-AzManagementGroupDeployment", typeof(Processors.CancelManagementGroupDeploymentProcessor_Orchestrator), ManagementGroupDeploymentNameOrId_PS());
        private static readonly CommandMetadata s_removeManagementGroupDeployment_PS = ManagementGroupCommand("Remove-AzManagementGroupDeployment", typeof(Processors.DeleteManagementGroupDeploymentProcessor_Orchestrator), ManagementGroupDeploymentNameOrId_PS());

        private static readonly CommandMetadata s_createManagementGroupDeployment_CLI = ManagementGroupCommand("deployment mg create", typeof(Processors.CreateManagementGroupDeploymentProcessor_Orchestrator), ManagementGroupDeploymentParameters_CLI());
        private static readonly CommandMetadata s_validateManagementGroupDeployment_CLI = ManagementGroupCommand("deployment mg validate", typeof(Processors.ValidateManagementGroupDeploymentProcessor_Orchestrator),
            new(ManagementGroupDeploymentParameters_CLI())
            {
                ["name"] = new("Name", "name", typeof(string), "The name of the deployment. Defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["mode"] = new("Mode", "mode", typeof(string), "Accepted for compatibility with create. Does not change validation mode: validation always uses Incremental, even when Complete is supplied.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
            });
        private static readonly CommandMetadata s_whatIfManagementGroupDeployment_CLI = ManagementGroupCommand("deployment mg what-if", typeof(Processors.WhatIfManagementGroupDeploymentProcessor_Orchestrator),
            new(ManagementGroupDeploymentParameters_CLI())
            {
                ["mode"] = new("Mode", "mode", typeof(string), "Accepted for compatibility with create. Does not change what-if mode: what-if always uses Incremental, even when Complete is supplied.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["result-format"] = new("ResultFormat", "result-format", typeof(string), "The format of the what-if results. FullResourcePayloads (default) or ResourceIdOnly.", IsRequired: false, Aliases: [], AllowedValues: ["FullResourcePayloads", "ResourceIdOnly"]),
            });
        private static readonly CommandMetadata s_listManagementGroupDeployment_CLI = ManagementGroupCommand("deployment mg list", typeof(Processors.ListManagementGroupDeploymentProcessor_Orchestrator), new()
        {
            ["management-group-id"] = new("GroupId", "management-group-id", typeof(string), "The management group id.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["filter"] = new("_filter", "filter", typeof(string), "An OData filter sent to the management-group deployment list API, for example \"provisioningState eq 'Succeeded'\".", IsRequired: false, Aliases: [], AllowedValues: []),
        });
        private static readonly CommandMetadata s_showManagementGroupDeployment_CLI = ManagementGroupCommand("deployment mg show",         typeof(Processors.GetManagementGroupDeploymentProcessor_Orchestrator), ManagementGroupDeploymentName_CLI());
        private static readonly CommandMetadata s_cancelManagementGroupDeployment_CLI = ManagementGroupCommand("deployment mg cancel", typeof(Processors.CancelManagementGroupDeploymentProcessor_Orchestrator), ManagementGroupDeploymentName_CLI());
        private static readonly CommandMetadata s_deleteManagementGroupDeployment_CLI = ManagementGroupCommand("deployment mg delete", typeof(Processors.DeleteManagementGroupDeploymentProcessor_Orchestrator), ManagementGroupDeploymentName_CLI());
        private static readonly CommandMetadata s_exportManagementGroupDeployment_CLI = ManagementGroupCommand("deployment mg export", typeof(Processors.ExportManagementGroupDeploymentProcessor_Orchestrator), ManagementGroupDeploymentName_CLI());

        private static readonly CommandMetadata s_testResourceGroupDeployment_CLI = new(
            Name: "TestResourceGroupDeploymentProcessor_Orchestrator",
            InvocationName: "deployment group validate",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.TestResourceGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.TestResourceGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]  = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]   = new("ResourceGroupName", "resource-group", typeof(string), "The name of the resource group to validate the deployment against.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]             = new("Name", "name", typeof(string), "The name of the deployment. Defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-uri"]     = new("TemplateUri", "template-uri", typeof(string), "The URI of the ARM template to validate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-file"]    = new("TemplateFile", "template-file", typeof(string), "The path to a local ARM template file to validate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-spec-id"] = new("TemplateSpecId", "template-spec-id", typeof(string), "The resource id of a Template Spec to validate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["query-string"]     = new("QueryString", "query-string", typeof(string), "The query string (such as a SAS token) to use with the template URI. Only valid together with a template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersUri", "parameters-uri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", "parameters-file", typeof(string), "The path to a local parameters file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["mode"]             = new("Mode", "mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["parameter"]        = new("Parameter", "parameter", typeof(string[]), "Deployment parameter(s) (repeatable): KEY=VALUE, a raw JSON object, or a path to a JSON parameters file (optionally prefixed with '@'). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["rollback-on-error"] = new("RollbackOnError", "rollback-on-error", typeof(bool), "Roll the resource group back to the last successful deployment on error.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rollback-to-deployment-name"] = new("RollbackToDeploymentName", "rollback-to-deployment-name", typeof(string), "Roll the resource group back to a specific named deployment on error. Mutually exclusive with rollback-on-error.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getResourceGroupDeployment_CLI = new(
            Name: "GetResourceGroupDeploymentProcessor_Orchestrator",
            InvocationName: "deployment group show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetResourceGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.GetResourceGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The name of the resource group that contains the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listResourceGroupDeployment_CLI = new(
            Name: "ListResourceGroupDeploymentProcessor",
            InvocationName: "deployment group list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListResourceGroupDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListResourceGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The name of the resource group whose deployments to list.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["filter"]          = new("Filter", "filter", typeof(string), "Filter expression using OData notation, e.g. \"provisioningState eq 'Failed'\".", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_stopResourceGroupDeployment_CLI = new(
            Name: "StopResourceGroupDeploymentProcessor",
            InvocationName: "deployment group cancel",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.StopResourceGroupDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.StopResourceGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The name of the resource group that contains the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the deployment to cancel.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_removeResourceGroupDeployment_CLI = new(
            Name: "RemoveResourceGroupDeploymentProcessor_Orchestrator",
            InvocationName: "deployment group delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.RemoveResourceGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.RemoveResourceGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The name of the resource group that contains the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the deployment to delete.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_exportResourceGroupDeploymentTemplate_CLI = new(
            Name: "ExportResourceGroupDeploymentTemplateProcessor",
            InvocationName: "deployment group export",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ExportResourceGroupDeploymentTemplateProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ExportResourceGroupDeploymentTemplateProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The name of the resource group that contains the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the deployment whose template to export.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newSubscriptionDeployment_CLI = new(
            Name: "NewSubscriptionDeploymentProcessor_Orchestrator",
            InvocationName: "deployment sub create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewSubscriptionDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewSubscriptionDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]  = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]             = new("DeploymentName", "name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]         = new("Location", "location", typeof(string), "The location to store the deployment metadata.", IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["mode"]             = new("Mode", "mode", typeof(string), "The mode that is used to deploy resources.", IsRequired: true, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["template-uri"]     = new("TemplateLinkUri", "template-uri", typeof(string), "The URI of the ARM template to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-file"]    = new("TemplateFile", "template-file", typeof(string), "The path to a local ARM JSON or Bicep template. Bicep files require the Bicep CLI for local compilation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-spec-id"] = new("TemplateLinkId", "template-spec-id", typeof(string), "The resource id of a Template Spec to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["query-string"]     = new("TemplateLinkQueryString", "query-string", typeof(string), "The query string (such as a SAS token) to use with the template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersLinkUri", "parameters-uri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", "parameters-file", typeof(string), "The path to a local ARM JSON or .bicepparam parameters file. A .bicepparam file can supply its own template and requires the Bicep CLI. Cannot be combined with --parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameter"]        = new("Parameter", "parameter", typeof(string[]), "Deployment parameter(s) (repeatable): KEY=VALUE, a raw JSON object, or a path to a JSON parameters file (optionally prefixed with '@'). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
            },
            ParameterAliases: new Dictionary<string, string> { ["l"] = "location", ["p"] = "parameter" }
        );
        private static readonly CommandMetadata s_testSubscriptionDeployment_CLI = new(
            Name: "TestSubscriptionDeploymentProcessor_Orchestrator",
            InvocationName: "deployment sub validate",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.TestSubscriptionDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.TestSubscriptionDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]  = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]             = new("DeploymentName", "name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]         = new("Location", "location", typeof(string), "The location to store the deployment metadata.", IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["mode"]             = new("Mode", "mode", typeof(string), "The mode that is used to deploy resources.", IsRequired: true, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["template-uri"]     = new("TemplateLinkUri", "template-uri", typeof(string), "The URI of the ARM template to validate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-file"]    = new("TemplateFile", "template-file", typeof(string), "The path to a local ARM JSON or Bicep template. Bicep files require the Bicep CLI for local compilation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-spec-id"] = new("TemplateLinkId", "template-spec-id", typeof(string), "The resource id of a Template Spec to validate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["query-string"]     = new("TemplateLinkQueryString", "query-string", typeof(string), "The query string (such as a SAS token) to use with the template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersLinkUri", "parameters-uri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", "parameters-file", typeof(string), "The path to a local ARM JSON or .bicepparam parameters file. A .bicepparam file can supply its own template and requires the Bicep CLI. Cannot be combined with --parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameter"]        = new("Parameter", "parameter", typeof(string[]), "Deployment parameter(s) (repeatable): KEY=VALUE, a raw JSON object, or a path to a JSON parameters file (optionally prefixed with '@'). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
            },
            ParameterAliases: new Dictionary<string, string> { ["l"] = "location", ["p"] = "parameter" }
        );
        private static readonly CommandMetadata s_newSubscriptionDeploymentWhatIf_CLI = new(
            Name: "NewSubscriptionDeploymentWhatIfProcessor_Orchestrator",
            InvocationName: "deployment sub what-if",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewSubscriptionDeploymentWhatIfProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewSubscriptionDeploymentWhatIfProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]  = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]             = new("DeploymentName", "name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]         = new("Location", "location", typeof(string), "The location to store the deployment what-if operation metadata.", IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["mode"]             = new("Mode", "mode", typeof(string), "The mode that is used to deploy resources.", IsRequired: true, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["template-uri"]     = new("TemplateLinkUri", "template-uri", typeof(string), "The URI of the ARM template to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-file"]    = new("TemplateFile", "template-file", typeof(string), "The path to a local ARM JSON or Bicep template. Bicep files require the Bicep CLI for local compilation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-spec-id"] = new("TemplateLinkId", "template-spec-id", typeof(string), "The resource id of a Template Spec to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["query-string"]     = new("TemplateLinkQueryString", "query-string", typeof(string), "The query string (such as a SAS token) to use with the template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersLinkUri", "parameters-uri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", "parameters-file", typeof(string), "The path to a local ARM JSON or .bicepparam parameters file. A .bicepparam file can supply its own template and requires the Bicep CLI. Cannot be combined with --parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameter"]        = new("Parameter", "parameter", typeof(string[]), "Deployment parameter(s) (repeatable): KEY=VALUE, a raw JSON object, or a path to a JSON parameters file (optionally prefixed with '@'). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: ["p"], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "The validation level of the what-if operation.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["result-format"]    = new("WhatIfSettingsResultFormat", "result-format", typeof(string), "The format of the what-if results.", IsRequired: false, Aliases: ["r"], AllowedValues: ["FullResourcePayloads", "ResourceIdOnly"]),
            },
            ParameterAliases: new Dictionary<string, string> { ["l"] = "location", ["p"] = "parameter", ["r"] = "result-format" }
        );
        private static readonly CommandMetadata s_getSubscriptionDeployment_CLI = new(
            Name: "GetSubscriptionDeploymentProcessor",
            InvocationName: "deployment sub show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetSubscriptionDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.GetSubscriptionDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listSubscriptionDeployment_CLI = new(
            Name: "ListSubscriptionDeploymentProcessor",
            InvocationName: "deployment sub list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListSubscriptionDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListSubscriptionDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["filter"]          = new("Filter", "filter", typeof(string), "Filter expression using OData notation, e.g. \"provisioningState eq 'Failed'\".", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_stopSubscriptionDeployment_CLI = new(
            Name: "StopSubscriptionDeploymentProcessor",
            InvocationName: "deployment sub cancel",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.StopSubscriptionDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.StopSubscriptionDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the deployment to cancel.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_removeSubscriptionDeployment_CLI = new(
            Name: "RemoveSubscriptionDeploymentProcessor",
            InvocationName: "deployment sub delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.RemoveSubscriptionDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.RemoveSubscriptionDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the deployment to delete.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_exportSubscriptionDeploymentTemplate_CLI = new(
            Name: "ExportSubscriptionDeploymentProcessor_Orchestrator",
            InvocationName: "deployment sub export",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ExportSubscriptionDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ExportSubscriptionDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("DeploymentName", "name", typeof(string), "The name of the deployment whose template to export.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_saveResourceGroupDeploymentTemplate_PS = new(
            Name: "SaveResourceGroupDeploymentTemplateProcessor_Orchestrator",
            InvocationName: "Save-AzResourceGroupDeploymentTemplate",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.SaveResourceGroupDeploymentTemplateProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.SaveResourceGroupDeploymentTemplateProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The name of the resource group that contains the deployment.", IsRequired: true, Aliases: ["ResourceGroup"], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the deployment whose template to save.", IsRequired: true, Aliases: ["DeploymentName"], AllowedValues: []),
                ["Path"]              = new("Path", "Path", typeof(string), "The output path of the template file. A directory receives '<deployment name>.json'; any other path is used as the file name and gains a '.json' extension when it lacks one. Defaults to the current directory.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Force"]             = new("Force", "Force", typeof(bool), "Overwrite the destination file when it already exists.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceGroup"] = "ResourceGroupName",
                ["DeploymentName"] = "Name",
            }
        );
        private static readonly CommandMetadata s_saveSubscriptionDeploymentTemplate_PS = new(
            Name: "SaveSubscriptionDeploymentTemplateProcessor_Orchestrator",
            InvocationName: "Save-AzDeploymentTemplate",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.SaveSubscriptionDeploymentTemplateProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.SaveSubscriptionDeploymentTemplateProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]           = new("DeploymentName", "Name", typeof(string), "The name of the deployment whose template to save.", IsRequired: true, Aliases: ["DeploymentName"], AllowedValues: []),
                ["Path"]           = new("Path", "Path", typeof(string), "The output path of the template file. A directory receives '<deployment name>.json'; any other path is used as the file name and gains a '.json' extension when it lacks one. Defaults to the current directory.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Force"]          = new("Force", "Force", typeof(bool), "Overwrite the destination file when it already exists.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["DeploymentName"] = "Name",
            }
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["New-AzResourceGroup"]      = s_newResourceGroup_PS,
            ["Set-AzResourceGroup"]      = s_setResourceGroup_PS,
            ["Remove-AzResourceGroup"]   = s_removeResourceGroup_PS,
            ["Get-AzResourceGroup_Get"]  = s_getResourceGroup_PS,
            ["Get-AzResourceGroup_List"] = s_listResourceGroup_PS,
            ["Get-AzRoleAssignment"]     = s_listRoleAssignment_PS,
            ["New-AzRoleAssignment"]     = s_newRoleAssignment_PS,
            ["Remove-AzRoleAssignment"]  = s_deleteRoleAssignment_PS,
            ["New-AzResourceGroupDeployment"] = s_newResourceGroupDeployment_PS,
            ["New-AzResourceGroupDeploymentWhatIf"] = s_newResourceGroupDeploymentWhatIf_PS,
            ["New-AzManagementGroupDeployment"] = s_newManagementGroupDeployment_PS,
            ["Test-AzManagementGroupDeployment"] = s_testManagementGroupDeployment_PS,
            ["Get-AzManagementGroupDeployment_Get"] = s_getManagementGroupDeployment_PS,
            ["Get-AzManagementGroupDeployment_GetById"] = s_getManagementGroupDeploymentById_PS,
            ["Get-AzManagementGroupDeployment_List"] = s_listManagementGroupDeployment_PS,
            ["Stop-AzManagementGroupDeployment"] = s_stopManagementGroupDeployment_PS,
            ["Remove-AzManagementGroupDeployment"] = s_removeManagementGroupDeployment_PS,
            ["Test-AzResourceGroupDeployment"] = s_testResourceGroupDeployment_PS,
            ["Get-AzResourceGroupDeployment_Get"]  = s_getResourceGroupDeployment_PS,
            ["Get-AzResourceGroupDeployment_GetById"] = s_getResourceGroupDeploymentById_PS,
            ["Get-AzResourceGroupDeployment_List"] = s_listResourceGroupDeployment_PS,
            ["Stop-AzResourceGroupDeployment"] = s_stopResourceGroupDeployment_PS,
            ["Remove-AzResourceGroupDeployment"] = s_removeResourceGroupDeployment_PS,
            ["Save-AzResourceGroupDeploymentTemplate"] = s_saveResourceGroupDeploymentTemplate_PS,
            ["New-AzDeployment"]         = s_newSubscriptionDeployment_PS,
            ["Test-AzDeployment"]        = s_testSubscriptionDeployment_PS,
            ["Get-AzDeployment_Get"]     = s_getSubscriptionDeployment_PS,
            ["Get-AzDeployment_List"]    = s_listSubscriptionDeployment_PS,
            ["Stop-AzDeployment"]        = s_stopSubscriptionDeployment_PS,
            ["Remove-AzDeployment"]      = s_removeSubscriptionDeployment_PS,
            ["Save-AzDeploymentTemplate"] = s_saveSubscriptionDeploymentTemplate_PS,
            ["Invoke-AzRestMethod"] = s_rest_PS,
            ["Get-AzResourceGroupDeploymentOperation_Get"]  = s_getResourceGroupDeploymentOperation_PS,
            ["Get-AzResourceGroupDeploymentOperation_List"] = s_listResourceGroupDeploymentOperation_PS,
            ["Get-AzDeploymentOperation_Get"]  = s_getSubscriptionDeploymentOperation_PS,
            ["Get-AzDeploymentOperation_List"] = s_listSubscriptionDeploymentOperation_PS,
            ["Get-AzDeploymentOperation_DeploymentObject"] = s_listSubscriptionDeploymentOperationByObject_PS,
            ["Get-AzManagementGroupDeploymentOperation_Get"]  = s_getManagementGroupDeploymentOperation_PS,
            ["Get-AzManagementGroupDeploymentOperation_List"] = s_listManagementGroupDeploymentOperation_PS,
            ["Get-AzManagementGroupDeploymentOperation_DeploymentObject"] = s_listManagementGroupDeploymentOperationByObject_PS,
            ["Get-AzTenantDeploymentOperation_Get"]  = s_getTenantDeploymentOperation_PS,
            ["Get-AzTenantDeploymentOperation_List"] = s_listTenantDeploymentOperation_PS,
            ["Get-AzTenantDeploymentOperation_DeploymentObject"] = s_listTenantDeploymentOperationByObject_PS,
        };
        // Full-path keys. Each key is `{verb} {path}` where `verb` matches
        // an [ExtensionVerb] declaration on this class; the dispatcher does longest-prefix match on the
        // (verb, path) tuple via Core.Extensions.LeafIndex. Resources contributes both the
        // "group" verb (resource group CRUD) and the "deployment" verb (resource-group- and
        // subscription-scoped ARM/Bicep deployments).
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["group create"]              = s_newResourceGroup_CLI,
            ["group delete"]              = s_deleteResourceGroup_CLI,
            ["group exists"]              = s_existsResourceGroup_CLI,
            ["group list"]                = s_listResourceGroup_CLI,
            ["group show"]                = s_getResourceGroup_CLI,
            ["group update"]              = s_updateResourceGroup_CLI,
            ["role assignment list"]      = s_listRoleAssignment_CLI,
            ["role assignment create"]    = s_newRoleAssignment_CLI,
            ["role assignment delete"]    = s_deleteRoleAssignment_CLI,
            ["deployment group create"]   = s_newResourceGroupDeployment_CLI,
            ["deployment group what-if"]  = s_newResourceGroupDeploymentWhatIf_CLI,
            ["deployment operation group show"]  = s_getResourceGroupDeploymentOperation_CLI,
            ["deployment operation group list"]  = s_listResourceGroupDeploymentOperation_CLI,
            ["deployment operation sub show"]    = s_getSubscriptionDeploymentOperation_CLI,
            ["deployment operation sub list"]    = s_listSubscriptionDeploymentOperation_CLI,
            ["deployment operation mg show"]     = s_getManagementGroupDeploymentOperation_CLI,
            ["deployment operation mg list"]     = s_listManagementGroupDeploymentOperation_CLI,
            ["deployment operation tenant show"] = s_getTenantDeploymentOperation_CLI,
            ["deployment operation tenant list"] = s_listTenantDeploymentOperation_CLI,
            ["deployment mg create"]      = s_createManagementGroupDeployment_CLI,
            ["deployment mg list"]        = s_listManagementGroupDeployment_CLI,
            ["deployment mg show"]        = s_showManagementGroupDeployment_CLI,
            ["deployment mg cancel"]      = s_cancelManagementGroupDeployment_CLI,
            ["deployment mg delete"]      = s_deleteManagementGroupDeployment_CLI,
            ["deployment mg export"]      = s_exportManagementGroupDeployment_CLI,
            ["deployment mg validate"]    = s_validateManagementGroupDeployment_CLI,
            ["deployment mg what-if"]     = s_whatIfManagementGroupDeployment_CLI,
            ["deployment group validate"] = s_testResourceGroupDeployment_CLI,
            ["deployment group show"]     = s_getResourceGroupDeployment_CLI,
            ["deployment group list"]     = s_listResourceGroupDeployment_CLI,
            ["deployment group cancel"]   = s_stopResourceGroupDeployment_CLI,
            ["deployment group delete"]   = s_removeResourceGroupDeployment_CLI,
            ["deployment group export"]   = s_exportResourceGroupDeploymentTemplate_CLI,
            ["deployment sub create"]     = s_newSubscriptionDeployment_CLI,
            ["deployment sub what-if"]    = s_newSubscriptionDeploymentWhatIf_CLI,
            ["deployment sub validate"]   = s_testSubscriptionDeployment_CLI,
            ["deployment sub show"]       = s_getSubscriptionDeployment_CLI,
            ["deployment sub list"]       = s_listSubscriptionDeployment_CLI,
            ["deployment sub cancel"]     = s_stopSubscriptionDeployment_CLI,
            ["deployment sub delete"]     = s_removeSubscriptionDeployment_CLI,
            ["deployment sub export"]     = s_exportSubscriptionDeploymentTemplate_CLI,
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
