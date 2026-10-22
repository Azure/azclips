using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

internal static class CertificateCommandInputs
{
    internal static void Validate(string name, string? version = null)
    {
        if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, "^[a-zA-Z0-9-]{1,127}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Certificate names must contain 1-127 letters, digits or hyphens.");
        if (version is not null && !Regex.IsMatch(version, "^[a-zA-Z0-9]{1,64}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Certificate version must be an alphanumeric version identifier.");
    }
}

public class ShowCertificateCommand : GetCertificateProcessor
{
    protected override Task BeforeProcessAsync()
    {
        CertificateCommandInputs.Validate(CertificateName, CertificateVersion);
        return base.BeforeProcessAsync();
    }
}

public class ListCertificatesCommand : GetCertificatesProcessor
{
    public int PageSize { get => Maxresults ?? 25; set => Maxresults = value; }
    public bool IncludePendingInput { get => IncludePending ?? false; set => IncludePending = value; }

    protected override string CreateRequestUri() => WithOptionalQueryParameters(base.CreateRequestUri(),
        ("maxresults", Maxresults?.ToString(CultureInfo.InvariantCulture)),
        ("includePending", IncludePending?.ToString().ToLowerInvariant()));

    protected override Task BeforeProcessAsync()
    {
        SecretCommandInputs.ValidatePageSize(PageSize);
        return base.BeforeProcessAsync();
    }
}

public class ListCertificateVersionsCommand : GetCertificateVersionsProcessor
{
    public int PageSize { get => Maxresults ?? 25; set => Maxresults = value; }

    protected override string CreateRequestUri() =>
        WithOptionalQueryParameters(base.CreateRequestUri(), ("maxresults", Maxresults?.ToString(CultureInfo.InvariantCulture)));

    protected override Task BeforeProcessAsync()
    {
        CertificateCommandInputs.Validate(CertificateName);
        SecretCommandInputs.ValidatePageSize(PageSize);
        return base.BeforeProcessAsync();
    }
}

public class RemoveCertificateCommand : DeleteCertificateProcessor
{
    public bool Yes { get; set; }

    protected override Task BeforeProcessAsync()
    {
        CertificateCommandInputs.Validate(CertificateName);
        if (!Yes) throw new ArgumentException("Deletion affects all certificate versions. Confirm with --yes or PowerShell ShouldProcess.");
        return base.BeforeProcessAsync();
    }
}

public class RecoverCertificateCommand : RecoverDeletedCertificateProcessor
{
    protected override Task BeforeProcessAsync()
    {
        CertificateCommandInputs.Validate(CertificateName);
        return base.BeforeProcessAsync();
    }
}

public class DownloadCertificateCommand : ShowCertificateCommand
{
    public string File { get; set; } = string.Empty;
    public string Encoding { get; set; } = "PEM";
    private string destination = string.Empty;

    protected override Task BeforeProcessAsync()
    {
        if (string.IsNullOrWhiteSpace(File)) throw new ArgumentException("Specify a destination file.");
        if (!string.Equals(Encoding, "PEM", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Encoding, "DER", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Certificate encoding must be PEM or DER.");
        destination = Path.GetFullPath(File);
        if (System.IO.File.Exists(destination) || Directory.Exists(destination))
            throw new IOException("The certificate destination already exists. Choose a new path.");
        return base.BeforeProcessAsync();
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(CertificateBundle? body)
    {
        if (body?.Cer is not { Length: > 0 } certificate)
            throw new InvalidOperationException("The response contains no public certificate to download.");
        try
        {
            using var parsed = X509CertificateLoader.LoadCertificate(certificate);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("The response contains an invalid public certificate.");
        }
        byte[] bytes = string.Equals(Encoding, "DER", StringComparison.OrdinalIgnoreCase)
            ? certificate : System.Text.Encoding.ASCII.GetBytes(PemEncoding.WriteString("CERTIFICATE", certificate) + "\n");
        bool created = false;
        try
        {
            await using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            created = true;
            await stream.WriteAsync(bytes, Token);
            await stream.FlushAsync(Token);
        }
        catch
        {
            if (created) System.IO.File.Delete(destination);
            throw;
        }
        await WriteOutput(new CertificateDownloadResult(destination, Encoding.ToUpperInvariant(), body.Id));
        return true;
    }
}

public sealed record CertificateDownloadResult(string File, string Encoding, string? Id);