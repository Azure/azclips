using System.Collections.Generic;
using Core.Abstractions;

namespace Core.Implementation
{
    public class CLICommandInvocation : ICommandInvocation
    {
        public string ExtensionName { get; }
        public string CommandName { get; }
        public IDictionary<string, object> Parameters { get; }
        public ClientType ClientType => ClientType.CLI;

        public CLICommandInvocation(string extensionName, string commandName, IDictionary<string, object> parameters)
        {
            ExtensionName = extensionName;
            CommandName = commandName;
            Parameters = parameters;
        }
    }
}
