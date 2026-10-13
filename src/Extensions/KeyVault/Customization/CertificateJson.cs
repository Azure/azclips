using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

internal static class CertificateJson
{
    public sealed class ThumbprintConverter : JsonConverter<byte[]>
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