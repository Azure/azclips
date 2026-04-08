using System.Collections.Generic;
using Core.Abstractions;

namespace Core.Runtime
{
	public class PSCommandInvocation : ICommandInvocation
	{
        public string ExtensionName { get; }
		public string CommandName { get; }
		public IDictionary<string, object> Parameters { get; }
		public ClientType ClientType => ClientType.PowerShell;
		public string? ModuleName { get; set; }
		public string? ModuleVersion { get; set; }

		public PSCommandInvocation(string extensionName, string commandName, IDictionary<string, object> parameters)
		{
			ExtensionName = extensionName;
			CommandName = commandName;
			Parameters = parameters;
		}
	}
}
