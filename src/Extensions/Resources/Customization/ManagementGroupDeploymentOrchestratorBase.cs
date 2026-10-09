// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Shared input surface for management-group deployment create/validate/what-if. Template and
/// parameter building is inherited from <see cref="DeploymentOrchestratorBase{TRequest, TResponse}"/>.
/// </summary>
public abstract class ManagementGroupDeploymentOrchestratorBase<TRequest, TResponse>
    : DeploymentOrchestratorBase<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    public string ManagementGroupId { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    protected override string DeploymentScope => "managementGroup";

    private static readonly Regex s_templateSchema = new(
        "https?://schema\\.management\\.azure\\.com/schemas/[0-9a-zA-Z-]+/(?<templateType>[a-zA-Z]+)Template\\.json#?", RegexOptions.Compiled);

    // Legacy CLI validate_bicep_target_scope (also applied to PS here): the compiled template's $schema must be a
    // management group template; a missing $schema reports "None".
    private static void ValidateBicepTargetScope(JsonElement template)
    {
        string? schema = template.ValueKind == JsonValueKind.Object
            && template.TryGetProperty("$schema", out JsonElement s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        Match match = s_templateSchema.Match(schema ?? string.Empty);
        string? targetScope = match.Success ? match.Groups["templateType"].Value.ToLowerInvariant() switch
        {
            "deployment" => "resourceGroup",
            "subscriptiondeployment" => "subscription",
            "managementgroupdeployment" => "managementGroup",
            "tenantdeployment" => "tenant",
            _ => null,
        } : null;
        if (targetScope != "managementGroup")
        {
            throw new ArgumentException($"The target scope \"{targetScope ?? "None"}\" does not match the deployment scope \"managementGroup\".");
        }
    }

    // The shared base reads files strictly (no comments) and wraps -TemplateParameterObject differently, so MG reads
    // local template/parameter files and wraps the object itself, like legacy, before handing them to the base.
    private void InlineLegacyInputs()
    {
        if (!string.IsNullOrWhiteSpace(TemplateFile) && TemplateObject is null)
        {
            TemplateObject = ReadTemplateFile(TemplateFile);
            TemplateFile = null;
        }
        if (ParametersObject is IDictionary entries)
        {
            // Legacy PS AddTemplateObjectParametersToHashtable: each value becomes { "value": v }; Key Vault { "reference" } passes through.
            JsonObject wrapped = new();
            foreach (DictionaryEntry entry in entries)
            {
                JsonNode? value = JsonSerializer.SerializeToNode(entry.Value);
                wrapped[entry.Key.ToString()!] = value is JsonObject reference && reference.ContainsKey("reference")
                    ? value
                    : new JsonObject { ["value"] = value };
            }
            ParametersObject = wrapped;
        }
        else if (ParametersObject is null && !string.IsNullOrWhiteSpace(ParametersFile) && !IsBicepParamFile(ParametersFile))
        {
            EnsureFileExists(ParametersFile);
            ParametersObject = UnwrapParameters(JsonNode.Parse(File.ReadAllText(ParametersFile), documentOptions: s_jsonWithComments));
            ParametersFile = null;
        }
    }

    protected override void ValidateScopeInputs()
    {
        if (string.IsNullOrWhiteSpace(ManagementGroupId))
        {
            throw new ArgumentException("ManagementGroupId must be provided.");
        }
        if (string.IsNullOrWhiteSpace(Location))
        {
            throw new ArgumentException("Location must be provided.");
        }
    }

    // CLI-only legacy `-p/--parameters` items: one .bicepparam, a file, @file, JSON, URI or KEY=VALUE. PS never sets it.
    public string[]? CliParameters { get; set; }

    // CLI-only `--query-string`: legacy allows it only together with --template-uri.
    public string? CliQueryString { get; set; }

    // CLI-only legacy `--no-prompt`: warn about missing template parameters instead of asking for them.
    public bool NoPrompt { get; set; }

    // Legacy az CLI asks for missing template parameters; the PS commands leave that to PowerShell.
    protected virtual bool PromptsForMissingParameters => false;

    private JsonObject? _cliTemplate;

    // CLI parameters before missing ones were asked for; legacy create with -c asks again before deploying.
    private JsonObject? _unpromptedParameters;

    private const string TemplateSpecsApiVersion = "2022-02-01";

    // Plain client like legacy urlopen: template/parameter URIs are public or SAS links, never sent an ARM token.
    private static readonly HttpClient s_download = new();

    private static readonly JsonDocumentOptions s_jsonWithComments = new() { CommentHandling = JsonCommentHandling.Skip };

    // Always Incremental: validate/what-if have no mode in legacy; only CLI create sets Mode afterwards.
    protected async Task<DeploymentProperties> BuildPropertiesAsync()
    {
        if (!string.IsNullOrWhiteSpace(CliQueryString))
        {
            if (string.IsNullOrWhiteSpace(TemplateUri))
            {
                throw new ArgumentException("please provide --template-uri if --query-string is specified");
            }
            QueryString = CliQueryString;
        }

        JsonObject? cliParameters = null;
        if (CliParameters is { Length: > 0 })
        {
            cliParameters = await ResolveCliParametersAsync(CliParameters);
        }
        else if (!string.IsNullOrWhiteSpace(ParametersFile) && IsBicepParamFile(ParametersFile))
        {
            UsePsBicepParamTemplate(ParametersFile);
        }
        if (PromptsForMissingParameters && !(CliParameters ?? []).Any(IsBicepParamFile))
        {
            cliParameters ??= new JsonObject();
            _unpromptedParameters = (JsonObject)cliParameters.DeepClone();
            AddMissingParameters(cliParameters, await GetCliTemplateAsync());
        }

        ValidateCommonInputs();
        InlineLegacyInputs();
        ParametersLink? parametersLink = null;
        return new DeploymentProperties
        {
            Mode = DeploymentMode.Incremental,
            Template = BuildTemplate(out TemplateLink? templateLink),
            TemplateLink = templateLink,
            Parameters = cliParameters is null ? BuildParameters(out parametersLink) : JsonSerializer.SerializeToElement(cliParameters),
            ParametersLink = parametersLink,
            ValidationLevel = NormalizeValidationLevel(ValidationLevel),
        };
    }

    // Legacy CLI _process_parameters: per item try file, JSON, URI, then KEY=VALUE typed by the template; later items win.
    private async Task<JsonObject> ResolveCliParametersAsync(string[] items)
    {
        string[] bicepParams = items.Where(IsBicepParamFile).ToArray();
        if (bicepParams.Length > 1)
        {
            throw new ArgumentException("Only one .bicepparam file can be provided with --parameters argument");
        }
        if (bicepParams.Length == 1)
        {
            return await ResolveCliBicepParamAsync(bicepParams[0], items);
        }

        JsonObject parameters = new();
        JsonObject? template = null;
        foreach (string raw in items)
        {
            // Legacy CLI expands '@file' arguments to the file content.
            string item = raw.StartsWith('@') && File.Exists(raw[1..]) ? File.ReadAllText(raw[1..]) : raw;
            JsonObject? source = TryLoadParametersFile(item) ?? TryParseParametersJson(item) ?? await TryLoadParametersUriAsync(item);
            if (source is not null)
            {
                foreach (KeyValuePair<string, JsonNode?> entry in source.ToList())
                {
                    parameters[entry.Key] = entry.Value?.DeepClone();
                }
                continue;
            }
            // ponytail: the template is loaded only when a KEY=VALUE item needs its parameter types.
            template ??= await GetCliTemplateAsync();
            if (!await TryParseKeyValueAsync(parameters, template, item))
            {
                throw new ArgumentException($"Unable to parse parameter: {item}");
            }
        }
        return parameters;
    }

    // Legacy CLI _parse_bicepparam_file + the bicepparam branch of _prepare_deployment_properties_unmodified.
    private async Task<JsonObject> ResolveCliBicepParamAsync(string bicepParam, string[] items)
    {
        if (!string.IsNullOrWhiteSpace(TemplateUri) || !string.IsNullOrWhiteSpace(TemplateSpecId))
        {
            throw new ArgumentException("A .bicepparam file can only be combined with --template-file.");
        }
        string? templateFile = string.IsNullOrWhiteSpace(TemplateFile) ? null : TemplateFile;
        bool usingNone = IsUsingNoneBicepParamFile(bicepParam);
        if (!usingNone && templateFile is not null && !IsBicepFile(templateFile))
        {
            throw new ArgumentException("Only a .bicep template is allowed with a .bicepparam file");
        }
        if (usingNone && templateFile is null)
        {
            throw new ArgumentException("The .bicepparam file uses 'using none', so a --template-file (-f) must be provided.");
        }

        (string? templateJson, string? templateSpecId, string parametersJson) = BuildBicepParams(bicepParam, usingNone ? null : templateFile, null);
        if (usingNone)
        {
            EnsureFileExists(templateFile!);
            templateJson = IsBicepFile(templateFile!) ? BicepCompiler.BuildTemplate(templateFile!) : File.ReadAllText(templateFile!);
            templateSpecId = null;
        }

        if (items.Length > 1)
        {
            JsonObject template = templateSpecId is not null ? await LoadTemplateSpecTemplateAsync(templateSpecId) : ParseTemplate(templateJson!);
            JsonObject inline = new();
            foreach (string item in items.Where(i => !IsBicepParamFile(i)))
            {
                if (!await TryParseKeyValueAsync(inline, template, item))
                {
                    throw new ArgumentException($"Unable to parse parameter: {item}. Only correctly formatted in-line parameters are allowed with a .bicepparam file");
                }
            }
            JsonObject overrides = new(inline.ToList().Select(p => KeyValuePair.Create(p.Key, p.Value?["value"]?.DeepClone())));
            (templateJson, templateSpecId, parametersJson) = BuildBicepParams(bicepParam, templateFile, overrides.Count == 0 ? null : overrides.ToJsonString());
        }

        JsonObject templateObject;
        if (templateSpecId is not null)
        {
            templateObject = await LoadTemplateSpecTemplateAsync(templateSpecId);
            TemplateSpecId = templateSpecId;
        }
        else
        {
            templateObject = !string.IsNullOrEmpty(templateJson) ? ParseTemplate(templateJson) : ReadTemplateFile(templateFile!);
            TemplateObject = templateObject;
        }
        TemplateFile = null;
        ValidateBicepTargetScope(JsonSerializer.SerializeToElement(templateObject));

        return JsonNode.Parse(parametersJson)?["parameters"]?.DeepClone() as JsonObject ?? new JsonObject();
    }

    // Legacy PS: a .bicepparam supplies the template (its 'using' target) when no template is given.
    private void UsePsBicepParamTemplate(string bicepParam)
    {
        if (!string.IsNullOrWhiteSpace(TemplateUri) || !string.IsNullOrWhiteSpace(TemplateSpecId) || TemplateObject is not null)
        {
            throw new ArgumentException("Parameters -TemplateUri, -TemplateSpecId or -TemplateObject cannot be used if a .bicepparam file is supplied with parameter -TemplateParameterFile.");
        }
        if (!string.IsNullOrWhiteSpace(TemplateFile))
        {
            if (!IsBicepFile(TemplateFile))
            {
                throw new ArgumentException("Parameter -TemplateFile only permits .bicep files if a .bicepparam file is supplied with parameter -TemplateParameterFile.");
            }
            return;
        }
        EnsureFileExists(bicepParam);
        (string? templateJson, string? templateSpecId, _) = BuildBicepParams(bicepParam, null, null);
        if (!string.IsNullOrEmpty(templateJson))
        {
            TemplateObject = ParseTemplate(templateJson);
        }
        else
        {
            TemplateSpecId = templateSpecId ?? throw new ArgumentException($"Unable to get a template from '{bicepParam}'.");
        }
    }

    private static (string? TemplateJson, string? TemplateSpecId, string ParametersJson) BuildBicepParams(string bicepParam, string? bicepFile, string? overridesJson)
    {
        using JsonDocument output = JsonDocument.Parse(BicepCompiler.BuildParams(bicepParam, bicepFile, overridesJson));
        string? Read(string name) =>
            output.RootElement.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return (Read("templateJson"), Read("templateSpecId"),
            Read("parametersJson") ?? throw new InvalidOperationException("Unexpected 'bicep build-params --stdout' output: missing 'parametersJson' string."));
    }

    // Legacy CLI is_using_none_bicepparam_file: the first non-comment statement is 'using none'.
    private static bool IsUsingNoneBicepParamFile(string path)
    {
        string content;
        try
        {
            content = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        content = Regex.Replace(content, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        content = Regex.Replace(content, "//.*", string.Empty);
        string? first = content.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
        return first is not null && Regex.IsMatch(first, @"^using\s+none$", RegexOptions.IgnoreCase);
    }

    private static JsonObject? TryLoadParametersFile(string item)
    {
        if (!File.Exists(item))
        {
            return null;
        }
        string content = File.ReadAllText(item);
        if (content.Length == 0)
        {
            return null;
        }
        try
        {
            return UnwrapParameters(JsonNode.Parse(content, documentOptions: s_jsonWithComments));
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse {item} with exception:\n    {ex.Message}");
        }
    }

    private static JsonObject? TryParseParametersJson(string item)
    {
        try
        {
            return UnwrapParameters(JsonNode.Parse(item, documentOptions: s_jsonWithComments));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<JsonObject?> TryLoadParametersUriAsync(string item)
    {
        if (!item.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }
        try
        {
            return UnwrapParameters(JsonNode.Parse(await s_download.GetStringAsync(item, Publisher.Token), documentOptions: s_jsonWithComments));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    // Legacy `parsed.get('parameters', parsed)`; non-object values are not a parameter source.
    private static JsonObject UnwrapParameters(JsonNode? node)
    {
        JsonObject root = node as JsonObject ?? throw new ArgumentException("Parameters must be a JSON object.");
        return root.ContainsKey("parameters")
            ? root["parameters"] as JsonObject ?? throw new ArgumentException("'parameters' must be a JSON object.")
            : root;
    }

    // Loaded once: a Bicep file is compiled on the first load and then cleared.
    private async Task<JsonObject> GetCliTemplateAsync() => _cliTemplate ??= await LoadCliTemplateAsync();

    // Legacy CLI create with --confirm-with-what-if builds the deployment again after the what-if, so it asks again.
    protected async Task AskMissingParametersAgainAsync(DeploymentProperties properties)
    {
        if (_unpromptedParameters is not null)
        {
            JsonObject parameters = (JsonObject)_unpromptedParameters.DeepClone();
            AddMissingParameters(parameters, await GetCliTemplateAsync());
            properties.Parameters = JsonSerializer.SerializeToElement(parameters);
        }
    }

    // Events.Warning is logged at Info by the engine (hidden by default), so warn through the session logger:
    // PS WriteWarning, CLI "WARNING:" on stderr, like legacy.
    protected static void Warn(string message) => AzclipsServiceProvider.GetRequiredService<ILoggingService>().Warn(message);

    protected static void Verbose(string message) => AzclipsServiceProvider.GetRequiredService<ILoggingService>().Verbose(message);

    // Legacy CLI _find_missing_parameters + _get_missing_parameters + _prompt_for_parameters.
    private void AddMissingParameters(JsonObject parameters, JsonObject template)
    {
        List<(string Name, JsonObject Parameter)> missing = [];
        foreach ((string name, JsonNode? node) in template["parameters"] as JsonObject ?? new JsonObject())
        {
            if (node is JsonObject parameter && !parameter.ContainsKey("defaultValue") && parameters[name] is null
                && !(ResolveParameterType(parameter, template)["nullable"] is JsonValue nullable && nullable.TryGetValue(out bool isNullable) && isNullable))
            {
                missing.Add((name, parameter));
            }
        }
        if (missing.Count == 0)
        {
            return;
        }
        string names = string.Join(", ", missing.Select(m => m.Name).Order(StringComparer.Ordinal));
        if (NoPrompt)
        {
            Warn($"Missing input parameters: {names} ");
            return;
        }
        if (Console.IsInputRedirected)
        {
            throw new ArgumentException($"Missing input parameters: {names}");
        }
        foreach ((string name, JsonObject parameter) in missing)
        {
            parameters[name] = new JsonObject { ["value"] = PromptForParameter(name, parameter) };
        }
    }

    private static JsonNode? PromptForParameter(string name, JsonObject parameter)
    {
        string type = parameter["type"] is JsonValue t && t.TryGetValue(out string? s) ? s.ToLowerInvariant() : "string";
        string help = parameter["metadata"]?["description"]?.ToString() ?? "Missing description";
        string message = $"Please provide {type} value for '{name}' (? for help): ";

        if (parameter["allowedValues"] is JsonArray allowed)
        {
            int choice = CliPrompt.Choice(message, allowed.Select(v => v?.ToString() ?? "None").ToList(), help, Warn);
            return allowed[choice]?.DeepClone();
        }
        switch (type)
        {
            case "securestring":
                return JsonValue.Create(CliPrompt.Text(message, help, secret: true));
            case "int":
                return JsonValue.Create(CliPrompt.Int(message, help, Warn));
            case "bool":
                return JsonValue.Create(CliPrompt.Bool(message, "t", "f", help));
            case "object" or "array":
                while (true)
                {
                    string value = CliPrompt.Text(message, help);
                    if (value.Length == 0)
                    {
                        return type == "object" ? new JsonObject() : new JsonArray();
                    }
                    try
                    {
                        return JsonNode.Parse(value);
                    }
                    catch (JsonException ex)
                    {
                        Console.Error.WriteLine($"ERROR: {ex.Message}");
                    }
                }
            default:
                return JsonValue.Create(CliPrompt.Text(message, help));
        }
    }

    // Legacy CLI template_obj source: template URI, template spec, or template file (Bicep compiled).
    private async Task<JsonObject> LoadCliTemplateAsync()
    {
        if (!string.IsNullOrWhiteSpace(TemplateUri))
        {
            string url = string.IsNullOrWhiteSpace(QueryString)
                ? TemplateUri
                : new UriBuilder(TemplateUri) { Query = NormalizeQueryString(QueryString) }.Uri.ToString();
            string content;
            try
            {
                content = await s_download.GetStringAsync(url, Publisher.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new ArgumentException($"Unable to retrieve url {url}");
            }
            return ParseTemplate(content);
        }
        if (!string.IsNullOrWhiteSpace(TemplateSpecId))
        {
            return await LoadTemplateSpecTemplateAsync(TemplateSpecId);
        }
        if (!string.IsNullOrWhiteSpace(TemplateFile))
        {
            JsonObject template = ReadTemplateFile(TemplateFile);
            if (IsBicepFile(TemplateFile))
            {
                // Reuse the compiled template for the request so Bicep runs only once, like legacy CLI.
                TemplateObject = template;
                TemplateFile = null;
            }
            return template;
        }
        throw new ArgumentException("Please enter one of the following: template file, template spec, template url, or Bicep parameters file.");
    }

    // Legacy CLI _load_template_spec_template: properties.mainTemplate of the template spec version.
    private async Task<JsonObject> LoadTemplateSpecTemplateAsync(string templateSpecId)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"https://management.azure.com{templateSpecId.Trim('"')}?api-version={TemplateSpecsApiVersion}");
        using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);
        await response.EnsureSuccessAsync(Publisher);
        JsonNode? body = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return body?["properties"]?["mainTemplate"]?.DeepClone() as JsonObject
            ?? throw new ArgumentException($"Template spec '{templateSpecId}' has no mainTemplate.");
    }

    private JsonObject ReadTemplateFile(string path)
    {
        EnsureFileExists(path);
        if (!IsBicepFile(path))
        {
            return ParseTemplate(File.ReadAllText(path));
        }
        JsonObject template = ParseTemplate(BicepCompiler.BuildTemplate(path));
        ValidateBicepTargetScope(JsonSerializer.SerializeToElement(template));
        return template;
    }

    private static JsonObject ParseTemplate(string json) =>
        JsonNode.Parse(json, documentOptions: s_jsonWithComments) as JsonObject
            ?? throw new ArgumentException("The template must be a JSON object.");

    // Legacy CLI _try_parse_key_value_object: KEY=VALUE typed by the template parameter's (resolved) type.
    private async Task<bool> TryParseKeyValueAsync(JsonObject parameters, JsonObject template, string item)
    {
        if (item == "{}" && parameters.Count == 0)
        {
            return true;
        }
        int separator = item.IndexOf('=');
        if (separator < 0)
        {
            return false;
        }
        string key = item[..separator];
        string value = item[(separator + 1)..];

        JsonObject? templateParameters = template["parameters"] as JsonObject;
        if (templateParameters?[key] is not JsonObject parameter)
        {
            IEnumerable<string> allowed = (templateParameters?.Select(p => p.Key) ?? []).Order(StringComparer.Ordinal);
            throw new ArgumentException($"unrecognized template parameter '{key}'. Allowed parameters: {string.Join(", ", allowed)}");
        }

        string? type = ResolveParameterType(parameter, template)["type"] is JsonValue t && t.TryGetValue(out string? s) ? s.ToLowerInvariant() : null;
        JsonNode? typed;
        switch (type)
        {
            case "object" or "array" or "secureobject":
                try
                {
                    typed = JsonNode.Parse(value);
                }
                catch (JsonException ex)
                {
                    throw new ArgumentException($"Failed to parse string as JSON:\n{value}\nError detail: {ex.Message}");
                }
                break;
            case "string" or "securestring":
                typed = JsonValue.Create(value);
                break;
            case "bool":
                typed = JsonValue.Create(value.Equals("true", StringComparison.OrdinalIgnoreCase));
                break;
            case "int":
                typed = long.TryParse(value.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number)
                    ? JsonValue.Create(number)
                    : throw new ArgumentException($"invalid literal for int() with base 10: '{value}'");
                break;
            default:
                Warn($"Unrecognized type '{type ?? "None"}' for parameter '{key}'. Interpretting as string.");
                typed = JsonValue.Create(value);
                break;
        }
        parameters[key] = new JsonObject { ["value"] = typed };
        return true;
    }

    // Legacy CLI _resolve_parameter_type: follows '$ref' JSON pointers into definitions/parameters/outputs.
    private static JsonObject ResolveParameterType(JsonObject parameter, JsonObject template)
    {
        JsonObject current = parameter;
        HashSet<string> visited = [];
        while (current["$ref"] is JsonValue reference && reference.TryGetValue(out string? referenced))
        {
            if (!visited.Add(referenced))
            {
                throw new ArgumentException($"Cycle detected with processing {referenced}.");
            }
            string[] segments = referenced.Split('/').Select(s => Uri.UnescapeDataString(s).Replace("~1", "/").Replace("~0", "~")).ToArray();
            if (segments.Length < 2 || segments[0] != "#" || segments[1] is not ("definitions" or "parameters" or "outputs"))
            {
                throw new ArgumentException($"Invalid $ref {referenced}.");
            }
            JsonNode? node = template[segments[1]];
            foreach (string segment in segments[2..])
            {
                node = node switch
                {
                    JsonObject o when o.ContainsKey(segment) => o[segment],
                    JsonArray a when segment.Length > 0 && segment.All(char.IsAsciiDigit) && int.TryParse(segment, out int i) && i < a.Count => a[i],
                    _ => throw new ArgumentException($"Failed to resolve path {referenced}."),
                };
            }
            JsonObject resolved = node?.DeepClone() as JsonObject ?? throw new ArgumentException($"Failed to resolve path {referenced}.");
            foreach (string name in new[] { "nullable", "minLength", "maxLength", "allowedValues" })
            {
                if (current[name] is { } overrideValue)
                {
                    resolved[name] = overrideValue.DeepClone();
                }
            }
            current = resolved;
        }
        return current;
    }

    // Legacy PS GetDynamicParameters: one entry per template parameter; required unless it has a default, is nullable,
    // or -TemplateParameterObject/-TemplateParameterFile/-TemplateParameterUri (or the .bicepparam) already supplies it.
    // Like legacy, any failure (e.g. inputs not complete yet during tab completion) just yields none.
    // A -TemplateSpecId template is fetched by the PS cmdlet and passed in as templateObject.
    public static IReadOnlyList<(string Name, string? Type, bool Required, int? MinLength, int? MaxLength)> GetPowerShellTemplateParameters(
        object? templateObject, string? templateFile, string? templateUri, string? queryString,
        object? parametersObject, string? parametersFile, string? parametersUri)
    {
        try
        {
            JsonObject? template;
            JsonObject? supplied;
            if (!string.IsNullOrWhiteSpace(parametersFile) && IsBicepParamFile(parametersFile))
            {
                string? bicepFile = !string.IsNullOrWhiteSpace(templateFile) && IsBicepFile(templateFile) ? templateFile : null;
                (string? templateJson, _, string parametersJson) = BuildBicepParams(parametersFile, bicepFile, null);
                template = templateJson is null ? null : ParseTemplate(templateJson);
                supplied = JsonNode.Parse(parametersJson)?["parameters"] as JsonObject;
            }
            else
            {
                template = templateObject is not null ? JsonSerializer.SerializeToNode(templateObject) as JsonObject
                    : !string.IsNullOrWhiteSpace(templateFile)
                        ? ParseTemplate(IsBicepFile(templateFile) ? BicepCompiler.BuildTemplate(templateFile) : File.ReadAllText(templateFile))
                    : !string.IsNullOrWhiteSpace(templateUri)
                        ? ParseTemplate(DownloadString(string.IsNullOrWhiteSpace(queryString) ? templateUri : AppendQueryString(templateUri, queryString)))
                    : null;
                supplied = parametersObject is not null ? JsonSerializer.SerializeToNode(parametersObject) as JsonObject
                    : !string.IsNullOrWhiteSpace(parametersFile) ? UnwrapParameters(JsonNode.Parse(File.ReadAllText(parametersFile), documentOptions: s_jsonWithComments))
                    : !string.IsNullOrWhiteSpace(parametersUri) ? UnwrapParameters(JsonNode.Parse(DownloadString(parametersUri), documentOptions: s_jsonWithComments))
                    : null;
            }
            if (template?["parameters"] is not JsonObject parameters)
            {
                return [];
            }
            HashSet<string> suppliedNames = new(supplied?.Select(p => p.Key) ?? [], StringComparer.OrdinalIgnoreCase);
            return parameters
                .Where(p => p.Value is JsonObject)
                .Select(p =>
                {
                    JsonObject resolved = ResolveParameterType((JsonObject)p.Value!, template);
                    bool nullable = resolved["nullable"] is JsonValue n && n.TryGetValue(out bool isNullable) && isNullable;
                    return (p.Key,
                        resolved["type"] is JsonValue t && t.TryGetValue(out string? type) ? type : null,
                        p.Value!["defaultValue"] is null && !nullable && !suppliedNames.Contains(p.Key),
                        resolved["minLength"] is JsonValue min && min.TryGetValue(out int minLength) ? minLength : (int?)null,
                        resolved["maxLength"] is JsonValue max && max.TryGetValue(out int maxLength) ? maxLength : (int?)null);
                })
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static string DownloadString(string url) => s_download.GetStringAsync(url).GetAwaiter().GetResult();

    // Legacy CLI _build_preflight_error_message: "code - message", nested details on following lines.
    protected static string BuildPreflightErrorMessage(ErrorResponse error) =>
        string.Join('\n', (error.Details ?? []).Select(BuildPreflightErrorMessage).Prepend($"{error.Code} - {error.Message}"));

    // Legacy CLI _build_http_response_error_message: the error JSON without details, then each nested detail as "Inner Errors".
    protected string BuildHttpResponseErrorMessage(ErrorResponse error, string prefix = "")
    {
        ErrorResponse self = new() { Code = error.Code, Message = error.Message, Target = error.Target, AdditionalInfo = error.AdditionalInfo };
        JsonSerializerOptions options = new(JsonOptions) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        return string.Join("\n\n", (error.Details ?? [])
            .Select(d => BuildHttpResponseErrorMessage(d, "Inner Errors: \n"))
            .Prepend(prefix + JsonSerializer.Serialize(self, options)));
    }

    // Legacy CLI rule: template file/URI basename without extension, else template spec resource name, else 'deployment1'.
    protected string CliDefaultDeploymentName()
    {
        string? source = null;
        if (!string.IsNullOrWhiteSpace(TemplateFile) && File.Exists(TemplateFile))
        {
            source = TemplateFile;
        }
        if (!string.IsNullOrWhiteSpace(TemplateUri) && Uri.TryCreate(TemplateUri, UriKind.Absolute, out Uri? uri))
        {
            source = uri.AbsolutePath;
        }
        if (!string.IsNullOrWhiteSpace(TemplateSpecId) && string.IsNullOrWhiteSpace(TemplateFile) && string.IsNullOrWhiteSpace(TemplateUri))
        {
            string id = TemplateSpecId.Trim('"');
            if (!id.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase) || !id.Contains("/providers/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("--template-spec is not a valid resource ID.");
            }
            if (!id.Contains("versions", StringComparison.Ordinal))
            {
                throw new ArgumentException("Please enter a valid template spec version ID.");
            }
            source = id.TrimEnd('/').Split('/')[^1];
        }
        string name = Path.GetFileNameWithoutExtension(source ?? string.Empty);
        return string.IsNullOrEmpty(name) ? "deployment1" : name;
    }
}

/// <summary>
/// Legacy az CLI (knack) console prompts. ponytail: the runtime has no prompt service, so these use the console directly.
/// Callers check <see cref="Console.IsInputRedirected"/> first, like knack's NoTTYException.
/// </summary>
internal static class CliPrompt
{
    // knack prompt / prompt_pass: '?' prints the help and asks again.
    public static string Text(string message, string? help = null, bool secret = false)
    {
        while (true)
        {
            Console.Write(message);
            string value = (secret ? ReadSecret() : Console.ReadLine()) ?? string.Empty;
            if (value == "?" && help is not null)
            {
                Console.WriteLine(help);
                continue;
            }
            return value;
        }
    }

    // knack prompt_int.
    public static long Int(string message, string help, Action<string> warn)
    {
        while (true)
        {
            string value = Text(message, help);
            if (long.TryParse(value.Trim(), out long number))
            {
                return number;
            }
            warn($"{value} is not a valid number");
        }
    }

    // knack prompt_y_n / prompt_t_f: asks until the true or false answer is given.
    public static bool Bool(string message, string trueAnswer, string falseAnswer, string? help = null)
    {
        while (true)
        {
            string value = Text($"{message} ({trueAnswer}/{falseAnswer}): ", help);
            if (value.Equals(falseAnswer, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (value.Equals(trueAnswer, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
    }

    // knack prompt_choice_list: numbered options, empty answer picks the first one; returns the 0-based index.
    public static int Choice(string message, IReadOnlyList<string> options, string help, Action<string> warn)
    {
        string list = string.Join('\n', options.Select((option, i) => $" [{i + 1}] {option}"));
        while (true)
        {
            string value = Text($"{message}\n{list}\nPlease enter a choice [Default choice(1)]: ", help);
            if (value.Length == 0)
            {
                value = "1";
            }
            if (int.TryParse(value, out int choice) && choice >= 1 && choice <= options.Count)
            {
                return choice - 1;
            }
            warn($"Valid values are [{string.Join(", ", Enumerable.Range(1, options.Count))}]");
        }
    }

    // getpass: reads without echoing.
    private static string ReadSecret()
    {
        StringBuilder value = new();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return value.ToString();
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0)
                {
                    value.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                value.Append(key.KeyChar);
            }
        }
    }
}
