namespace Core.Engine
{
    public static class EngineExtension
    {
        public static string ResolveAssemblyName(this string extensionName) => $"Microsoft.Azure.Cirrus.Extensions.{extensionName}";
        public static string ResolveExtensionName(this string extensionName)
        {
            //TODO: implement extension name resolution logic, this will map extension/module name passed by user to actual extension name in dll
            return extensionName;
        }
    }
}