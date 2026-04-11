// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Resources
{
    public class ExtensionIndex: IExtensionIndex
    {
        private readonly IDictionary<string, CommandMetadata> _psTable = new Dictionary<string, CommandMetadata>();
        private readonly IDictionary<string, CommandMetadata> _cliTable = new Dictionary<string, CommandMetadata>();

        public ExtensionIndex()
        {
            _psTable.Add("New-ResourceGroup", new CommandMetadata(
                Name: "NewResourceGroupCommand",
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Commands.NewResourceGroupCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
                CommandType: typeof(Commands.NewResourceGroupCommand),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()
            ));
            _psTable.Add("Get-ResourceGroup_Get", new CommandMetadata(
                Name: nameof(Commands.GetResourceGroupCommand),
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Commands.GetResourceGroupCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
                CommandType: typeof(Commands.GetResourceGroupCommand),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()
            ));
            _psTable.Add("Get-ResourceGroup_List", new CommandMetadata(
                Name: nameof(Commands.ListResourceGroupCommand),
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Commands.ListResourceGroupCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
                CommandType: typeof(Commands.ListResourceGroupCommand),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()
            ));
            _cliTable.Add("create", new CommandMetadata(
                Name: "NewResourceGroupCommand",
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Commands.NewResourceGroupCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
                CommandType: typeof(Commands.NewResourceGroupCommand),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()
            ));
            _cliTable.Add("list", new CommandMetadata(
                Name: nameof(Commands.GetResourceGroupCommand),
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Commands.ListResourceGroupCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
                CommandType: typeof(Commands.ListResourceGroupCommand),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()
            ));
            _cliTable.Add("get", new CommandMetadata(
                Name: nameof(Commands.GetResourceGroupCommand),
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Commands.GetResourceGroupCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
                CommandType: typeof(Commands.GetResourceGroupCommand),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()
            ));
        }

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
        {
            switch (clientType)
            {
                case ClientType.PowerShell:
                    if (_psTable.TryGetValue(name, out var psData))
                    {
                        return psData;
                    }
                    break;
                case ClientType.CLI:
                    if (_cliTable.TryGetValue(name, out var cliData))
                    {
                        return cliData;
                    }
                    break;
                default:
                    throw new ArgumentException($"Client type '{clientType}' is not supported.");
            }
            throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
        }

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => _psTable.Values,
                ClientType.CLI => _cliTable.Values,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }
    }
}
