// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Resources
{
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_newResourceGroup_PS = new(
            Name: "NewResourceGroupProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Processors.NewResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]           = new("Name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]       = new("Location", typeof(string), "Location for resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getResourceGroup_PS = new(
            Name: "GetResourceGroupProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Processors.GetResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
            ProcessorType: typeof(Processors.GetResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]           = new("Name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listResourceGroup_PS = new(
            Name: "ListResourceGroupProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Processors.ListResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
            ProcessorType: typeof(Processors.ListResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newResourceGroupDeployment_PS = new(
            Name: "NewResourceGroupDeploymentProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Processors.NewResourceGroupDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", typeof(string), "The name of the resource group to deploy into.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["TemplateUri"]       = new("TemplateUri", typeof(string), "The URI of the ARM template to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateFile"]      = new("TemplateFile", typeof(string), "The path to a local ARM template file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateSpecId"]    = new("TemplateSpecId", typeof(string), "The resource id of a Template Spec to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["QueryString"]       = new("QueryString", typeof(string), "The query string (for example, a SAS token) to be used with the template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]     = new("ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersFile"]    = new("ParametersFile", typeof(string), "The path to a local parameters file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Mode"]              = new("Mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["Parameter"]         = new("Parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RollbackOnError"]   = new("RollbackOnError", typeof(bool), "Roll the resource group back to the last successful deployment on error.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RollbackToLastDeploymentName"] = new("RollbackToLastDeploymentName", typeof(string), "Roll the resource group back to a specific named deployment on error. Mutually exclusive with RollbackOnError.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"]   = new("ValidationLevel", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["NoWait"]            = new("NoWait", typeof(bool), "Return as soon as the deployment is accepted, without waiting for it to complete.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newResourceGroupDeploymentWhatIf_PS = new(
            Name: "NewResourceGroupDeploymentWhatIfProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Processors.NewResourceGroupDeploymentWhatIfProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentWhatIfProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", typeof(string), "The name of the resource group to deploy into.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["TemplateUri"]       = new("TemplateUri", typeof(string), "The URI of the ARM template to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateFile"]      = new("TemplateFile", typeof(string), "The path to a local ARM template file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateSpecId"]    = new("TemplateSpecId", typeof(string), "The resource id of a Template Spec to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["QueryString"]       = new("QueryString", typeof(string), "The query string (for example, a SAS token) to be used with the template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]     = new("ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersFile"]    = new("ParametersFile", typeof(string), "The path to a local parameters file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Mode"]              = new("Mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["Parameter"]         = new("Parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"]   = new("ValidationLevel", typeof(string), "The validation level of the what-if.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["ResultFormat"]      = new("ResultFormat", typeof(string), "The result format of the what-if. ResourceIdOnly or FullResourcePayloads (default).", IsRequired: false, Aliases: [], AllowedValues: ["ResourceIdOnly", "FullResourcePayloads"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_newResourceGroup_CLI = new(
            Name: "NewResourceGroupProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Processors.NewResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]        = new("Location", typeof(string), "Location for resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getResourceGroup_CLI = new(
            Name: "GetResourceGroupProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Processors.GetResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
            ProcessorType: typeof(Processors.GetResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listResourceGroup_CLI = new(
            Name: "ListResourceGroupProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Processors.ListResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
            ProcessorType: typeof(Processors.ListResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newResourceGroupDeployment_CLI = new(
            Name: "NewResourceGroupDeploymentProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Processors.NewResourceGroupDeploymentProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]  = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]   = new("ResourceGroupName", typeof(string), "The name of the resource group to deploy into.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]             = new("Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["template-uri"]     = new("TemplateUri", typeof(string), "The URI of the ARM template to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-file"]    = new("TemplateFile", typeof(string), "The path to a local ARM template file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-spec-id"] = new("TemplateSpecId", typeof(string), "The resource id of a Template Spec to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["query-string"]     = new("QueryString", typeof(string), "The query string (for example, a SAS token) to be used with the template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", typeof(string), "The path to a local parameters file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["mode"]             = new("Mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["parameter"]        = new("Parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rollback-on-error"] = new("RollbackOnError", typeof(bool), "Roll the resource group back to the last successful deployment on error.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rollback-to-last-deployment-name"] = new("RollbackToLastDeploymentName", typeof(string), "Roll the resource group back to a specific named deployment on error. Mutually exclusive with rollback-on-error.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["no-wait"]          = new("NoWait", typeof(bool), "Return as soon as the deployment is accepted, without waiting for it to complete.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newResourceGroupDeploymentWhatIf_CLI = new(
            Name: "NewResourceGroupDeploymentWhatIfProcessor",
            Aliases: [],
            FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Processors.NewResourceGroupDeploymentWhatIfProcessor",
            AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentWhatIfProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]  = new("SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]   = new("ResourceGroupName", typeof(string), "The name of the resource group to deploy into.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]             = new("Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["template-uri"]     = new("TemplateUri", typeof(string), "The URI of the ARM template to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-file"]    = new("TemplateFile", typeof(string), "The path to a local ARM template file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-spec-id"] = new("TemplateSpecId", typeof(string), "The resource id of a Template Spec to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["query-string"]     = new("QueryString", typeof(string), "The query string (for example, a SAS token) to be used with the template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", typeof(string), "The path to a local parameters file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["mode"]             = new("Mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["parameter"]        = new("Parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", typeof(string), "The validation level of the what-if.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["result-format"]    = new("ResultFormat", typeof(string), "The result format of the what-if. ResourceIdOnly or FullResourcePayloads (default).", IsRequired: false, Aliases: [], AllowedValues: ["ResourceIdOnly", "FullResourcePayloads"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["New-ResourceGroup"]      = s_newResourceGroup_PS,
            ["Get-ResourceGroup_Get"]  = s_getResourceGroup_PS,
            ["Get-ResourceGroup_List"] = s_listResourceGroup_PS,
            ["New-ResourceGroupDeployment"] = s_newResourceGroupDeployment_PS,
            ["New-ResourceGroupDeploymentWhatIf"] = s_newResourceGroupDeploymentWhatIf_PS,
        };
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["create"] = s_newResourceGroup_CLI,
            ["list"]   = s_listResourceGroup_CLI,
            ["show"]   = s_getResourceGroup_CLI,
            ["group create"] = s_newResourceGroupDeployment_CLI,
            ["group what-if"] = s_newResourceGroupDeploymentWhatIf_CLI,
        };
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
        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable.Values,
                ClientType.CLI => s_cliTable.Values,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }
    }
}