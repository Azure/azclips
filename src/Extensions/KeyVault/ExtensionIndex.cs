using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;

namespace Microsoft.Azure.Azclips.Extensions.KeyVault;

[ExtensionVerb("keyvault")]
public class ExtensionIndex : IExtensionIndex
{
    private static readonly IReadOnlyDictionary<string, CommandMetadata> s_cliTable = BuildTable(true);
    private static readonly IReadOnlyDictionary<string, CommandMetadata> s_psTable = BuildTable(false);

    private static IReadOnlyDictionary<string, CommandMetadata> BuildTable(bool cli)
    {
        var table = new Dictionary<string, CommandMetadata>(StringComparer.OrdinalIgnoreCase);
        Add<SetSecretCommand>("set", "Set-AzKeyVaultSecret", hasName: true, attributes: true);
        Add<ShowSecretCommand>("show", "Get-AzKeyVaultSecret_Get", hasName: true, version: true);
        Add<ListSecretsCommand>("list", "Get-AzKeyVaultSecret_List", page: true);
        Add<ListSecretVersionsCommand>("list-versions", "Get-AzKeyVaultSecret_Versions", hasName: true, page: true);
        Add<UpdateSecretCommand>("set-attributes", "Update-AzKeyVaultSecret", hasName: true, version: true, attributes: true);
        Add<RemoveSecretCommand>("delete", "Remove-AzKeyVaultSecret", hasName: true);
        Add<CreateCertificateCommand>("create", "Add-AzKeyVaultCertificate", hasName: true, attributes: true, certificate: true);
        Add<ImportCertificateCommand>("import", "Import-AzKeyVaultCertificate", hasName: true, attributes: true, certificate: true);
        Add<ShowCertificateCommand>("show", "Get-AzKeyVaultCertificate_Get", hasName: true, version: true, certificate: true);
        Add<ListCertificatesCommand>("list", "Get-AzKeyVaultCertificate_List", page: true, certificate: true);
        Add<ListCertificateVersionsCommand>("list-versions", "Get-AzKeyVaultCertificate_Versions", hasName: true, page: true, certificate: true);
        Add<UpdateCertificateCommand>("set-attributes", "Update-AzKeyVaultCertificate", hasName: true, version: true, attributes: true, certificate: true);
        Add<DownloadCertificateCommand>("download", "Export-AzKeyVaultCertificate", hasName: true, version: true, certificate: true);
        Add<RemoveCertificateCommand>("delete", "Remove-AzKeyVaultCertificate", hasName: true, certificate: true);
        if (!cli) Add<RecoverCertificateCommand>("recover", "Undo-AzKeyVaultCertificateRemoval", hasName: true, certificate: true);
        return table;

        void Add<T>(string verb, string psName, bool hasName = false, bool version = false, bool attributes = false, bool page = false, bool certificate = false)
        {
            string entity = certificate ? "certificate" : "secret";
            var parameters = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);
            Parameter("VaultName", "vault-name", "VaultName", typeof(string), "Azure public-cloud vault name.");
            Parameter("VaultUrl", "vault-url", "VaultUrl", typeof(string), "Root HTTPS vault URL. Use instead of vault name.");
            Parameter("Profile", "profile", "Profile", typeof(string), "Use the named connection profile.");
            if (hasName) Parameter(certificate ? "CertificateName" : "SecretName", "name", "Name", typeof(string), $"{entity} name.", true);
            if (version) Parameter(certificate ? "CertificateVersion" : "SecretVersion", "version", "Version", typeof(string), $"{entity} version. Omit for latest.");
            if (page) Parameter("PageSize", "maxresults", "MaxResults", typeof(int), "Page size from 1 to 25. All pages are returned.");
            if (attributes)
            {
                Parameter("Enabled", "enabled", "Enabled", typeof(string), $"Whether the {entity} is enabled: true or false.");
                Parameter("Expires", "expires", "Expires", typeof(string), "Expiration timestamp in UTC.");
                Parameter("NotBefore", "not-before", "NotBefore", typeof(string), "Not-before timestamp in UTC.");
                if (!certificate) Parameter("ContentType", "content-type", "ContentType", typeof(string), "Secret content type.");
                Parameter("TagsInput", "tags", "Tag", typeof(string[]), "Tags in NAME=VALUE form.");
                if (certificate)
                {
                    Parameter("Policy", "policy", "Policy", typeof(string), "Policy JSON using REST API field names.");
                    Parameter("PolicyFile", "policy-file", "PolicyPath", typeof(string), "UTF-8 JSON policy file. Use instead of policy JSON.");
                }
            }
            if (verb == "set")
            {
                if (cli)
                {
                    Parameter("Value", "value", "Value", typeof(string), "Secret value. Prefer --file to avoid shell history.");
                    Parameter("File", "file", "File", typeof(string), "UTF-8 secret file; contents are preserved including newlines.");
                }
                else Parameter("SecureValue", "secret-value", "SecretValue", typeof(System.Security.SecureString), "Secret value as SecureString.", true);
            }
            if (verb == "show" && !certificate) Parameter("AsPlainText", "as-plain-text", "AsPlainText", typeof(bool), "Explicitly return the plaintext secret value.");
            if (verb == "delete") Parameter("Yes", "yes", "Yes", typeof(bool), $"Confirm deletion of all {entity} versions.");
            if (certificate && verb == "create")
            {
                Parameter("NoWait", "no-wait", "NoWait", typeof(bool), "Return the creation operation without polling.");
                Parameter("TimeoutSeconds", "timeout", "TimeoutSeconds", typeof(int), "Creation timeout in seconds, from 1 to 3600. Default: 300.");
            }
            if (certificate && verb == "list") Parameter("IncludePendingInput", "include-pending", "IncludePending", typeof(bool), "Include certificates that are not completely provisioned.");
            if (certificate && verb is "import" or "download")
                Parameter("File", "file", "FilePath", typeof(string), verb == "import" ? "PFX or PEM file containing a private key." : "New destination for the public certificate.", true);
            if (certificate && verb == "import")
            {
                if (cli) Parameter("PasswordFile", "password-file", "PasswordFile", typeof(string), "UTF-8 password file. Contents, including newlines, are preserved.");
                else Parameter("SecurePassword", "password", "Password", typeof(System.Security.SecureString), "Certificate password as SecureString.");
            }
            if (certificate && verb == "download") Parameter("Encoding", "encoding", "Encoding", typeof(string), "Public certificate encoding: PEM (default) or DER.");
            string invocation = cli ? $"keyvault {entity} {verb}" : psName;
            table.Add(invocation, new CommandMetadata(
                Name: typeof(T).Name, InvocationName: invocation, Aliases: [],
                FullName: typeof(T).FullName!, AssemblyName: typeof(T).Assembly.GetName().Name!,
                ProcessorType: typeof(T), Parameters: parameters, ParameterAliases: new Dictionary<string, string>()));

            void Parameter(string property, string cliName, string psParameter, Type type, string help, bool required = false)
            {
                string name = cli ? cliName : psParameter;
                parameters.Add(name, new ParameterMetadata(property, name, type, help, IsRequired: required, Aliases: [], AllowedValues: []));
            }
        }
    }

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
    {
        var table = GetCommandTable(clientType);
        return table.TryGetValue(name, out var metadata)
            ? metadata
            : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
    }

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) => clientType switch
    {
        ClientType.CLI => s_cliTable,
        ClientType.PowerShell => s_psTable,
        _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
    };

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => GetCommandTable(clientType).Values;

    public IEnumerable<string> GetCommandKeys(ClientType clientType) => GetCommandTable(clientType).Keys;
}