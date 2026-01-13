using System;
using System.Collections.Generic;
using System.Text;
using SharedContracts;
using Core.Abstractions;
using Core.Implementation;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine
{
    public static class Engine
    {
        private static ConcurrentDictionary<string, IExtensionIndex> _extensionIndices = new ConcurrentDictionary<string, IExtensionIndex>();

        public static IProcessor GetCommand(ICommandInvocation invocation)
        {
            var telemetry = TryGetTelemetryService();
            using var activity = telemetry?.StartActivity("Engine.GetCommand", ActivityKind.Internal);

            IExtensionIndex extensionIndex = _extensionIndices.GetOrAdd(invocation.ExtensionName, (extensionName) =>
            {
                using var loadActivity = telemetry?.StartActivity("Engine.LoadExtension", ActivityKind.Internal);
                loadActivity?.SetTag("extension", extensionName);
                
                string assemblyName = invocation.ExtensionName.ResolveExtensionName().ResolveAssemblyName();
                Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult()
                    .GetRequiredService<ILoggingService>()
                    .Verbose($"Resolved extension '{extensionName}' to assembly '{assemblyName}'. Loading extension index...");
                Type? IndexType = Type.GetType($"{assemblyName}.ExtensionIndex, {assemblyName}") ?? throw new InvalidOperationException($"No IExtensionIndex implementation found in assembly {assemblyName}");
                return (Activator.CreateInstance(IndexType) as IExtensionIndex)!;
            });

            ICommandMetadata commandMetadata;
            using (var metaActivity = telemetry?.StartActivity("Engine.GetMetadata", ActivityKind.Internal))
            {
                commandMetadata = extensionIndex.GetCommandMetaData(invocation.CommandName, invocation.ClientType);
            }

            IProcessor instance;
            using (var instActivity = telemetry?.StartActivity("Engine.CreateInstance", ActivityKind.Internal))
            {
                instance = commandMetadata.GetInstance();
            }

            using (var bindActivity = telemetry?.StartActivity("Engine.BindParameters", ActivityKind.Internal))
            {
                invocation.Parameters.ToList().ForEach(p => instance.SetProperty(p.Key, p.Value));
            }
            
            return instance;
        }

        public static object ProcessCommand(IProcessor processor)
        {
            var telemetry = TryGetTelemetryService();
            using var activity = telemetry?.StartActivity("Engine.ProcessCommand", ActivityKind.Internal);
            return processor.Process();
        }

        public static string ProcessCommand(IProcessor processor, bool serialize)
        {
            var telemetry = TryGetTelemetryService();
            using var activity = telemetry?.StartActivity("Engine.ProcessCommand", ActivityKind.Internal);
            activity?.SetTag("serialize", serialize);
            return processor.Process(serialize);
        }

        private static ITelemetryService? TryGetTelemetryService()
        {
            try
            {
                 // Check if Bootstrapper Task is completed to avoid deadlocks or blocking if not started
                 if (Bootstrapper.ServiceProviderAsync.IsCompleted)
                 {
                     return Bootstrapper.ServiceProviderAsync.Result.GetService<ITelemetryService>();
                 }
                 return null;
            }
            catch
            {
                return null;
            }
        }

        private static string ResolveAssemblyName (this string extensionName) => $"Microsoft.Azure.Cirrus.Extensions.{extensionName}";
        private static string ResolveExtensionName(this string extensionName)
        {
            //TODO: implement extension name resolution logic, this will map extension/module name passed by user to actual extension name in dll
            return extensionName;
        }
    }
}
