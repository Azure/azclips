using System.Text.RegularExpressions;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;

public abstract partial class KeyVaultDataPlaneProcessorBase
{
    public string? VaultName { get; set; }
    public string? VaultUrl { get; set; }
    protected string Endpoint => VaultBaseUrl!.AbsoluteUri.TrimEnd('/');
    private bool dataPlanePrepared;

    private protected partial void EnsureExecutionAllowed() => PrepareDataPlane();

    protected static string WithOptionalQueryParameters(string generatedUri, params (string Name, string? Value)[] optionalParameters)
    {
        var uri = new UriBuilder(generatedUri);
        var generatedQuery = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
        query["api-version"] = generatedQuery.GetValues("api-version")?.FirstOrDefault()
            ?? throw new InvalidOperationException("The generated Key Vault request is missing its API version.");
        foreach (var parameter in optionalParameters)
            if (parameter.Value is not null) query[parameter.Name] = parameter.Value;
        uri.Query = query.ToString();
        uri.Fragment = string.Empty;
        return uri.Uri.AbsoluteUri;
    }

    protected void PrepareDataPlane()
    {
        if (!JsonOptions.IsReadOnly)
            JsonOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        if (ResolvedProfile is null || string.IsNullOrWhiteSpace(ResolvedProfile.TenantId))
            throw new InvalidOperationException("Sign in and select a profile before using Key Vault.");
        if (!string.IsNullOrEmpty(ResolvedProfile.CloudName) && ResolvedProfile.CloudName != "AzureCloud")
            throw new NotSupportedException("Key Vault currently supports AzureCloud only. Other clouds require cloud-aware authentication support.");
        if (VaultName is not null && VaultUrl is not null)
            throw new ArgumentException("Specify either a vault name or a vault URL, not both.");
        if (VaultName is not null)
        {
            ValidateVaultName(VaultName);
            VaultBaseUrl = new Uri($"https://{VaultName}.vault.azure.net/");
        }
        else if (VaultUrl is not null)
        {
            if (!Uri.TryCreate(VaultUrl, UriKind.Absolute, out var endpoint))
                throw new ArgumentException("The vault URL must be an absolute HTTPS URL.");
            VaultBaseUrl = endpoint;
        }
        ValidateEndpoint(VaultBaseUrl);
        if (dataPlanePrepared) return;

        var writeOutput = WriteOutput;
        WriteOutput = (value, cancellationToken) => writeOutput(Customization.CertificateOutput.Convert(value)!, cancellationToken);

        Uri vault = VaultBaseUrl!;
        AcquireTokenAsyncDelegate acquireToken = AuthHandler.AcquireTokenAsyncDefault;
        HttpPipeline.Prepend(async (request, callback, next) =>
        {
            var uri = request.RequestUri;
            if (uri is null || !uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps ||
                uri.Port != 443 || !string.Equals(uri.IdnHost, vault.IdnHost, StringComparison.OrdinalIgnoreCase) ||
                uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
                !(uri.AbsolutePath == "/secrets" || uri.AbsolutePath.StartsWith("/secrets/", StringComparison.Ordinal) ||
                    uri.AbsolutePath == "/certificates" || uri.AbsolutePath.StartsWith("/certificates/", StringComparison.Ordinal) ||
                    request.Method == HttpMethod.Post && Regex.IsMatch(uri.AbsolutePath,
                        "^/deleted(?:secrets|certificates)/[a-zA-Z0-9-]{1,127}/recover$", RegexOptions.CultureInvariant)))
                throw new ArgumentException("The request must target the selected vault's secrets or certificates endpoint.");

            request.MarkSensitive();
            var previousOverride = AuthHandler.AcquireTokenAsyncOverride;
            bool previousApplyOverride = AuthHandler.ApplyOverride;
            AuthHandler.AcquireTokenAsyncOverride = (_, tenant, prompt, _, profile) =>
                acquireToken(["https://vault.azure.net/.default"], tenant, prompt, callback.Token, profile);
            AuthHandler.ApplyOverride = true;
            try
            {
                var response = await next.SendAsync(request, callback);
                response.RequestMessage ??= request;
                response.RequestMessage.MarkSensitive();
                return response;
            }
            catch (HttpRequestException)
            {
                throw new HttpRequestException("Key Vault request failed. Check network connectivity and the selected profile. Sensitive details are omitted.");
            }
            finally
            {
                AuthHandler.AcquireTokenAsyncOverride = previousOverride;
                AuthHandler.ApplyOverride = previousApplyOverride;
            }
        });
        dataPlanePrepared = true;
    }

    private static void ValidateEndpoint(Uri? endpoint)
    {
        const string suffix = ".vault.azure.net";
        if (endpoint is null || !endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps ||
            endpoint.Port != 443 || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 ||
            endpoint.Fragment.Length != 0 || endpoint.AbsolutePath != "/" ||
            !endpoint.IdnHost.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use the root HTTPS URL of an Azure public-cloud vault.");
        ValidateVaultName(endpoint.IdnHost[..^suffix.Length]);
    }

    private static void ValidateVaultName(string name)
    {
        if (!Regex.IsMatch(name, "^[a-zA-Z][a-zA-Z0-9-]{1,22}[a-zA-Z0-9]$", RegexOptions.CultureInvariant) || name.Contains("--", StringComparison.Ordinal))
            throw new ArgumentException("Vault names must be 3-24 letters, digits or hyphens, start with a letter, end with a letter or digit and contain no consecutive hyphens.");
    }
}