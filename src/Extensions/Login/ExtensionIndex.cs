// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Abstractions;

namespace Microsoft.Azure.Cirrus.Extensions.Resources
{
    public class ExtensionIndex: IExtensionIndex
    {
        private readonly IDictionary<string, ICommandMetadata> _psTable = new Dictionary<string, ICommandMetadata>();
        private readonly IDictionary<string, ICommandMetadata> _cliTable = new Dictionary<string, ICommandMetadata>();

        public ExtensionIndex()
        {
            _psTable.Add("Connect-Cirrus", new CommandMetadata(
                name: "NewResourceGroupCommand",
                fullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Commands.NewResourceGroupCommand",
                assemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
                commandType: typeof(Commands.NewResourceGroupCommand)
            ));
            _cliTable.Add("login", new CommandMetadata(
                name: "NewResourceGroupCommand",
                fullName: "Microsoft.Azure.Cirrus.Extensions.Resources.Commands.NewResourceGroupCommand",
                assemblyName: "Microsoft.Azure.Cirrus.Extensions.Resources",
                commandType: typeof(Commands.NewResourceGroupCommand)
            ));
        }

        public ICommandMetadata GetCommandMetaData(string name, ClientType clientType)
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
    }

    class CommandMetadata : ICommandMetadata
    {
        public string Name { get; private set; }
        public string FullName { get; private set; }
        public string AssemblyName { get; private set; }
        public Type CommandType { get; private set; }
        public CommandMetadata(string name, string fullName, string assemblyName, Type commandType)
        {
            Name = name;
            FullName = fullName;
            AssemblyName = assemblyName;
            CommandType = commandType;
        }
    }
}
