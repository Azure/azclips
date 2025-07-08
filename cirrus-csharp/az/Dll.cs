using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace AzTool
{
    public class DllLoader
    {
        /// <summary>
        /// Loads a DLL from file path into memory
        /// </summary>
        /// <param name="dllPath">Path to the DLL file</param>
        /// <returns>Loaded Assembly</returns>
        public static Assembly LoadDllIntoMemory(string dllPath)
        {
            try
            {
                // Method 1: Load from file path
                return Assembly.LoadFrom(dllPath);

                // Alternative Method 2: Load as bytes (completely in memory)
                // byte[] dllBytes = File.ReadAllBytes(dllPath);
                // return Assembly.Load(dllBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error loading DLL: {ex.Message}");
                throw;
            }
        }
    }
    
    public class StringAttributeTypeProvider : ICustomAttributeTypeProvider<string>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
        public string GetSystemType() => "System.Type";
        public string GetTypeFromSerializedName(string name) => name;
        public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;
        public bool IsSystemType(string type) => type == "System.Type";
        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
            reader.GetString(reader.GetTypeDefinition(handle).Name);
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
            reader.GetString(reader.GetTypeReference(handle).Name);
    }

    public class DllReader
    {
        PEReader PEReader { get; }
        MetadataReader? MetadataReader => PEReader.HasMetadata ? PEReader.GetMetadataReader() : null;

        public DllReader(System.IO.Stream stream)
        {
            PEReader = new PEReader(stream);
        }

        /// <summary>
        /// Gets a custom attribute of type T from the assembly.
        /// </summary>
        /// <typeparam name="T">The type of the custom attribute to retrieve.</typeparam>
        /// <returns>An instance of the custom attribute if found, otherwise null.</returns>
        /// <remarks>
        /// This method supports string-based custom attributes only.
        /// </remarks>
        public T? GetCustomAttribute<T>() where T : Attribute
        {
            if (MetadataReader == null)
            {
                return null;
            }

            foreach (var handler in MetadataReader.CustomAttributes)
            {
                var attribute = MetadataReader.GetCustomAttribute(handler);

                if (attribute.Constructor.Kind != HandleKind.MemberReference)
                    continue;
                var MemberReference = MetadataReader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);

                var container = MemberReference.Parent;
                if (container.Kind == HandleKind.TypeReference)
                {
                    var typeRef = MetadataReader.GetTypeReference((TypeReferenceHandle)container);
                    if (MetadataReader.GetString(typeRef.Name) == typeof(T).Name && MetadataReader.GetString(typeRef.Namespace) == typeof(T).Namespace)
                    {
                        // TODO: Create an instance of T using reflection using the properties of the attribute, the attribute is like "[assembly: ModuleInfo(Name = "Databricks", Description = "Manage databricks workspaces.")]"
                        var provider = new StringAttributeTypeProvider();
                        var decoded = attribute.DecodeValue(provider);
                        var ctorArgs = decoded.FixedArguments.Select(arg => arg.Value).ToArray();
                        // Create instance using constructor arguments
                        var instance = (T?)Activator.CreateInstance(typeof(T), ctorArgs);
                        if (instance == null)
                            continue;
                        // Set named arguments (e.g., properties like Name, Description)
                        foreach (var namedArg in decoded.NamedArguments)
                        {
                            if (namedArg.Name == null)
                                continue;
                            var member = typeof(T).GetProperty(namedArg.Name) ?? (MemberInfo?)typeof(T).GetField(namedArg.Name);
                            if (member is PropertyInfo prop)
                            {
                                prop.SetValue(instance, namedArg.Value);
                            }
                            else if (member is FieldInfo field)
                            {
                                field.SetValue(instance, namedArg.Value);
                            }
                        }
                        return instance;
                    }
                }
            }
            return null;
        }
    }

}