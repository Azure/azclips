using System.Reflection;
using Core.Abstractions;

namespace Core.Engine
{
    public static class CommandLoadingExtension
    {
        public static IExtensionIndex GetExtensionIndex(this Assembly assembly)
        {
            string assemblyName = assembly.GetName().Name ?? throw new InvalidOperationException($"The assembly name could not be determined for assembly '{assembly.FullName}'.");
            string typeName = $"{assemblyName}.ExtensionIndex";
            Type type = assembly.GetType(typeName) ?? throw new InvalidOperationException($"No IExtensionIndex implementation found in assembly {assembly.FullName}");
            if (type == null)
            {
                throw new InvalidOperationException($"The type '{typeName}' could not be found in assembly '{assembly.FullName}'.");
            }
            if (!typeof(IExtensionIndex).IsAssignableFrom(type))
            {
                throw new InvalidOperationException($"The type '{typeName}' does not implement IExtensionIndex.");
            }
            return (Activator.CreateInstance(type) as IExtensionIndex)!;
        }

        public static IProcessor GetInstance(this CommandMetadata metadata)
        {
            IProcessor? instance = null;
            try
            {
                var commandType = metadata.CommandType;
                if (commandType == null && !String.IsNullOrEmpty(metadata.FullName) && !String.IsNullOrEmpty(metadata.AssemblyName))
                {
                    commandType = System.Reflection.Assembly.Load(metadata.AssemblyName).GetType(metadata.FullName);
                }
                if (commandType != null && typeof(IProcessor).IsAssignableFrom(commandType))
                {
                    instance = (IProcessor?)Activator.CreateInstance(commandType);
                }
                if (instance == null)
                {
                    throw new Exception();
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException($"The command '{metadata.Name}' could not be created. Ensure that the command implements IProcessor and has a parameterless constructor.", e);
            }
            return instance!;
        }
    }
}
