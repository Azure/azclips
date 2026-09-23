using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

internal static class CertificateJson
{
    internal static void Configure(JsonSerializerOptions options)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(type =>
        {
            foreach (var property in type.Properties)
                if (property.Name == "x5t" && property.PropertyType == typeof(byte[]))
                    property.CustomConverter = new ThumbprintConverter();
        });
        options.TypeInfoResolver = resolver;
    }

    private sealed class ThumbprintConverter : JsonConverter<byte[]>
    {
        public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try { return Base64Url.DecodeFromChars(reader.GetString()!); }
            catch (Exception error) when (error is FormatException or ArgumentException)
            {
                throw new JsonException("Invalid certificate thumbprint.");
            }
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) =>
            writer.WriteStringValue(Base64Url.EncodeToString(value));
    }
}