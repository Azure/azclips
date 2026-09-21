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
        return table;

        void Add<T>(string verb, string psName, bool hasName = false, bool version = false, bool attributes = false, bool page = false)
        {
            var parameters = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);
            Parameter("VaultName", "vault-name", "VaultName", typeof(string), "Azure public-cloud vault name.");
            Parameter("VaultUrl", "vault-url", "VaultUrl", typeof(string), "Root HTTPS vault URL. Use instead of vault name.");
            Parameter("Profile", "profile", "Profile", typeof(string), "Use the named connection profile.");
            if (hasName) Parameter("SecretName", "name", "Name", typeof(string), "Secret name.", true);
            if (version) Parameter("SecretVersion", "version", "Version", typeof(string), "Secret version. Omit for latest.");
            if (page) Parameter("PageSize", "maxresults", "MaxResults", typeof(int), "Page size from 1 to 25. All pages are returned.");
            if (attributes)
            {
                Parameter("Enabled", "enabled", "Enabled", typeof(string), "Whether the secret is enabled: true or false.");
                Parameter("Expires", "expires", "Expires", typeof(string), "Expiration timestamp in UTC.");
                Parameter("NotBefore", "not-before", "NotBefore", typeof(string), "Not-before timestamp in UTC.");
                Parameter("ContentType", "content-type", "ContentType", typeof(string), "Secret content type.");
                Parameter("TagsInput", "tags", "Tag", typeof(string[]), "Tags in NAME=VALUE form.");
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
            if (verb == "show") Parameter("AsPlainText", "as-plain-text", "AsPlainText", typeof(bool), "Explicitly return the plaintext secret value.");
            if (verb == "delete") Parameter("Yes", "yes", "Yes", typeof(bool), "Confirm deletion of all secret versions.");
            string invocation = cli ? $"keyvault secret {verb}" : psName;
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