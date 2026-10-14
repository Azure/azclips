using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

public sealed record SecretMetadata(
    string? Id,
    string? ContentType,
    SecretAttributes? Attributes,
    IDictionary<string, string>? Tags,
    bool? Managed,
    string? RecoveryId = null,
    int? DeletedDate = null,
    int? ScheduledPurgeDate = null)
{
    internal static SecretMetadata From(SecretBundle body) =>
        new(body.Id, body.ContentType, body.Attributes, body.Tags, body.Managed);

    internal static SecretMetadata From(DeletedSecretBundle body) =>
        new(body.Id, body.ContentType, body.Attributes, body.Tags, body.Managed,
            body.RecoveryId, body.DeletedDate, body.ScheduledPurgeDate);

    internal static SecretMetadata From(SecretItem body) =>
        new(body.Id, body.ContentType, body.Attributes, body.Tags, body.Managed);
}