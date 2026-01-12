using System;
using System.Collections.Generic;
using System.Text;
using SharedContracts;
using Core.Abstractions;
using Core.Implementation;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine
{
    public static class Engine
    {
        private static ConcurrentDictionary<string, IExtensionIndex> _extensionIndices = new ConcurrentDictionary<string, IExtensionIndex>();

        public static IProcessor GetCommand(ICommandInvocation invocation)
        {
            IExtensionIndex extensionIndex = _extensionIndices.GetOrAdd(invocation.ExtensionName, (extensionName) =>
            {
                string assemblyName = invocation.ExtensionName.ResolveExtensionName().ResolveAssemblyName();
                Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult()
                    .GetRequiredService<ILoggingService>()
                    .Verbose($"Resolved extension '{extensionName}' to assembly '{assemblyName}'. Loading extension index...");
                Type? IndexType = Type.GetType($"{assemblyName}.ExtensionIndex, {assemblyName}") ?? throw new InvalidOperationException($"No IExtensionIndex implementation found in assembly {assemblyName}");
                return (Activator.CreateInstance(IndexType) as IExtensionIndex)!;
            });
            ICommandMetadata commandMetadata = extensionIndex.GetCommandMetaData(invocation.CommandName, invocation.ClientType);
            IProcessor instance = commandMetadata.GetInstance();
            invocation.Parameters.ToList().ForEach(p => instance.SetProperty(p.Key, p.Value));
            return instance;
        }

        private static string ResolveAssemblyName (this string extensionName) => $"Microsoft.Azure.Cirrus.Extensions.{extensionName}";
        private static string ResolveExtensionName(this string extensionName)
        {
            //TODO: implement extension name resolution logic, this will map extension/module name passed by user to actual extension name in dll
            return extensionName;
        }
    }
}
