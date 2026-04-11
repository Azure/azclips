// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Login
{
    public class ExtensionIndex: IExtensionIndex
    {
        private readonly IDictionary<string, CommandMetadata> _psTable = new Dictionary<string, CommandMetadata>();
        private readonly IDictionary<string, CommandMetadata> _cliTable = new Dictionary<string, CommandMetadata>();

        public ExtensionIndex()
        {
            _psTable.Add("Connect-CirrusAccount", new CommandMetadata(
                Name: nameof(Commands.LoginCommand),
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.LoginCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
                CommandType: typeof(Commands.LoginCommand),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()
            ));
            _psTable.Add("Disconnect-CirrusAccount", new CommandMetadata(
                Name: nameof(Commands.LogoutCommand),
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.LogoutCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
                CommandType: typeof(Commands.LogoutCommand),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()
            ));
            _psTable.Add("Get-CirrusAccount", new CommandMetadata(
                Name: nameof(Commands.GetContextCommand),
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.GetContextCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
                CommandType: typeof(Commands.GetContextCommand),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()
            ));
            // login is a special command that the command group and command name are the same
            _cliTable.Add("", new CommandMetadata(
                Name: nameof(Commands.LoginCommand),
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.LoginCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
                CommandType: typeof(Commands.LoginCommand),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()
            ));
            _cliTable.Add("logout", new CommandMetadata(
                Name: nameof(Commands.LogoutCommand),
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.LogoutCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
                CommandType: typeof(Commands.LogoutCommand),
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>()
            ));
            _cliTable.Add("show", new CommandMetadata(
                Name: nameof(Commands.GetContextCommand),
                Aliases: [],
                FullName: "Microsoft.Azure.Cirrus.Extensions.Login.Commands.GetContextCommand",
                AssemblyName: "Microsoft.Azure.Cirrus.Extensions.Login",
                CommandType: typeof(Commands.GetContextCommand),
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
