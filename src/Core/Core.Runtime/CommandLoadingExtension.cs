// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;

namespace Core.Runtime
{
    public static class CommandLoadingExtension
    {
        public static IExtensionIndex GetExtensionIndex(this Assembly assembly)
        {
            string assemblyName = assembly.GetName().Name ?? throw new ExtensionLoadException($"The assembly name could not be determined for assembly '{assembly.FullName}'.");
            string typeName = $"{assemblyName}.ExtensionIndex";
            Type type = assembly.GetType(typeName) ?? throw new ExtensionLoadException($"No IExtensionIndex implementation found in assembly {assembly.FullName}");
            if (type == null)
            {
                throw new ExtensionLoadException($"The type '{typeName}' could not be found in assembly '{assembly.FullName}'.");
            }
            if (!typeof(IExtensionIndex).IsAssignableFrom(type))
            {
                throw new ExtensionLoadException($"The type '{typeName}' does not implement IExtensionIndex.");
            }
            return (Activator.CreateInstance(type) as IExtensionIndex)!;
        }

        public static IProcessor GetInstance(this CommandMetadata metadata)
        {
            IProcessor? instance = null;
            try
            {
                var processorType = metadata.ProcessorType;
                if (processorType == null && !String.IsNullOrEmpty(metadata.FullName) && !String.IsNullOrEmpty(metadata.AssemblyName))
                {
                    processorType = System.Reflection.Assembly.Load(metadata.AssemblyName).GetType(metadata.FullName);
                }
                if (processorType != null && typeof(IProcessor).IsAssignableFrom(processorType))
                {
                    instance = (IProcessor?)Activator.CreateInstance(processorType);
                }
                if (instance == null)
                {
                    throw new Exception();
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException($"The processor for command '{metadata.Name}' could not be created. Ensure that the processor implements IProcessor and has a parameterless constructor.", e);
            }
            return instance!;
        }
    }
}
