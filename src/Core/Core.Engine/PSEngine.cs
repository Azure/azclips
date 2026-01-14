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
    public class PSEngine : IEngine
    {
        private ConcurrentDictionary<string, IExtensionIndex> _extensionIndices = new ConcurrentDictionary<string, IExtensionIndex>();

        private readonly IServiceProvider _serviceProvider;
        public ClientType ClientType = ClientType.PowerShell;

        public Action<string> Log { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Action<string> Debug { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public PSEngine(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default)
        {
            // TODO: Implement command execution logic
            throw new NotImplementedException();
        }

        public IProcessor GetCommand(ICommandInvocation invocation)
        {
            IExtensionIndex extensionIndex = _extensionIndices.GetOrAdd(invocation.ExtensionName, (extensionName) =>
            {
                string assemblyName = invocation.ExtensionName.ResolveExtensionName().ResolveAssemblyName();
                _serviceProvider
                    .GetRequiredService<ILoggingService>()
                    .Verbose($"Resolved extension '{extensionName}' to assembly '{assemblyName}'. Loading extension index...");
                Type? IndexType = Type.GetType($"{assemblyName}.ExtensionIndex, {assemblyName}") ?? throw new InvalidOperationException($"No IExtensionIndex implementation found in assembly {assemblyName}");
                return (Activator.CreateInstance(IndexType) as IExtensionIndex)!;
            });
            ICommandMetadata commandMetadata = extensionIndex.GetCommandMetaData(invocation.CommandName, ClientType);
            IProcessor instance = commandMetadata.GetInstance();
            invocation.Parameters.ToList().ForEach(p => instance.SetProperty(p.Key, p.Value));
            return instance;
        }
    }
}
