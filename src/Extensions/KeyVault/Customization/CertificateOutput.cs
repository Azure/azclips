using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

internal static class CertificateOutput
{
    internal static object? Convert(object? value) => value switch
    {
        CertificateBundle certificate => new CertificateResult(certificate.Id, certificate.X509Thumbprint, certificate.Attributes, certificate.Tags,
            certificate.Cer, certificate.Kid, certificate.Sid, certificate.ContentType, certificate.Policy),
        DeletedCertificateBundle certificate => new DeletedCertificateResult(certificate),
        CertificateItem certificate => new CertificateMetadata(certificate.Id, certificate.X509Thumbprint, certificate.Attributes, certificate.Tags),
        IEnumerable<CertificateItem> certificates => certificates.Select(certificate => new CertificateMetadata(certificate.Id,
            certificate.X509Thumbprint, certificate.Attributes, certificate.Tags)).ToArray(),
        CertificateOperation operation => new CertificateOperationResult(operation),
        _ => value
    };

    internal static DateTimeOffset? Date(int? seconds) => seconds.HasValue ? DateTimeOffset.FromUnixTimeSeconds(seconds.Value) : null;

    internal static string? Name(string? id, int segment)
    {
        if (!Uri.TryCreate(id, UriKind.Absolute, out var uri)) return null;
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > segment ? Uri.UnescapeDataString(parts[segment]) : null;
    }

    internal static string? Vault(string? id) => Uri.TryCreate(id, UriKind.Absolute, out var uri) ? uri.Host.Split('.')[0] : null;
}

public sealed record CertificateAttributeResult(
    DateTimeOffset? Created, bool? Enabled, DateTimeOffset? Expires, DateTimeOffset? NotBefore, string? RecoveryLevel, DateTimeOffset? Updated)
{
    internal static CertificateAttributeResult From(CertificateAttributes? attributes) => new(
        CertificateOutput.Date(attributes?.Created), attributes?.Enabled, CertificateOutput.Date(attributes?.Expires),
        CertificateOutput.Date(attributes?.NotBefore), attributes?.RecoveryLevel, CertificateOutput.Date(attributes?.Updated));
}

public class CertificateMetadata
{
    internal CertificateMetadata(string? id, byte[]? thumbprint, CertificateAttributes? attributes, IDictionary<string, string>? tags, bool includeSubject = true)
    {
        Id = id;
        X509Thumbprint = thumbprint;
        Attributes = CertificateAttributeResult.From(attributes);
        Tags = tags;
        Subject = includeSubject ? string.Empty : null;
    }

    public string? Id { get; }
    public string? Name => CertificateOutput.Name(Id, 1);
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Subject { get; }
    public CertificateAttributeResult Attributes { get; }
    public IDictionary<string, string>? Tags { get; }
    public byte[]? X509Thumbprint { get; }
    public string? X509ThumbprintHex => X509Thumbprint is null ? null : System.Convert.ToHexString(X509Thumbprint);
    [JsonIgnore] public string? VaultName => CertificateOutput.Vault(Id);
    [JsonIgnore] public string? Version => CertificateOutput.Name(Id, 2);
    [JsonIgnore] public bool? Enabled => Attributes.Enabled;
    [JsonIgnore] public DateTime? Created => Attributes.Created?.UtcDateTime;
    [JsonIgnore] public DateTime? Updated => Attributes.Updated?.UtcDateTime;
    [JsonIgnore] public DateTime? Expires => Attributes.Expires?.UtcDateTime;
    [JsonIgnore] public DateTime? NotBefore => Attributes.NotBefore?.UtcDateTime;
    [JsonIgnore] public string? RecoveryLevel => Attributes.RecoveryLevel;
    [JsonIgnore] public bool? AttributesEnabled => Enabled;
}

public class CertificateResult : CertificateMetadata
{
    private readonly Lazy<X509Certificate2?> certificate;

    internal CertificateResult(string? id, byte[]? thumbprint, CertificateAttributes? attributes, IDictionary<string, string>? tags,
        byte[]? cer, string? keyId, string? secretId, string? contentType, CertificatePolicy? policy)
        : base(id, thumbprint, attributes, tags, includeSubject: false)
    {
        Cer = cer;
        Kid = keyId;
        Sid = secretId;
        ContentType = contentType;
        Policy = policy is null ? null : new CertificatePolicyResult(policy, id);
        certificate = new Lazy<X509Certificate2?>(() => Cer is { Length: > 0 } ? X509CertificateLoader.LoadCertificate(Cer) : null);
    }

    public byte[]? Cer { get; }
    public string? Kid { get; }
    public string? Sid { get; }
    public string? ContentType { get; }
    public CertificatePolicyResult? Policy { get; }
    [JsonIgnore] public string? KeyId => Kid;
    [JsonIgnore] public string? SecretId => Sid;
    [JsonIgnore] public string? Thumbprint => X509ThumbprintHex ?? Certificate?.Thumbprint;
    [JsonIgnore] public X509Certificate2? Certificate => certificate.Value;
}

public sealed class DeletedCertificateResult : CertificateResult
{
    internal DeletedCertificateResult(DeletedCertificateBundle certificate)
        : base(certificate.Id, certificate.X509Thumbprint, certificate.Attributes, certificate.Tags, certificate.Cer,
            certificate.Kid, certificate.Sid, certificate.ContentType, certificate.Policy)
    {
        RecoveryId = certificate.RecoveryId;
        DeletedDate = CertificateOutput.Date(certificate.DeletedDate);
        ScheduledPurgeDate = CertificateOutput.Date(certificate.ScheduledPurgeDate);
    }

    public string? RecoveryId { get; }
    public DateTimeOffset? DeletedDate { get; }
    public DateTimeOffset? ScheduledPurgeDate { get; }
}

public sealed class CertificateOperationResult
{
    internal CertificateOperationResult(CertificateOperation operation)
    {
        Id = operation.Id;
        Status = operation.Status;
        StatusDetails = operation.StatusDetails;
        RequestId = operation.RequestId;
        Target = operation.Target;
        Csr = operation.Csr;
        CancellationRequested = operation.CancellationRequested;
        IssuerParameters = operation.IssuerParameters is null ? null : new CertificateIssuerResult(
            operation.IssuerParametersCertificateTransparency, operation.IssuerParametersCertificateType, operation.IssuerParametersName);
        Error = operation.Error;
    }

    public string? Id { get; }
    public string? Name => CertificateOutput.Name(Id, 1);
    public string? Status { get; }
    public string? StatusDetails { get; }
    public string? RequestId { get; }
    public string? Target { get; }
    public byte[]? Csr { get; }
    public bool? CancellationRequested { get; }
    public CertificateIssuerResult? IssuerParameters { get; }
    public KeyVaultErrorError? Error { get; }
    [JsonIgnore] public string? VaultName => CertificateOutput.Vault(Id);
    [JsonIgnore] public string? Issuer => IssuerParameters?.Name;
    [JsonIgnore] public string? CertificateSigningRequest => Csr is { Length: > 0 } ? System.Convert.ToBase64String(Csr) : null;
    [JsonIgnore] public string? ErrorCode => Error?.Code;
    [JsonIgnore] public string? ErrorMessage => Error?.Message;
}

public sealed record CertificateIssuerResult(bool? CertificateTransparency, string? CertificateType, string? Name);

public sealed class CertificatePolicyResult
{
    private readonly CertificateAttributeResult attributes;

    internal CertificatePolicyResult(CertificatePolicy policy, string? certificateId)
    {
        Id = policy.Id;
        if (Id is null && Uri.TryCreate(certificateId, UriKind.Absolute, out var uri))
            Id = new Uri(uri, "policy").AbsoluteUri;
        attributes = CertificateAttributeResult.From(policy.Attributes);
        Enabled = attributes.Enabled;
        IssuerName = policy.IssuerParametersName;
        CertificateType = policy.IssuerParametersCertificateType;
        CertificateTransparency = policy.IssuerParametersCertificateTransparency;
        Kty = policy.KeyPropertiesKeyType;
        KeySize = policy.KeyPropertiesKeySize;
        Curve = policy.KeyPropertiesCurve;
        Exportable = policy.KeyPropertiesExportable;
        ReuseKeyOnRenewal = policy.KeyPropertiesReuseKey;
        SecretContentType = policy.SecretPropertiesContentType;
        SubjectName = policy.X509CertificatePropertiesSubject;
        ValidityInMonths = policy.X509CertificatePropertiesValidityInMonths;
        KeyUsage = policy.X509CertificatePropertiesKeyUsage;
        Ekus = policy.X509CertificatePropertiesEkus;
        DnsNames = policy.SubjectAlternativeNamesDnsNames;
        Emails = policy.SubjectAlternativeNamesEmails;
        UserPrincipalNames = policy.SubjectAlternativeNamesUpns;
        foreach (var action in policy.LifetimeActions ?? [])
        {
            if (action.ActionActionType == "AutoRenew")
            {
                RenewAtNumberOfDaysBeforeExpiry = action.TriggerDaysBeforeExpiry;
                RenewAtPercentageLifetime = action.TriggerLifetimePercentage;
            }
            else if (action.ActionActionType == "EmailContacts")
            {
                EmailAtNumberOfDaysBeforeExpiry = action.TriggerDaysBeforeExpiry;
                EmailAtPercentageLifetime = action.TriggerLifetimePercentage;
            }
        }
    }

    public string? Id { get; }
    public CertificateAttributeResult Attributes => attributes with { Enabled = Enabled };
    public CertificateIssuerResult IssuerParameters => new(CertificateTransparency, CertificateType, IssuerName);
    public object KeyProperties => new { curve = Curve, exportable = Exportable, keySize = KeySize, keyType = Kty, reuseKey = ReuseKeyOnRenewal };
    public object SecretProperties => new { contentType = SecretContentType };
    public object X509CertificateProperties => new
    {
        ekus = Ekus, keyUsage = KeyUsage, subject = SubjectName,
        subjectAlternativeNames = new { emails = Emails, upns = UserPrincipalNames, dnsNames = DnsNames }, validityInMonths = ValidityInMonths
    };
    public object[] LifetimeActions => new[]
    {
        (Name: "AutoRenew", Days: RenewAtNumberOfDaysBeforeExpiry, Percentage: RenewAtPercentageLifetime),
        (Name: "EmailContacts", Days: EmailAtNumberOfDaysBeforeExpiry, Percentage: EmailAtPercentageLifetime)
    }.Where(action => action.Days.HasValue || action.Percentage.HasValue).Select(action => (object)new
    {
        action = new { actionType = action.Name },
        trigger = new { daysBeforeExpiry = action.Days, lifetimePercentage = action.Percentage }
    }).ToArray();
    [JsonIgnore] public string? SecretContentType { get; set; }
    [JsonIgnore] public string? Kty { get; set; }
    [JsonIgnore] public int? KeySize { get; set; }
    [JsonIgnore] public string? Curve { get; set; }
    [JsonIgnore] public bool? Exportable { get; set; }
    [JsonIgnore] public bool? ReuseKeyOnRenewal { get; set; }
    [JsonIgnore] public string? SubjectName { get; set; }
    [JsonIgnore] public IList<string>? DnsNames { get; set; }
    [JsonIgnore] public IList<string>? Emails { get; set; }
    [JsonIgnore] public IList<string>? UserPrincipalNames { get; set; }
    [JsonIgnore] public IList<string>? KeyUsage { get; set; }
    [JsonIgnore] public IList<string>? Ekus { get; set; }
    [JsonIgnore] public int? ValidityInMonths { get; set; }
    [JsonIgnore] public string? IssuerName { get; set; }
    [JsonIgnore] public string? CertificateType { get; set; }
    [JsonIgnore] public bool? CertificateTransparency { get; set; }
    [JsonIgnore] public int? RenewAtNumberOfDaysBeforeExpiry { get; set; }
    [JsonIgnore] public int? RenewAtPercentageLifetime { get; set; }
    [JsonIgnore] public int? EmailAtNumberOfDaysBeforeExpiry { get; set; }
    [JsonIgnore] public int? EmailAtPercentageLifetime { get; set; }
    [JsonIgnore] public bool? Enabled { get; set; }
    [JsonIgnore] public DateTime? Created => Attributes.Created?.UtcDateTime;
    [JsonIgnore] public DateTime? Updated => Attributes.Updated?.UtcDateTime;
    [JsonIgnore] public int? X509CertificatePropertiesValidityInMonths => ValidityInMonths;
}