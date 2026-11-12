using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

internal static class CertificateFiles
{
    internal const int MaximumBytes = 1024 * 1024;

    internal static async Task<byte[]> Read(string file, int maximumBytes, CancellationToken token)
    {
        try
        {
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            if (stream.Length > maximumBytes) throw new ArgumentException("The input file exceeds the supported size limit.");
            byte[] buffer = new byte[maximumBytes + 1];
            try
            {
                int count = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, token);
                if (count > maximumBytes) throw new ArgumentException("The input file exceeds the supported size limit.");
                return buffer.AsSpan(0, count).ToArray();
            }
            finally { CryptographicOperations.ZeroMemory(buffer); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException("The certificate input file could not be read.");
        }
    }

    internal static async Task<string> ReadText(string file, int maximumBytes, CancellationToken token)
    {
        byte[] bytes = await Read(file, maximumBytes, token);
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { throw new ArgumentException("The input file must contain valid UTF-8."); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static async Task<CertificatePolicy?> Policy(string? json, string? file, CertificatePolicy? inherited, JsonSerializerOptions options, CancellationToken token)
    {
        if ((json is null ? 0 : 1) + (file is null ? 0 : 1) + (inherited is null ? 0 : 1) > 1)
            throw new ArgumentException("Specify one policy source: JSON, file or typed policy.");
        if (json?.StartsWith('@') == true)
        {
            file = json[1..];
            json = null;
        }
        if (file is not null) json = await ReadText(file, MaximumBytes, token);
        if (json is null) return inherited;
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new ArgumentException("Policy JSON exceeds 1 MiB.");
        try
        {
            var strict = new JsonSerializerOptions(options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
            return CertificateJson.NormalizePolicy(json).Deserialize<CertificatePolicy>(strict) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new ArgumentException("Policy must be a JSON object using supported classic CLI or REST policy fields without conflicting values.");
        }
    }

    internal static string Unprotect(SecureString value)
    {
        IntPtr buffer = Marshal.SecureStringToGlobalAllocUnicode(value);
        try { return Marshal.PtrToStringUni(buffer, value.Length)!; }
        finally { Marshal.ZeroFreeGlobalAllocUnicode(buffer); }
    }

    internal static void ValidatePkcs12(byte[] bytes, string? password)
    {
        var certificates = X509CertificateLoader.LoadPkcs12Collection(bytes, password);
        try
        {
            if (!certificates.Any(certificate => certificate.HasPrivateKey))
                throw new CryptographicException();
        }
        finally
        {
            foreach (var certificate in certificates) certificate.Dispose();
        }
    }
}

public class CreateCertificateCommand : CreateCertificateProcessor
{
    public string? Policy { get; set; }
    public string? PolicyFile { get; set; }
    public string? Enabled { get; set; }
    public string? Expires { get; set; }
    public string? NotBefore { get; set; }
    public string[]? TagsInput { get; set; }
    public bool NoWait { get; set; }
    public int TimeoutSeconds { get; set; } = 300;

    protected override async Task BeforeProcessAsync()
    {
        CertificateCommandInputs.Validate(CertificateName);
        if (TimeoutSeconds is < 1 or > 3600) throw new ArgumentException("Timeout must be between 1 and 3600 seconds.");
        if (Enabled is not null) CertificateAttributesEnabled = SecretCommandInputs.ParseEnabled(Enabled);
        if (Expires is not null) CertificateAttributesExpires = SecretCommandInputs.ParseTime(Expires);
        if (NotBefore is not null) CertificateAttributesNotBefore = SecretCommandInputs.ParseTime(NotBefore);
        Tags = SecretCommandInputs.ParseTags(TagsInput) ?? Tags;
        await base.BeforeProcessAsync();
        CertificatePolicy = await CertificateFiles.Policy(Policy, PolicyFile, CertificatePolicy, JsonOptions, Token)
            ?? throw new ArgumentException("An explicit certificate policy is required.");
    }

    protected override async Task<HttpResponseMessage> SendCoreAsync()
    {
        // TODO: Replace custom polling when spec/directive metadata and codegen/runtime
        // support the certificate pending endpoint, terminal states, and required command behavior.
        var originalPublisher = Publisher;
        var originalToken = Token;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(originalToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        Publisher = new CertificateOperationPublisher(originalPublisher, timeout.Token);
        HttpResponseMessage? response = null;
        try
        {
            Token.ThrowIfCancellationRequested();
            response = await base.SendCoreAsync();
            var poller = CreateExtendedProcessor<GetCertificateOperationProcessor>(child => child.CertificateName = CertificateName);
            while (true)
            {
                Token.ThrowIfCancellationRequested();
                CertificateOperation operation;
                try
                {
                    operation = JsonSerializer.Deserialize<CertificateOperation>(await response.Content.ReadAsStringAsync(Token), JsonOptions)
                        ?? throw new JsonException();
                }
                catch (JsonException) { throw new InvalidOperationException("The certificate operation response is invalid. Sensitive details omitted."); }
                string state = operation.Status?.ToLowerInvariant() ?? string.Empty;
                if (state is "failed" or "canceled" or "cancelled")
                    throw new InvalidOperationException($"Certificate creation {state}. Sensitive service details omitted.");
                if (state != "completed" && state != "inprogress")
                    throw new InvalidOperationException("The certificate operation returned an unsupported status.");
                if (state == "completed" || NoWait || string.Equals(CertificatePolicy!.IssuerParametersName, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    var result = response;
                    response = null;
                    return result;
                }
                TimeSpan delay = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(HttpPipeline.PollingInterval));
                delay = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
                await Publisher.Publish(Events.DelayPolling, "Waiting for the certificate operation.");
                response.Dispose();
                response = null;
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(Token, timeout.Token);
                await Task.Delay(delay, wait.Token);
                await Publisher.Publish(Events.BeforePollingCall);
                response = await poller.SendAsync();
                await Publisher.Publish(Events.AfterPollingCall);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !originalToken.IsCancellationRequested)
        {
            throw new TimeoutException("Certificate creation timed out. The server operation may still be pending.");
        }
        finally { response?.Dispose(); Publisher = originalPublisher; }
    }
}

public class ImportCertificateCommand : ImportCertificateProcessor
{
    public string? File { get; set; }
    public string? PasswordFile { get; set; }
    public SecureString? SecurePassword { get; set; }
    public string? Policy { get; set; }
    public string? PolicyFile { get; set; }
    public string? Enabled { get; set; }
    public string? Expires { get; set; }
    public string? NotBefore { get; set; }
    public string[]? TagsInput { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        CertificateCommandInputs.Validate(CertificateName);
        if ((File is null ? 0 : 1) + (Base64EncodedCertificate is null ? 0 : 1) != 1)
            throw new ArgumentException("Specify exactly one certificate source: file or encoded certificate.");
        if ((Password is null ? 0 : 1) + (PasswordFile is null ? 0 : 1) + (SecurePassword is null ? 0 : 1) > 1)
            throw new ArgumentException("Specify only one certificate password source.");
        if (Enabled is not null) CertificateAttributesEnabled = SecretCommandInputs.ParseEnabled(Enabled);
        if (Expires is not null) CertificateAttributesExpires = SecretCommandInputs.ParseTime(Expires);
        if (NotBefore is not null) CertificateAttributesNotBefore = SecretCommandInputs.ParseTime(NotBefore);
        Tags = SecretCommandInputs.ParseTags(TagsInput) ?? Tags;
        await base.BeforeProcessAsync();
        CertificatePolicy = await CertificateFiles.Policy(Policy, PolicyFile, CertificatePolicy, JsonOptions, Token);
        if (PasswordFile is not null) Password = await CertificateFiles.ReadText(PasswordFile, 16 * 1024, Token);
        else if (SecurePassword is not null) Password = CertificateFiles.Unprotect(SecurePassword);
        if (File is null) return;

        byte[] bytes = await CertificateFiles.Read(File, CertificateFiles.MaximumBytes, Token);
        try
        {
            bool pem = bytes.AsSpan().IndexOf("-----BEGIN CERTIFICATE-----"u8) >= 0;
            string contentType = pem ? "application/x-pem-file" : "application/x-pkcs12";
            if (CertificatePolicy?.SecretPropertiesContentType is { } declared && declared != contentType)
                throw new ArgumentException("The certificate file format conflicts with the policy content type.");
            CertificatePolicy ??= new CertificatePolicy();
            CertificatePolicy.SecretPropertiesContentType = contentType;
            if (pem)
            {
                string text = new UTF8Encoding(false, true).GetString(bytes);
                bool encrypted = text.Contains("-----BEGIN ENCRYPTED PRIVATE KEY-----", StringComparison.Ordinal);
                if (!encrypted && !text.Contains("-----BEGIN PRIVATE KEY-----", StringComparison.Ordinal))
                    throw new ArgumentException("PEM import requires a certificate and a PKCS#8 private key. Pending-CSR merge is not supported.");
                using var parsed = encrypted
                    ? X509Certificate2.CreateFromEncryptedPem(text, text, Password ?? string.Empty)
                    : X509Certificate2.CreateFromPem(text, text);
                if (!parsed.HasPrivateKey) throw new CryptographicException();
                Base64EncodedCertificate = text;
            }
            else
            {
                CertificateFiles.ValidatePkcs12(bytes, Password);
                Base64EncodedCertificate = Convert.ToBase64String(bytes);
            }
        }
        catch (Exception error) when (error is CryptographicException or DecoderFallbackException)
        {
            throw new ArgumentException("The certificate file or password is invalid, or the file has no matching private key.");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public override async Task ProcessAsync()
    {
        try { await base.ProcessAsync(); }
        finally { Base64EncodedCertificate = null; Password = null; SecurePassword = null; }
    }
}

public class UpdateCertificateCommand : UpdateCertificateProcessor
{
    public string? Policy { get; set; }
    public string? PolicyFile { get; set; }
    public string? Enabled { get; set; }
    public string? Expires { get; set; }
    public string? NotBefore { get; set; }
    public string[]? TagsInput { get; set; }
    private CertificatePolicy? policyUpdate;

    protected override async Task BeforeProcessAsync()
    {
        CertificateCommandInputs.Validate(CertificateName, CertificateVersion);
        if (Enabled is not null) CertificateAttributesEnabled = SecretCommandInputs.ParseEnabled(Enabled);
        if (Expires is not null) CertificateAttributesExpires = SecretCommandInputs.ParseTime(Expires);
        if (NotBefore is not null) CertificateAttributesNotBefore = SecretCommandInputs.ParseTime(NotBefore);
        Tags = SecretCommandInputs.ParseTags(TagsInput) ?? Tags;
        await base.BeforeProcessAsync();
        if (Policy is not null || PolicyFile is not null)
            policyUpdate = await CertificateFiles.Policy(Policy, PolicyFile, CertificatePolicy, JsonOptions, Token);
        if (policyUpdate is null && CertificatePolicy is null && Tags is null && CertificateAttributesEnabled is null &&
            CertificateAttributesExpires is null && CertificateAttributesNotBefore is null)
            throw new ArgumentException("Specify at least one certificate attribute, tag or policy update.");
    }

    protected override async Task<HttpResponseMessage> SendCoreAsync()
    {
        if (policyUpdate is not null)
        {
            var policy = CreateExtendedProcessor<UpdateCertificatePolicyProcessor>(child =>
            {
                child.CertificateName = CertificateName;
                child.Body = policyUpdate;
            });
            using var response = await policy.SendAsync();
            if (CertificatePolicy is null && Tags is null && CertificateAttributesEnabled is null &&
                CertificateAttributesExpires is null && CertificateAttributesNotBefore is null)
                return await CreateExtendedProcessor<GetCertificateProcessor>(child => child.CertificateName = CertificateName).SendAsync();
        }
        return await base.SendCoreAsync();
    }
}