// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Core.Cli.Dll;

public class DllReader(Stream DllStream) : IDisposable
{
    sealed class StringAttributeTypeProvider : ICustomAttributeTypeProvider<string>
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


    PEReader PEReader { get; } = new PEReader(DllStream);
    MetadataReader? MetadataReader => PEReader.HasMetadata ? PEReader.GetMetadataReader() : null;

    public void Dispose()
    {
        PEReader.Dispose();
        DllStream.Dispose();
        GC.SuppressFinalize(this);
    }

    public T? GetCustomAttribute<T>() where T : Attribute
    {
        if (MetadataReader == null)
        {
            return null;
        }
        foreach (var handle in MetadataReader.CustomAttributes)
        {
            var attribute = MetadataReader.GetCustomAttribute(handle);
            var ctorHandle = attribute.Constructor;
            if (ctorHandle.Kind != HandleKind.MemberReference) continue;
            var memberRef = MetadataReader.GetMemberReference((MemberReferenceHandle)ctorHandle);
            var parentHandle = memberRef.Parent;
            if (parentHandle.Kind != HandleKind.TypeReference) continue;
            var typeRef = MetadataReader.GetTypeReference((TypeReferenceHandle)parentHandle);
            if (MetadataReader.GetString(typeRef.Namespace) != typeof(T).Namespace) continue;
            if (MetadataReader.GetString(typeRef.Name) != typeof(T).Name) continue;
            var typeProvider = new StringAttributeTypeProvider();
            var decodedValue = attribute.DecodeValue(typeProvider);
            var ctorArgs = decodedValue.FixedArguments.Select(arg => arg.Value).ToArray();
            var instance = (T?)Activator.CreateInstance(typeof(T), ctorArgs);
            if (instance == null) continue;
            foreach (var namedArg in decodedValue.NamedArguments)
            {
                if (namedArg.Name == null) continue;
                var property = typeof(T).GetProperty(namedArg.Name);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, namedArg.Value);
                    continue;
                }
                var field = typeof(T).GetField(namedArg.Name);
                field?.SetValue(instance, namedArg.Value);
            }
            return instance;
        }
        return null;
    }
}
