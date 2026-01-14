using Core.Abstractions;

namespace Core.Implementation
{
    public static class CommandMetadataExtension
    {
        public static IProcessor GetInstance(this ICommandMetadata metadata)
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

        public static void SetProperty(this IProcessor processor, string propertyName, object? value)
        {
            var propertyInfo = processor.GetType().GetProperty(propertyName);
            if (propertyInfo == null)
            {
                throw new ArgumentException($"The property '{propertyName}' does not exist on command '{processor.GetType().FullName}'.");
            }
            try
            {
                if (value != null)
                {
                    var convertedValue = Convert.ChangeType(value, propertyInfo.PropertyType);
                    propertyInfo.SetValue(processor, convertedValue);
                }
                else
                {
                    propertyInfo.SetValue(processor, null);
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException($"The property '{propertyName}' on command '{processor.GetType().FullName}' could not be set to value '{value}'.", e);
            }
        }
    }
}
