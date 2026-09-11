using System.Text.Json;

namespace Ace.Generator.Model.Ir;

/// <summary>
/// Canonical System.Text.Json settings for (de)serializing IR documents.
///
/// The IR's discriminated unions rely on STJ polymorphism, but the emitted IR
/// does not place the discriminator (<c>kind</c> / <c>typeKind</c> / <c>type</c>)
/// first in each object. <see cref="JsonSerializerOptions.AllowOutOfOrderMetadataProperties"/>
/// lets STJ find the discriminator wherever it appears, so consumers must use
/// these options (or set that flag themselves) when reading IR.
/// </summary>
public static class IrJson
{
    /// <summary>Shared, read-optimized options for IR (de)serialization.</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        AllowOutOfOrderMetadataProperties = true,
    };

    /// <summary>Deserialize an IR document from JSON using <see cref="Options"/>.</summary>
    public static IRDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<IRDocument>(json, Options)
        ?? throw new JsonException("IR document deserialized to null.");
}