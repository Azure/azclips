// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Azclips.Extensions.Resources;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

/// <summary>
/// Template parameters as PowerShell dynamic parameters, like New-AzTenantDeployment's TemplateUtility.GetDynamicParameters.
/// Only local templates (-TemplateFile .json/.bicep, -TemplateObject, or the template a .bicepparam file's 'using' names)
/// are read; any read failure yields no dynamic parameters.
/// </summary>
internal static class TenantDeploymentTemplateParameters
{
    // A template parameter whose name matches a cmdlet parameter is exposed with this suffix.
    private const string DuplicatedParameterSuffix = "FromTemplate";

    private static readonly JsonDocumentOptions JsoncOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static RuntimeDefinedParameterDictionary Create(Type cmdletType, string? templateFile, Hashtable? templateObject, string? templateJson, ISet<string>? suppliedNames)
    {
        RuntimeDefinedParameterDictionary dynamicParameters = new();
        if (ReadTemplate(templateFile, templateObject, templateJson) is not { } template
            || template["parameters"] is not JsonObject declared)
        {
            return dynamicParameters;
        }

        JsonObject? definitions = template["definitions"] as JsonObject;
        HashSet<string> staticNames = GetStaticParameterNames(cmdletType);
        foreach (KeyValuePair<string, JsonNode?> parameter in declared)
        {
            if (parameter.Value is not JsonObject definition)
            {
                continue;
            }

            bool hasDefault = definition.ContainsKey("defaultValue");
            ResolvedParameter resolved = ResolveParameter(definition, definitions);
            RuntimeDefinedParameter runtimeParameter = new()
            {
                Name = staticNames.Contains(parameter.Key) ? parameter.Key + DuplicatedParameterSuffix : parameter.Key,
                ParameterType = GetParameterType(resolved.Type),
            };
            runtimeParameter.Attributes.Add(new ParameterAttribute
            {
                // A value from the parameters file or -Parameter also satisfies the template, so PowerShell does not prompt for it.
                Mandatory = !hasDefault && resolved.Nullable != true && suppliedNames is not null && !suppliedNames.Contains(parameter.Key),
                ValueFromPipelineByPropertyName = true,
                // Keeps the template's parameter name for FromTemplate-suffixed parameters.
                HelpMessage = parameter.Key,
            });
            if (resolved.MinLength is int min && resolved.MaxLength is int max)
            {
                runtimeParameter.Attributes.Add(new ValidateLengthAttribute(min, max));
            }
            dynamicParameters.Add(runtimeParameter.Name, runtimeParameter);
        }
        return dynamicParameters;
    }

    // Names of the parameters already set by a parameters file or -Parameter. Sources whose values are not visible
    // here (-ParametersUri, a parameters URL, a .bicepparam passed through -Parameter) return null, meaning every
    // template parameter may already be set. Like Az PowerShell, a .bicepparam parameters file is compiled for its
    // values and for the template its 'using' declaration names (null when that is a Template Spec).
    public static ISet<string>? GetSuppliedNames(string? parametersFile, string? parametersUri, string[]? parameterTokens, out string? bicepParamTemplateJson)
    {
        bicepParamTemplateJson = null;
        if (!string.IsNullOrWhiteSpace(parametersUri))
        {
            return null;
        }

        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        if (IsBicepParam(parametersFile))
        {
            JsonObject? envelope = ReadJsonObject(BicepCompiler.BuildParams(parametersFile!));
            bicepParamTemplateJson = GetString(envelope?["templateJson"]);
            AddObjectKeys(names, ReadJsonObject(GetString(envelope?["parametersJson"]) ?? "{}"));
        }
        else if (!string.IsNullOrWhiteSpace(parametersFile))
        {
            AddObjectKeys(names, ReadJsonObject(File.ReadAllText(parametersFile)));
        }
        foreach (string token in (parameterTokens ?? []).Select(t => t.Trim()).Where(t => t.Length > 0))
        {
            if (IsBicepParam(token) || (Uri.TryCreate(token, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
            {
                return null;
            }
            if (token[0] == '{')
            {
                AddObjectKeys(names, ReadJsonObject(token));
            }
            else if (token[0] == '@' || File.Exists(token))
            {
                AddObjectKeys(names, ReadJsonObject(File.ReadAllText(token.TrimStart('@'))));
            }
            else if (token.IndexOf('=') is > 0 and int index)
            {
                names.Add(token[..index].Trim());
            }
        }
        return names;
    }

    // One -Parameter JSON token with the bound dynamic parameter values; it is applied last, so it overrides other sources.
    public static string ToParameterToken(IEnumerable<KeyValuePair<string, object?>> values)
    {
        JsonObject parameters = new();
        foreach ((string name, object? value) in values)
        {
            parameters[name] = new JsonObject { ["value"] = JsonSerializer.SerializeToNode(ToJsonValue(value)) };
        }
        return parameters.ToJsonString();
    }

    // Like Az PowerShell's ParameterUtility.AddTemplateObjectParametersToHashtable: each -TemplateParameterObject value is
    // wrapped as { "value": ... }, except a hashtable with a "reference" key (a Key Vault reference), which is passed as is.
    public static string ToParameterToken(Hashtable templateParameterObject)
    {
        JsonObject parameters = new();
        foreach (DictionaryEntry entry in templateParameterObject)
        {
            object? value = entry.Value is PSObject psObject ? psObject.BaseObject : entry.Value;
            JsonNode? node = JsonSerializer.SerializeToNode(ToJsonValue(value));
            parameters[entry.Key.ToString()!] = value is Hashtable reference && reference.ContainsKey("reference") ? node : new JsonObject { ["value"] = node };
        }
        return parameters.ToJsonString();
    }

    private static JsonObject? ReadTemplate(string? templateFile, Hashtable? templateObject, string? templateJson)
    {
        string? content = templateObject is not null
            ? JsonSerializer.Serialize(ToJsonValue(templateObject))
            : templateJson ?? (string.IsNullOrWhiteSpace(templateFile) || !File.Exists(templateFile)
                ? null
                : templateFile.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase)
                    ? BicepCompiler.BuildTemplate(templateFile)
                    : File.ReadAllText(templateFile));
        return content is null ? null : ReadJsonObject(content);
    }

    private sealed record ResolvedParameter(string? Type, bool? Nullable, int? MinLength, int? MaxLength);

    // Like Az PowerShell's TemplateUtility.ResolveParameterType: follows '#/definitions/...' $ref chains to the declared
    // type; nullable, minLength and maxLength set at a higher level win. An invalid or cyclic $ref throws.
    private static ResolvedParameter ResolveParameter(JsonObject definition, JsonObject? definitions)
    {
        bool? nullable = null;
        int? minLength = null;
        int? maxLength = null;
        HashSet<string> visited = new(StringComparer.Ordinal);
        JsonObject current = definition;
        while (true)
        {
            nullable ??= current["nullable"]?.GetValueKind() switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
            minLength ??= current["minLength"] is JsonValue min ? min.GetValue<int>() : null;
            maxLength ??= current["maxLength"] is JsonValue max ? max.GetValue<int>() : null;
            if (GetString(current["$ref"]) is not { } reference)
            {
                return new ResolvedParameter(GetString(current["type"]), nullable, minLength, maxLength);
            }
            if (!visited.Add(reference))
            {
                throw new InvalidOperationException($"Cycle detected with processing {reference}.");
            }
            current = ResolveDefinition(reference, definitions);
        }
    }

    private static JsonObject ResolveDefinition(string reference, JsonObject? definitions)
    {
        string[] segments = reference.Split('/').Select(s => Uri.UnescapeDataString(s.Replace("~1", "/").Replace("~0", "~"))).ToArray();
        if (segments.Length < 2 || segments[0] != "#" || segments[1] != "definitions")
        {
            throw new InvalidOperationException($"Invalid $ref {reference}.");
        }

        JsonNode? current = definitions;
        foreach (string segment in segments[2..])
        {
            current = current switch
            {
                JsonObject obj when obj.ContainsKey(segment) => obj[segment],
                JsonArray array when int.TryParse(segment, out int index) && index >= 0 && index < array.Count => array[index],
                _ => throw new InvalidOperationException($"Failed to resolve path {reference}."),
            };
        }
        return current as JsonObject ?? throw new InvalidOperationException($"Failed to find valid definition at path {reference}.");
    }

    private static JsonObject? ReadJsonObject(string json) => JsonNode.Parse(json, documentOptions: JsoncOptions) as JsonObject;

    private static string? GetString(JsonNode? node) => node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : null;

    // ARM parameters files wrap values as { "parameters": { ... } }; an inline object may not.
    private static void AddObjectKeys(HashSet<string> names, JsonObject? root)
    {
        foreach (string key in ((root?["parameters"] as JsonObject) ?? root)?.Select(p => p.Key) ?? [])
        {
            names.Add(key);
        }
    }

    // Like Az PowerShell, a Bicep CLI too old to compile the .bicepparam parameters file fails with its upgrade message.
    public static void EnsureBicepParamVersion(string? parametersFile)
    {
        if (IsBicepParam(parametersFile))
        {
            BicepCompiler.EnsureBicepParamVersion(withOverrides: false);
        }
    }

    private static bool IsBicepParam(string? path) => path?.Trim().EndsWith(".bicepparam", StringComparison.OrdinalIgnoreCase) == true;

    private static HashSet<string> GetStaticParameterNames(Type cmdletType)
    {
        HashSet<string> names = new(Cmdlet.CommonParameters.Concat(Cmdlet.OptionalCommonParameters), StringComparer.OrdinalIgnoreCase);
        foreach (PropertyInfo property in cmdletType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.IsDefined(typeof(ParameterAttribute), true))
            {
                names.Add(property.Name);
            }
        }
        return names;
    }

    private static Type GetParameterType(string? type) => type?.ToLowerInvariant() switch
    {
        "string" => typeof(string),
        "int" => typeof(int),
        "bool" => typeof(bool),
        "securestring" => typeof(SecureString),
        "object" or "secureobject" => typeof(Hashtable),
        "array" => typeof(object[]),
        _ => typeof(object),
    };

    private static object? ToJsonValue(object? value) => value switch
    {
        PSObject psObject => ToJsonValue(psObject.BaseObject),
        SecureString secure => new NetworkCredential(string.Empty, secure).Password,
        IDictionary dictionary => dictionary.Keys.Cast<object>().ToDictionary(k => k.ToString()!, k => ToJsonValue(dictionary[k])),
        string => value,
        IEnumerable items => items.Cast<object?>().Select(ToJsonValue).ToList(),
        _ => value,
    };
}
