// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Shared input surface and request building for management-group deployments
/// (<c>az deployment mg create|validate|what-if</c>, <c>New-/Test-AzManagementGroupDeployment</c>).
/// CLI follows legacy <c>_prepare_deployment_properties_unmodified</c>; PS (<see cref="IsCli"/> false) follows
/// legacy <c>DeploymentCmdletBase</c> + <c>NewResourceManagerSdkClient.CreateBasicDeployment</c>.
/// </summary>
public abstract class ManagementGroupDeploymentOrchestratorBase<TRequest, TResponse> : ResourcesProcessorBase<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    private const string DeploymentScope = "managementGroup";
    private const string TemplateSpecsApiVersion = "2022-02-01";

    // Plain client like legacy urlopen: template/parameter URIs are public or SAS links, never sent an ARM token.
    private static readonly HttpClient s_download = new();
    private static readonly JsonDocumentOptions s_jsonWithComments = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly Regex s_templateSchema = new(
        "https?://schema\\.management\\.azure\\.com/schemas/[0-9a-zA-Z-]+/(?<templateType>[a-zA-Z]+)Template\\.json#?", RegexOptions.Compiled);

    public string ManagementGroupId { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string Location { get; set; } = string.Empty;

    public string? TemplateFile { get; set; }

    public string? TemplateUri { get; set; }

    public string? TemplateSpecId { get; set; }

    /// <summary>PS <c>-TemplateObject</c>.</summary>
    public object? TemplateObject { get; set; }

    /// <summary>PS <c>-QueryString</c>, sent on the template link.</summary>
    public string? QueryString { get; set; }

    /// <summary>PS <c>-TemplateParameterFile</c> (.json or .bicepparam).</summary>
    public string? ParametersFile { get; set; }

    /// <summary>PS <c>-TemplateParameterUri</c>, sent as <c>parametersLink</c>.</summary>
    public string? ParametersUri { get; set; }

    /// <summary>PS <c>-TemplateParameterObject</c>.</summary>
    public object? ParametersObject { get; set; }

    /// <summary>PS template parameters given as dynamic cmdlet parameters, as NAME=JSON; they win over the other sources.</summary>
    public string[]? TemplateParameterValues { get; set; }

    public string? ValidationLevel { get; set; }

    /// <summary>CLI <c>--parameters</c> items: one .bicepparam, a file, @file, JSON, URI or KEY=VALUE; later items win.</summary>
    public string[]? CliParameters { get; set; }

    /// <summary>CLI <c>--query-string</c>: legacy allows it only together with --template-uri.</summary>
    public string? CliQueryString { get; set; }

    /// <summary>CLI <c>--no-prompt</c>: warn about missing template parameters instead of asking for them.</summary>
    public bool NoPrompt { get; set; }

    /// <summary>CLI <c>-j/--handle-extended-json-format</c>: deprecated no-op.</summary>
    public bool HandleExtendedJsonFormat { get; set; }

    /// <summary>CLI rules (validators, prompts, Bicep target-scope check); the PS orchestrators override it.</summary>
    protected virtual bool IsCli => true;

    protected override void AfterInit() => Publisher = DeploymentErrorPublisher.Wrap(Publisher, powerShell: !IsCli, rawCli: IsCli);

    // POST whatIf. ARM polls it through Location; a template error comes back as status "Failed", which legacy
    // clients return as the result, so the failed polling response is used instead of the runtime LRO error.
    protected async Task<HttpResponseMessage> SendWhatIfAsync(ScopedDeploymentWhatIf body)
    {
        try
        {
            return await CreateExtendedProcessor<WhatIfAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name!;
                p.Body = body;
            }).SendAsync();
        }
        catch (Exception ex) when (DeploymentErrorPublisher.IsLroFailure(ex) && DeploymentErrorPublisher.LastResponse(Publisher) is { } failed)
        {
            return failed;
        }
    }

    private string DefaultDeploymentName() => IsCli ? CliDefaultDeploymentName() : PsDefaultDeploymentName();

    protected ScopedDeployment BuildDeployment(DeploymentProperties properties) =>
        new() { Location = Location, Properties = properties };

    /// <summary>Legacy CLI argument validators (<c>process_deployment_create_namespace</c>), then the default name.</summary>
    protected string ResolveName()
    {
        if (IsCli)
        {
            if (HandleExtendedJsonFormat)
            {
                Warn("Argument '--handle-extended-json-format/-j' has been deprecated and will be removed in a future release.");
            }
            if (new[] { TemplateUri, TemplateFile, TemplateSpecId }.Count(Has) > 1)
            {
                throw new ArgumentException("incorrect usage: Choose only one of --template-file FILE | --template-uri URI | --template-spec ID to pass in");
            }
        }
        if (!Has(Name))
        {
            Name = DefaultDeploymentName();
        }
        return Name!;
    }

    protected async Task<DeploymentProperties> BuildPropertiesAsync()
    {
        DeploymentProperties properties = IsCli ? await BuildCliPropertiesAsync() : BuildPsProperties();
        properties.Mode = DeploymentMode.Incremental;
        properties.ValidationLevel = NormalizeValidationLevel(ValidationLevel);
        return properties;
    }

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

    // Events.Warning is logged at Info by the engine (hidden by default), so warn through the session logger:
    // PS WriteWarning, CLI "WARNING:" on stderr, like legacy.
    protected static void Warn(string message) => AzclipsServiceProvider.GetRequiredService<ILoggingService>().Warn(message);

    protected static void Verbose(string message) => AzclipsServiceProvider.GetRequiredService<ILoggingService>().Verbose(message);

    // Legacy CLI rule: template file/URI basename without extension, else template spec version name, else 'deployment1'.
    private string CliDefaultDeploymentName()
    {
        string? source = null;
        if (Has(TemplateFile) && File.Exists(TemplateFile))
        {
            source = TemplateFile;
        }
        if (Has(TemplateUri) && Uri.TryCreate(TemplateUri, UriKind.Absolute, out Uri? uri))
        {
            source = uri.AbsolutePath;
        }
        if (Has(TemplateSpecId) && !Has(TemplateFile) && !Has(TemplateUri))
        {
            string id = TemplateSpecId!.Trim('"');
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

    // Legacy PS GenerateDeploymentName: template URI/file basename without extension, else a new GUID.
    // Legacy BuildAndUseBicepTemplate clears -TemplateFile after compiling, so a .bicep file gets a GUID.
    private string PsDefaultDeploymentName()
    {
        string? source = Has(TemplateUri) ? TemplateUri : Has(TemplateFile) && !IsBicepFile(TemplateFile!) ? TemplateFile : null;
        return string.IsNullOrEmpty(source) ? Guid.NewGuid().ToString() : Path.GetFileNameWithoutExtension(source);
    }

    // ── CLI ──

    private async Task<DeploymentProperties> BuildCliPropertiesAsync()
    {
        string[] items = CliParameters ?? [];
        string[] bicepParams = items.Where(IsBicepParamFile).ToArray();
        if (!Has(TemplateFile) && !Has(TemplateSpecId) && !Has(TemplateUri) && bicepParams.Length == 0)
        {
            throw new ArgumentException("Please enter one of the following: template file, template spec, template url, or Bicep parameters file.");
        }
        if (Has(CliQueryString) && !Has(TemplateUri))
        {
            throw new ArgumentException("please provide --template-uri if --query-string is specified");
        }

        DeploymentProperties properties = new();
        if (bicepParams.Length > 0)
        {
            if (bicepParams.Length > 1)
            {
                throw new ArgumentException("Only one .bicepparam file can be provided with --parameters argument");
            }
            // Legacy has no message for this (it fails internally); MG uses the same one.
            if (Has(TemplateUri) || Has(TemplateSpecId))
            {
                throw new ArgumentException("A .bicepparam file can only be combined with --template-file.");
            }
            await BuildCliBicepParamAsync(bicepParams[0], items, properties);
            return properties;
        }

        JsonObject template;
        if (Has(TemplateUri))
        {
            properties.TemplateLink = new TemplateLink { Uri = TemplateUri, QueryString = Has(CliQueryString) ? CliQueryString : null };
            template = await DownloadTemplateAsync(TemplateUri!, CliQueryString);
        }
        else if (Has(TemplateSpecId))
        {
            properties.TemplateLink = new TemplateLink { Id = TemplateSpecId };
            template = await LoadTemplateSpecTemplateAsync(TemplateSpecId!);
        }
        else
        {
            properties.Template = template = ReadTemplateFile(TemplateFile!);
        }

        JsonObject parameters = await ResolveCliParametersAsync(items, template);
        AddMissingParameters(parameters, template);
        properties.Parameters = parameters;
        return properties;
    }

    // Legacy CLI _process_parameters: per item try file, JSON, URI, then KEY=VALUE typed by the template; later items win.
    private async Task<JsonObject> ResolveCliParametersAsync(string[] items, JsonObject template)
    {
        JsonObject parameters = new();
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
            if (!TryParseKeyValue(parameters, template, item))
            {
                throw new ArgumentException($"Unable to parse parameter: {item}");
            }
        }
        return parameters;
    }

    // Legacy CLI _parse_bicepparam_file + the bicepparam branch of _prepare_deployment_properties_unmodified.
    private async Task BuildCliBicepParamAsync(string bicepParam, string[] items, DeploymentProperties properties)
    {
        string? templateFile = Has(TemplateFile) ? TemplateFile : null;
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
            JsonObject template = templateSpecId is not null ? await LoadTemplateSpecTemplateAsync(templateSpecId) : ParseCliTemplate(templateJson!, usingNone ? templateFile : null);
            JsonObject inline = new();
            foreach (string item in items.Where(i => !IsBicepParamFile(i)))
            {
                if (!TryParseKeyValue(inline, template, item))
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
            properties.TemplateLink = new TemplateLink { Id = templateSpecId };
            templateObject = await LoadTemplateSpecTemplateAsync(templateSpecId);
        }
        else
        {
            properties.Template = templateObject = !string.IsNullOrEmpty(templateJson) ? ParseCliTemplate(templateJson, usingNone ? templateFile : null) : ReadTemplateFile(templateFile!);
        }
        ValidateTargetScope(templateObject);
        properties.Parameters = JsonNode.Parse(parametersJson)?["parameters"]?.DeepClone() as JsonObject ?? new JsonObject();
    }

    // Legacy CLI _process_template_file: a Bicep file is compiled and its target scope checked; JSON may have comments.
    private JsonObject ReadTemplateFile(string path)
    {
        EnsureFileExists(path);
        if (!IsBicepFile(path))
        {
            return IsCli ? ParseCliTemplate(File.ReadAllText(path), path) : ParseTemplate(File.ReadAllText(path));
        }
        JsonObject template = ParseTemplate(BicepCompiler.BuildTemplate(path));
        if (IsCli)
        {
            ValidateTargetScope(template);
        }
        return template;
    }

    // Legacy CLI validate_bicep_target_scope: a missing or unknown $schema reports "None".
    private static void ValidateTargetScope(JsonObject template)
    {
        Match match = s_templateSchema.Match(template["$schema"] is JsonValue s && s.TryGetValue(out string? schema) ? schema : string.Empty);
        string? targetScope = match.Success ? match.Groups["templateType"].Value.ToLowerInvariant() switch
        {
            "deployment" => "resourceGroup",
            "subscriptiondeployment" => "subscription",
            "managementgroupdeployment" => "managementGroup",
            "tenantdeployment" => "tenant",
            _ => null,
        } : null;
        if (targetScope != DeploymentScope)
        {
            throw new ArgumentException($"The target scope \"{targetScope ?? "None"}\" does not match the deployment scope \"{DeploymentScope}\".");
        }
    }

    private async Task<JsonObject> DownloadTemplateAsync(string templateUri, string? queryString)
    {
        string url = Has(queryString) ? new UriBuilder(templateUri) { Query = queryString!.TrimStart('?') }.Uri.ToString() : templateUri;
        string content;
        try
        {
            content = await s_download.GetStringAsync(url, Publisher.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ArgumentException($"Unable to retrieve url {url}");
        }
        return ParseCliTemplate(content, url);
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
            return UnwrapParameters(ParseCliJson(content, item));
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
            return UnwrapParameters(ParseCliJson(item, null));
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
            return UnwrapParameters(ParseCliJson(await s_download.GetStringAsync(item, Publisher.Token), null));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    // Legacy CLI _try_parse_key_value_object: KEY=VALUE typed by the template parameter's (resolved) type.
    private static bool TryParseKeyValue(JsonObject parameters, JsonObject template, string item)
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
            int choice = Prompt.Choice(message, allowed.Select(v => v?.ToString() ?? "None").ToList(), help);
            return allowed[choice]?.DeepClone();
        }
        switch (type)
        {
            case "securestring":
                return JsonValue.Create(Prompt.Text(message, help, secret: true));
            case "int":
                return JsonValue.Create(Prompt.Int(message, help));
            case "bool":
                return JsonValue.Create(Prompt.Bool(message, "t", "f", help));
            case "object" or "array":
                while (true)
                {
                    string value = Prompt.Text(message, help);
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
                return JsonValue.Create(Prompt.Text(message, help));
        }
    }

    // ── PS ──

    // Legacy PS GetTemplateParameterObject + CreateBasicDeployment. The cmdlet has already checked the parameter sets,
    // the .bicepparam combinations and resolved the paths.
    private DeploymentProperties BuildPsProperties()
    {
        JsonObject dynamicValues = new();
        foreach (string pair in TemplateParameterValues ?? [])
        {
            int separator = pair.IndexOf('=');
            dynamicValues[pair[..separator]] = JsonNode.Parse(pair[(separator + 1)..]);
        }

        JsonObject? parameters = null;
        if (Has(ParametersFile) && IsBicepParamFile(ParametersFile!))
        {
            // Legacy BuildAndUseBicepParameters: the dynamic parameters are the build overrides; no --bicep-file.
            (string? templateJson, string? templateSpecId, string parametersJson) =
                BuildBicepParams(ParametersFile!, null, dynamicValues.Count == 0 ? null : dynamicValues.ToJsonString());
            if (TemplateObject is null && !Has(TemplateFile) && !Has(TemplateUri) && !Has(TemplateSpecId))
            {
                if (!string.IsNullOrEmpty(templateJson))
                {
                    TemplateObject = ParseTemplate(templateJson);
                }
                else if (!string.IsNullOrEmpty(templateSpecId))
                {
                    TemplateSpecId = templateSpecId;
                }
                else
                {
                    throw new InvalidOperationException($"The provided file {ParametersFile} doesn't exist");
                }
            }
            parameters = JsonNode.Parse(parametersJson)?["parameters"]?.DeepClone() as JsonObject ?? new JsonObject();
        }

        DeploymentProperties properties = new();
        if (Has(TemplateSpecId))
        {
            properties.TemplateLink = new TemplateLink { Id = TemplateSpecId };
        }
        else if (Has(TemplateUri))
        {
            properties.TemplateLink = new TemplateLink { Uri = TemplateUri, QueryString = Has(QueryString) ? QueryString : null };
        }
        else if (Has(TemplateFile))
        {
            properties.Template = ReadTemplateFile(TemplateFile!);
        }
        else
        {
            properties.Template = TemplateObject as JsonObject ?? JsonSerializer.SerializeToNode(TemplateObject);
        }

        if (Has(ParametersUri))
        {
            properties.ParametersLink = new ParametersLink { Uri = ParametersUri! };
            return properties;
        }

        if (parameters is null)
        {
            parameters = new JsonObject();
            if (ParametersObject is not null)
            {
                // Legacy AddTemplateObjectParametersToHashtable: each value becomes { "value": v }; a Key Vault { "reference" } passes through.
                foreach ((string key, JsonNode? value) in JsonSerializer.SerializeToNode(ParametersObject) as JsonObject ?? new JsonObject())
                {
                    parameters[key] = value is JsonObject reference && reference.ContainsKey("reference")
                        ? value.DeepClone()
                        : new JsonObject { ["value"] = value?.DeepClone() };
                }
            }
            else if (Has(ParametersFile))
            {
                if (File.Exists(ParametersFile))
                {
                    foreach ((string key, JsonNode? value) in UnwrapParameters(JsonNode.Parse(File.ReadAllText(ParametersFile!), documentOptions: s_jsonWithComments)))
                    {
                        parameters[key] = value?.DeepClone();
                    }
                }
                else
                {
                    // Legacy warning text, uninterpolated as in legacy.
                    Warn("${templateParameterFilePath} does not exist");
                }
            }
            foreach ((string key, JsonNode? value) in dynamicValues)
            {
                parameters[key] = new JsonObject { ["value"] = value?.DeepClone() };
            }
        }
        properties.Parameters = parameters;
        return properties;
    }

    // Legacy PS DeploymentCmdletBase.GetDynamicParameters + TemplateUtility.GetDynamicParameters: one entry per template
    // parameter; required unless it has a default, is nullable, or the parameter object/file/URI (or .bicepparam) supplies it.
    // Like legacy, any failure (inputs incomplete during tab completion) yields none. The template spec is read by the cmdlet.
    public static IReadOnlyList<(string Name, string? Type, bool Required, int? MinLength, int? MaxLength)> GetPowerShellTemplateParameters(
        object? templateObject, string? templateFile, string? templateUri, string? templateSpecId, Func<string, object?> readTemplateSpec,
        object? parametersObject, string? parametersFile, string? parametersUri)
    {
        try
        {
            JsonObject? supplied;
            if (Has(parametersFile) && IsBicepParamFile(parametersFile!))
            {
                (string? templateJson, string? bicepTemplateSpecId, string parametersJson) = BuildBicepParams(parametersFile!, null, null);
                supplied = JsonNode.Parse(parametersJson)?["parameters"] as JsonObject;
                if (templateObject is null && !Has(templateFile) && !Has(templateUri) && !Has(templateSpecId))
                {
                    templateObject = templateJson is null ? null : ParseTemplate(templateJson);
                    templateSpecId = bicepTemplateSpecId;
                }
            }
            else
            {
                supplied = parametersObject is not null ? JsonSerializer.SerializeToNode(parametersObject) as JsonObject
                    : Has(parametersFile) ? UnwrapParameters(JsonNode.Parse(File.ReadAllText(parametersFile!), documentOptions: s_jsonWithComments))
                    : Has(parametersUri) ? UnwrapParameters(JsonNode.Parse(s_download.GetStringAsync(parametersUri).GetAwaiter().GetResult(), documentOptions: s_jsonWithComments))
                    : null;
            }

            JsonObject? template =
                Has(templateFile) ? ParseTemplate(IsBicepFile(templateFile!) ? BicepCompiler.BuildTemplate(templateFile!) : File.ReadAllText(templateFile!))
                : Has(templateUri) ? ParseTemplate(s_download.GetStringAsync(templateUri).GetAwaiter().GetResult())
                : Has(templateSpecId) ? JsonSerializer.SerializeToNode(readTemplateSpec(templateSpecId!)) as JsonObject
                : templateObject is not null ? JsonSerializer.SerializeToNode(templateObject) as JsonObject
                : null;
            if (template?["parameters"] is not JsonObject templateParameters)
            {
                return [];
            }
            HashSet<string> suppliedNames = new(supplied?.Select(p => p.Key) ?? [], StringComparer.OrdinalIgnoreCase);
            return templateParameters
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

    // ── Shared helpers ──

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

    // Legacy `parsed.get('parameters', parsed)`; non-object values are not a parameter source.
    private static JsonObject UnwrapParameters(JsonNode? node)
    {
        JsonObject root = node as JsonObject ?? throw new ArgumentException("Parameters must be a JSON object.");
        return root.ContainsKey("parameters")
            ? root["parameters"] as JsonObject ?? throw new ArgumentException("'parameters' must be a JSON object.")
            : root;
    }

    // CLI --validation-level is an enum (case-insensitive); PS passes any string through.
    private static string? NormalizeValidationLevel(string? level) =>
        !Has(level) ? null
            : new[] { "Template", "Provider", "ProviderNoRbac" }.FirstOrDefault(l => l.Equals(level, StringComparison.OrdinalIgnoreCase)) ?? level;

    private static JsonObject ParseTemplate(string json) =>
        JsonNode.Parse(json, documentOptions: s_jsonWithComments) as JsonObject
            ?? throw new ArgumentException("The template must be a JSON object.");

    private static JsonObject ParseCliTemplate(string json, string? path) =>
        ParseCliJson(json, path) as JsonObject ?? throw new ArgumentException("The template must be a JSON object.");

    // Legacy CLI _remove_comments_from_json: comments allowed, json.loads(strict=False) accepts raw control
    // characters (multiline strings) inside strings, and a parse failure reports the legacy message.
    private static JsonNode? ParseCliJson(string json, string? path)
    {
        try
        {
            return JsonNode.Parse(EscapeControlCharactersInStrings(json), documentOptions: s_jsonWithComments);
        }
        catch (JsonException)
        {
            throw new ArgumentException(path is null
                ? "Failed to parse the JSON data, please check whether it is a valid JSON format"
                : $"Failed to parse '{path}', please check whether it is a valid JSON format");
        }
    }

    private static string EscapeControlCharactersInStrings(string json)
    {
        StringBuilder result = new(json.Length);
        bool inString = false, escaped = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
                else if (c < ' ')
                {
                    result.Append($"\\u{(int)c:x4}");
                    continue;
                }
            }
            else if (c == '"')
            {
                inString = true;
            }
            else if (c == '/' && i + 1 < json.Length && json[i + 1] is '/' or '*')
            {
                // Copy comments verbatim so quotes inside them don't start a string.
                int end = json[i + 1] == '/' ? json.IndexOf('\n', i) : json.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? json.Length : json[i + 1] == '/' ? end : end + 2;
                result.Append(json, i, end - i);
                i = end - 1;
                continue;
            }
            result.Append(c);
        }
        return result.ToString();
    }

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"[Errno 2] No such file or directory: '{path}'", path);
        }
    }

    private static bool Has(string? value) => !string.IsNullOrWhiteSpace(value);

    private static bool IsBicepFile(string path) => path.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase);

    private static bool IsBicepParamFile(string path) => path.EndsWith(".bicepparam", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Legacy az CLI (knack) console prompts. ponytail: the runtime has no prompt service, so these use the console directly.
    /// Callers check <see cref="Console.IsInputRedirected"/> first, like knack's NoTTYException.
    /// </summary>
    protected static class Prompt
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
        public static long Int(string message, string help)
        {
            while (true)
            {
                string value = Text(message, help);
                if (long.TryParse(value.Trim(), out long number))
                {
                    return number;
                }
                Warn($"{value} is not a valid number");
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
        public static int Choice(string message, IReadOnlyList<string> options, string help)
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
                Warn($"Valid values are [{string.Join(", ", Enumerable.Range(1, options.Count))}]");
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
}
