using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

public class SetSecretCommand : SetSecretProcessor
{
    public SecureString? SecureValue { get; set; }
    public string? File { get; set; }
    public string? Enabled { get; set; }
    public string? Expires { get; set; }
    public string? NotBefore { get; set; }
    public string[]? TagsInput { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        SecretCommandInputs.ValidateName(SecretName);
        if ((Value is null ? 0 : 1) + (SecureValue is null ? 0 : 1) + (File is null ? 0 : 1) != 1)
            throw new ArgumentException("Specify exactly one secret value source: value, file or SecureString.");
        if (Enabled is not null) SecretAttributesEnabled = SecretCommandInputs.ParseEnabled(Enabled);
        if (Expires is not null) SecretAttributesExpires = SecretCommandInputs.ParseTime(Expires);
        if (NotBefore is not null) SecretAttributesNotBefore = SecretCommandInputs.ParseTime(NotBefore);
        Tags = SecretCommandInputs.ParseTags(TagsInput) ?? Tags;
        await base.BeforeProcessAsync();
        if (File is not null)
        {
            try
            {
                using var stream = System.IO.File.OpenRead(File);
                if (stream.Length > 25 * 1024) throw new ArgumentException("Secret values must not exceed 25 KiB.");
                using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
                Value = await reader.ReadToEndAsync(Token);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                throw new ArgumentException("The secret file could not be read as UTF-8.");
            }
        }
        else if (SecureValue is not null)
        {
            IntPtr buffer = Marshal.SecureStringToGlobalAllocUnicode(SecureValue);
            try { Value = Marshal.PtrToStringUni(buffer, SecureValue.Length); }
            finally { Marshal.ZeroFreeGlobalAllocUnicode(buffer); }
        }
        if (Value is null || Encoding.UTF8.GetByteCount(Value) > 25 * 1024)
            throw new ArgumentException("Secret values must not exceed 25 KiB.");
    }

    public override async Task ProcessAsync()
    {
        try { await base.ProcessAsync(); }
        finally { Value = null; SecureValue = null; }
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(SecretBundle? body)
    {
        if (body is not null) await WriteOutput(SecretMetadata.From(body));
        return true;
    }
}

public class ShowSecretCommand : GetSecretProcessor
{
    public bool AsPlainText { get; set; }

    protected override Task BeforeProcessAsync()
    {
        SecretCommandInputs.ValidateName(SecretName);
        SecretCommandInputs.ValidateVersion(SecretVersion);
        return base.BeforeProcessAsync();
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(SecretBundle? body)
    {
        if (body is not null)
            await WriteOutput(AsPlainText ? body.Value ?? string.Empty : SecretMetadata.From(body));
        return true;
    }
}

public class UpdateSecretCommand : UpdateSecretProcessor
{
    public string? Enabled { get; set; }
    public string? Expires { get; set; }
    public string? NotBefore { get; set; }
    public string[]? TagsInput { get; set; }

    protected override Task BeforeProcessAsync()
    {
        SecretCommandInputs.ValidateName(SecretName);
        SecretCommandInputs.ValidateVersion(SecretVersion);
        if (Enabled is null && Expires is null && NotBefore is null && TagsInput is null && ContentType is null &&
            SecretAttributesEnabled is null && SecretAttributesExpires is null && SecretAttributesNotBefore is null && Tags is null)
            throw new ArgumentException("Specify at least one attribute, content type or tag update.");
        if (Enabled is not null) SecretAttributesEnabled = SecretCommandInputs.ParseEnabled(Enabled);
        if (Expires is not null) SecretAttributesExpires = SecretCommandInputs.ParseTime(Expires);
        if (NotBefore is not null) SecretAttributesNotBefore = SecretCommandInputs.ParseTime(NotBefore);
        Tags = SecretCommandInputs.ParseTags(TagsInput) ?? Tags;
        return base.BeforeProcessAsync();
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(SecretBundle? body)
    {
        if (body is not null) await WriteOutput(SecretMetadata.From(body));
        return true;
    }
}

public class RemoveSecretCommand : DeleteSecretProcessor
{
    public bool Yes { get; set; }

    protected override Task BeforeProcessAsync()
    {
        SecretCommandInputs.ValidateName(SecretName);
        if (!Yes) throw new ArgumentException("Deletion affects all secret versions. Confirm with --yes or PowerShell ShouldProcess.");
        return base.BeforeProcessAsync();
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeletedSecretBundle? body)
    {
        if (body is not null) await WriteOutput(SecretMetadata.From(body));
        return true;
    }
}

public class RecoverSecretCommand : RecoverDeletedSecretProcessor
{
    protected override Task BeforeProcessAsync()
    {
        SecretCommandInputs.ValidateName(SecretName);
        return base.BeforeProcessAsync();
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(SecretBundle? body)
    {
        if (body is not null) await WriteOutput(SecretMetadata.From(body));
        return true;
    }
}

public class ListSecretsCommand : GetSecretsProcessor
{
    public int PageSize { get => Maxresults ?? 25; set => Maxresults = value; }

    protected override Task BeforeProcessAsync()
    {
        SecretCommandInputs.ValidatePageSize(PageSize);
        return base.BeforeProcessAsync();
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(SecretListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(page => page.Value?.Select(SecretMetadata.From), page => page.NextLink,
            CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}

public class ListSecretVersionsCommand : GetSecretVersionsProcessor
{
    public int PageSize { get => Maxresults ?? 25; set => Maxresults = value; }

    protected override Task BeforeProcessAsync()
    {
        SecretCommandInputs.ValidateName(SecretName);
        SecretCommandInputs.ValidatePageSize(PageSize);
        return base.BeforeProcessAsync();
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(SecretListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(page => page.Value?.Select(SecretMetadata.From), page => page.NextLink,
            CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}