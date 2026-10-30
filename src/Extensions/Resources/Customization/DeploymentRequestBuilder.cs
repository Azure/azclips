// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Scope-neutral deployment inputs (name, template source, parameters source, mode, inline
    /// parameters, validation level) with their validation and the template/parameters/bicep
    /// building, so every deployment scope shares one copy of that logic. Orchestrators own their
    /// scope inputs (resource group, management group, ...), check them first, and compose one
    /// builder per invocation.
    /// </summary>
    internal sealed class DeploymentRequestBuilder
    {
        private static readonly Regex DeploymentNamePattern = new("^[-\\w\\._\\(\\)]+$", RegexOptions.Compiled);

        public string Name { get; set; } = string.Empty;

        public string? TemplateUri { get; set; }

        public string? TemplateFile { get; set; }

        public string? TemplateSpecId { get; set; }

        /// <summary>
        /// A hash table holding the ARM template itself, the fourth template source alongside
        /// TemplateFile/TemplateUri/TemplateSpecId. PowerShell-only: the CLI has no equivalent.
        /// </summary>
        public Hashtable? TemplateObject { get; set; }

        public string? QueryString { get; set; }

        public string? ParametersUri { get; set; }

        public string? ParametersFile { get; set; }

        public string? Mode { get; set; }

        public string[]? Parameter { get; set; }

        public string? ValidationLevel { get; set; }

        // The inlined (non-link) template that BuildTemplate produced, kept so BuildParameters can
        // check the template's own "parameters" section for values the caller never supplied.
        private JsonElement? inlineTemplate;

        // 'bicep build-params' shells out to the Bicep CLI, so its envelope is compiled at most once
        // per invocation: BuildTemplate and BuildParameters both read from this cache.
        private JsonElement? bicepParamEnvelope;

        private bool bicepVersionChecked;

        // Validates the template/parameters/mode/inline rules shared by every deployment scope.
        // Fails fast with deterministic ArgumentExceptions. Orchestrators check their scope inputs
        // first, call this, then add their own command-specific rules.
        public void ValidateCommonInputs()
        {
            // Like azure-cli, a .bicepparam path passed as an inline parameter is the parameters file.
            int bicepParamIndex = Array.FindIndex(Parameter ?? [], t => IsBicepParamFile(t.Trim()));
            if (string.IsNullOrWhiteSpace(ParametersFile) && bicepParamIndex >= 0)
            {
                ParametersFile = Parameter![bicepParamIndex].Trim();
                Parameter = [.. Parameter[..bicepParamIndex], .. Parameter[(bicepParamIndex + 1)..]];
            }

            // Like azure-cli, a URL passed as an inline parameter is a remote parameters file. It is sent
            // as a parametersLink for ARM to fetch, and ARM rejects inline values alongside that link.
            if (Parameter is { Length: > 0 } && Array.Exists(Parameter, t => IsHttpUrl(t.Trim())))
            {
                if (Parameter.Length > 1 || !string.IsNullOrWhiteSpace(ParametersUri) || !string.IsNullOrWhiteSpace(ParametersFile))
                {
                    throw AzclipsErrorException.InvalidInput("A parameters URL must be the only parameter source; it cannot be combined with other inline parameter values, a parameters file, or a parameters URI, because ARM does not allow inline parameters together with a parameters link.");
                }
                ParametersUri = Parameter[0].Trim();
                Parameter = [];
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                // The deployment name is optional: legacy PowerShell's DeploymentName carries no
                // [Parameter] attribute at all and falls back to Guid.NewGuid(), and the CLI's
                // --name is declared required=False.
                Name = Guid.NewGuid().ToString();
            }
            else if (Name.Length > 64 || !DeploymentNamePattern.IsMatch(Name))
            {
                throw AzclipsErrorException.InvalidInput("Deployment name must be 1-64 characters and match the pattern ^[-\\w\\._\\(\\)]+$.");
            }

            int templateSources = (string.IsNullOrWhiteSpace(TemplateUri) ? 0 : 1)
                                 + (string.IsNullOrWhiteSpace(TemplateFile) ? 0 : 1)
                                 + (string.IsNullOrWhiteSpace(TemplateSpecId) ? 0 : 1)
                                 + (TemplateObject is { Count: > 0 } ? 1 : 0);
            // A .bicepparam file names the template it targets in its 'using' declaration, so the
            // envelope produced by 'bicep build-params' supplies the template on its own.
            if (templateSources > 1 || (templateSources == 0 && !IsBicepParamFile(ParametersFile)))
            {
                throw new ArgumentException("Exactly one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided, unless ParametersFile is a .bicepparam file that carries its own template.");
            }

            int parameterSources = (string.IsNullOrWhiteSpace(ParametersUri) ? 0 : 1)
                                  + (string.IsNullOrWhiteSpace(ParametersFile) ? 0 : 1);
            if (parameterSources > 1)
            {
                throw AzclipsErrorException.InvalidInput("Provide at most one of ParametersUri or ParametersFile.");
            }

            // ARM allows EITHER an inline "parameters" object OR a "parametersLink", never both.
            if (Parameter is { Length: > 0 } && !string.IsNullOrWhiteSpace(ParametersUri))
            {
                throw AzclipsErrorException.InvalidInput("Inline parameters cannot be combined with a parameters URI; ARM does not allow inline parameters together with a parameters link. Provide a parameters file instead, or remove the parameters URI.");
            }

            // Validate the parameter tokens up front so malformed input fails fast.
            if (Parameter is { Length: > 0 })
            {
                foreach (string token in Parameter)
                {
                    ApplyParameterToken(new JsonObject(), token);
                }
            }

            // azure-cli rejects --query-string unless --template-uri is given, because ARM's
            // queryString field only exists on the TemplateLink built from a template URI.
            if (!string.IsNullOrWhiteSpace(QueryString) && string.IsNullOrWhiteSpace(TemplateUri))
            {
                throw new ArgumentException("QueryString requires TemplateUri; it is only carried on the template link built from a template URI.");
            }

            if (!string.IsNullOrWhiteSpace(Mode)
                && !Mode.Equals("Incremental", StringComparison.OrdinalIgnoreCase)
                && !Mode.Equals("Complete", StringComparison.OrdinalIgnoreCase))
            {
                throw AzclipsErrorException.InvalidInput("Mode must be either 'Incremental' or 'Complete'.");
            }
        }

        public static DeploymentMode ParseMode(string? mode) =>
            string.IsNullOrWhiteSpace(mode)
                ? DeploymentMode.Incremental
                : Enum.Parse<DeploymentMode>(mode, ignoreCase: true);

        public object? BuildTemplate(out TemplateLink? templateLink)
        {
            templateLink = null;
            inlineTemplate = null;

            if (!string.IsNullOrWhiteSpace(TemplateUri))
            {
                templateLink = new TemplateLink
                {
                    Uri = TemplateUri,
                    QueryString = NormalizeQueryString(QueryString),
                };
                return null;
            }

            if (!string.IsNullOrWhiteSpace(TemplateSpecId))
            {
                // A Template Spec link never carries a query string: azure-cli attaches --query-string
                // to the template-uri link only.
                templateLink = new TemplateLink { Id = TemplateSpecId };
                return null;
            }

            if (TemplateObject is { Count: > 0 })
            {
                inlineTemplate = JsonSerializer.SerializeToElement(TemplateObject);
                return inlineTemplate;
            }

            if (string.IsNullOrWhiteSpace(TemplateFile))
            {
                return BuildTemplateFromBicepParam(out templateLink);
            }

            // Local file: read and inline. A .bicep source is compiled to ARM JSON first.
            string templatePath = TemplateFile!;
            if (IsBicepFile(templatePath))
            {
                EnsureFileExists(templatePath);
                string armJson = BicepCompiler.BuildTemplate(templatePath);
                inlineTemplate = JsonSerializer.Deserialize<JsonElement>(armJson);
                return inlineTemplate;
            }

            inlineTemplate = ReadJsonFile(templatePath);
            return inlineTemplate;
        }

        // No explicit template source: the .bicepparam envelope names the template its 'using'
        // declaration points at, either as inline ARM JSON or as a Template Spec id.
        private object? BuildTemplateFromBicepParam(out TemplateLink? templateLink)
        {
            templateLink = null;
            JsonElement envelope = GetBicepParamEnvelope();

            if (TryGetNonEmptyString(envelope, "templateJson", out string? templateJson))
            {
                inlineTemplate = JsonSerializer.Deserialize<JsonElement>(templateJson!);
                return inlineTemplate;
            }

            if (TryGetNonEmptyString(envelope, "templateSpecId", out string? templateSpecId))
            {
                templateLink = new TemplateLink { Id = templateSpecId };
                return null;
            }

            throw new ArgumentException($"The .bicepparam file '{ParametersFile}' does not reference a template, so one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided.");
        }

        public object? BuildParameters(out ParametersLink? parametersLink)
        {
            parametersLink = null;

            if (!string.IsNullOrWhiteSpace(ParametersUri))
            {
                parametersLink = new ParametersLink { Uri = ParametersUri };
                return null;
            }

            JsonElement? fileParameters = null;
            JsonObject? bicepParamInline = null;
            if (!string.IsNullOrWhiteSpace(ParametersFile))
            {
                if (IsBicepParamFile(ParametersFile))
                {
                    // Like azure-cli's supplemental parameters, inline values are compiled into the
                    // .bicepparam file as overrides rather than overlaid on its output.
                    if (Parameter is { Length: > 0 })
                    {
                        bicepParamInline = new JsonObject();
                        foreach (string token in Parameter)
                        {
                            ApplyParameterToken(bicepParamInline, token);
                        }
                    }
                    fileParameters = ReadBicepParamParameters(bicepParamInline);
                }
                else
                {
                    fileParameters = ReadParametersFile(ParametersFile);
                }
            }

            // No inline overlay: preserve the original file-or-null behavior.
            if (Parameter is not { Length: > 0 })
            {
                EnsureNoMissingTemplateParameters(fileParameters);
                return fileParameters;
            }

            // Inline parameters present: start from the file's inner parameters object (the base)
            // and overlay each token on top, so inline values win per key.
            JsonObject parameters;
            if (fileParameters is { } element)
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    throw AzclipsErrorException.InvalidInput("The parameters file must contain a JSON object of parameters to combine with inline parameter values.");
                }
                parameters = JsonObject.Create(element)!;
            }
            else
            {
                parameters = new JsonObject();
            }

            if (bicepParamInline is not null)
            {
                // Values are already compiled in; only references such as Key Vault entries remain to overlay.
                foreach (KeyValuePair<string, JsonNode?> entry in bicepParamInline)
                {
                    if (entry.Value is JsonObject reference && !reference.ContainsKey("value"))
                    {
                        parameters[FindExistingKeyIgnoreCase(parameters, entry.Key) ?? entry.Key] = reference.DeepClone();
                    }
                }
            }
            else
            {
                foreach (string token in Parameter)
                {
                    ApplyParameterToken(parameters, token);
                }
            }

            EnsureNoMissingTemplateParameters(parameters);
            return parameters;
        }

        // Merges one --parameters/-Parameter token into the accumulated parameters. azure-cli's
        // _process_parameters accepts three forms and merges repeats left to right: a JSON
        // parameters file (a bare path or '@path'), a raw JSON object, or KEY=VALUE.
        private void ApplyParameterToken(JsonObject parameters, string token)
        {
            if (TryReadParameterObject(token, out JsonObject? supplied))
            {
                foreach (KeyValuePair<string, JsonNode?> entry in supplied!)
                {
                    parameters[FindExistingKeyIgnoreCase(parameters, entry.Key) ?? entry.Key] = entry.Value?.DeepClone();
                }
                return;
            }

            (string key, string raw) = ParseInlineParameter(token);
            JsonNode? value = ParseTemplateTypedValue(key, raw);
            // ARM parameter names are case-insensitive, but JsonObject's indexer is case-sensitive.
            // Reuse any existing same-named key (from the file or an earlier token, matched
            // case-insensitively) so the body never carries two keys differing only by case. The
            // existing casing is kept, the inline value wins, and for duplicates the last one wins.
            parameters[FindExistingKeyIgnoreCase(parameters, key) ?? key] = new JsonObject { ["value"] = value };
        }

        // Recognizes the two object-valued token forms — '@path' or a bare path to a JSON
        // parameters file, and a raw JSON object — leaving everything else to be read as KEY=VALUE.
        private static bool TryReadParameterObject(string token, out JsonObject? parameters)
        {
            parameters = null;
            string trimmed = token.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            if (trimmed[0] == '@')
            {
                string path = trimmed[1..];
                EnsureFileExists(path);
                parameters = UnwrapParameterObject(ParseJsonObject(File.ReadAllText(path), $"parameters file '{path}'"));
                return true;
            }

            if (trimmed[0] == '{')
            {
                parameters = UnwrapParameterObject(ParseJsonObject(trimmed, $"inline parameter JSON '{token}'"));
                return true;
            }

            if (File.Exists(trimmed))
            {
                parameters = UnwrapParameterObject(ParseJsonObject(File.ReadAllText(trimmed), $"parameters file '{trimmed}'"));
                return true;
            }

            return false;
        }

        // Legacy clients accept JSONC in template/parameter files: az strips comments, Az PowerShell parses with Newtonsoft.
        private static readonly JsonDocumentOptions JsoncDocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        private static readonly JsonSerializerOptions JsoncSerializerOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        private static JsonObject ParseJsonObject(string json, string description)
        {
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(json, documentOptions: JsoncDocumentOptions);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Failed to parse {description} as JSON: {ex.Message}", ex);
            }

            return node as JsonObject ?? throw new ArgumentException($"The {description} must contain a JSON object.");
        }

        // ARM parameters files are wrapped as { "$schema", "contentVersion", "parameters": { ... } }.
        private static JsonObject UnwrapParameterObject(JsonObject root) =>
            root["parameters"] as JsonObject ?? root;

        // azure-cli fails client-side when the template declares a parameter that has neither a
        // supplied value nor a defaultValue (custom.py's _find_missing_parameters). The extension
        // runtime is non-interactive, so the missing names are reported instead of prompted for.
        // Only checked when the template was inlined: a TemplateUri/TemplateSpecId link is never
        // downloaded here, and a ParametersLink's values are never visible, so ARM reports those.
        private void EnsureNoMissingTemplateParameters(object? suppliedParameters)
        {
            if (inlineTemplate is not { } template
                || template.ValueKind != JsonValueKind.Object
                || !template.TryGetProperty("parameters", out JsonElement declared)
                || declared.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            List<string> missing = new();
            foreach (JsonProperty parameter in declared.EnumerateObject())
            {
                if (parameter.Value.ValueKind != JsonValueKind.Object
                    || parameter.Value.TryGetProperty("defaultValue", out _)
                    || (parameter.Value.TryGetProperty("nullable", out JsonElement nullable) && nullable.ValueKind == JsonValueKind.True)
                    || HasSuppliedValue(suppliedParameters, parameter.Name))
                {
                    continue;
                }

                missing.Add(parameter.Name);
            }

            if (missing.Count > 0)
            {
                missing.Sort(StringComparer.OrdinalIgnoreCase);
                throw new ArgumentException($"Missing value for required template parameter(s): {string.Join(", ", missing)}. Supply them with a parameters file or with inline parameters.");
            }
        }

        private static bool HasSuppliedValue(object? suppliedParameters, string name) =>
            suppliedParameters switch
            {
                JsonObject parameters => FindExistingKeyIgnoreCase(parameters, name) is not null,
                JsonElement element => element.ValueKind == JsonValueKind.Object
                    && element.EnumerateObject().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)),
                _ => false,
            };

        // The validation level is passed through to ARM untouched. Legacy PowerShell declares it as
        // a plain string with no client-side check, so a level ARM adds later works without a client
        // update. Returns null when nothing was supplied.
        public static string? NormalizeValidationLevel(string? validationLevel) =>
            string.IsNullOrWhiteSpace(validationLevel) ? null : validationLevel.Trim();

        // Trims a single leading '?' or '&' from a query string and returns null when empty/whitespace.
        // ARM's TemplateLink.queryString field stores the raw query WITHOUT a leading separator.
        private static string? NormalizeQueryString(string? queryString)
        {
            if (string.IsNullOrWhiteSpace(queryString))
            {
                return null;
            }
            // Trim surrounding whitespace first so a pasted SAS token with leading/trailing
            // spaces doesn't yield an invalid queryString / embedded ParametersLink URI.
            string trimmed = queryString.Trim();
            trimmed = trimmed.Length > 0 && (trimmed[0] == '?' || trimmed[0] == '&')
                ? trimmed[1..]
                : trimmed;
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        // Find an existing key in 'parameters' that equals 'key' ignoring case, returning the
        // existing key (so its casing is preserved) or null when no such key exists.
        private static string? FindExistingKeyIgnoreCase(JsonObject parameters, string key)
        {
            foreach (var entry in parameters)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Key;
                }
            }

            return null;
        }

        // Split a single inline "KEY=VALUE" token into its key and raw value text.
        private static (string Key, string Raw) ParseInlineParameter(string pair)
        {
            if (string.IsNullOrWhiteSpace(pair))
            {
                throw AzclipsErrorException.InvalidInput("Invalid inline parameter: the token is empty; expected KEY=VALUE.");
            }

            int separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                throw AzclipsErrorException.InvalidInput($"Invalid inline parameter '{pair}': expected KEY=VALUE with a non-empty key.");
            }

            string key = pair[..separator].Trim();
            if (key.Length == 0)
            {
                throw AzclipsErrorException.InvalidInput($"Invalid inline parameter '{pair}': the key is empty.");
            }

            string raw = pair[(separator + 1)..];
            return (key, raw);
        }

        // Like azure-cli's _try_parse_key_value_object, a key the local template declares is read as
        // that parameter's type. Undeclared keys and linked templates parse JSON-first.
        private JsonNode? ParseTemplateTypedValue(string key, string raw)
        {
            switch (ResolveParameterType(key)?.ToLowerInvariant())
            {
                case null:
                    return ParseTypedValue(raw);
                case "object" or "array" or "secureobject":
                    try
                    {
                        return JsonNode.Parse(raw);
                    }
                    catch (JsonException ex)
                    {
                        throw new ArgumentException($"Failed to parse inline parameter '{key}' as JSON: {ex.Message}", ex);
                    }
                case "bool":
                    return JsonValue.Create(raw.Equals("true", StringComparison.OrdinalIgnoreCase));
                case "int" when long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number):
                    return JsonValue.Create(number);
                default:
                    return JsonValue.Create(raw);
            }
        }

        // Like azure-cli's _resolve_parameter_type: follows local '$ref' pointers to the declared
        // type; null when the template is not local, the key is undeclared, or a '$ref' cannot be resolved.
        private string? ResolveParameterType(string key)
        {
            if (inlineTemplate is not { ValueKind: JsonValueKind.Object } template
                || !template.TryGetProperty("parameters", out JsonElement declared)
                || declared.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            JsonElement definition = default;
            foreach (JsonProperty parameter in declared.EnumerateObject())
            {
                if (string.Equals(parameter.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    definition = parameter.Value;
                    break;
                }
            }

            HashSet<string> visited = new(StringComparer.Ordinal);
            while (definition.ValueKind == JsonValueKind.Object)
            {
                if (!TryGetNonEmptyString(definition, "$ref", out string? reference))
                {
                    return TryGetNonEmptyString(definition, "type", out string? type) ? type : null;
                }
                if (!visited.Add(reference!) || ResolveReference(template, reference!) is not { } target)
                {
                    return null;
                }
                definition = target;
            }
            return null;
        }

        // Resolves '#/definitions|parameters|outputs/...' JSON pointers within the template.
        private static JsonElement? ResolveReference(JsonElement template, string reference)
        {
            string[] segments = reference.Split('/');
            if (segments.Length < 3 || segments[0] != "#" || segments[1] is not ("definitions" or "parameters" or "outputs"))
            {
                return null;
            }

            JsonElement current = template;
            foreach (string segment in segments[1..])
            {
                string name = segment.Replace("~1", "/").Replace("~0", "~");
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
                {
                    return null;
                }
            }
            return current;
        }

        private static JsonNode? ParseTypedValue(string raw)
        {
            try
            {
                // JSON-first: typed values such as 3, true, ["a","b"], {"k":"v"}.
                return JsonNode.Parse(raw);
            }
            catch (JsonException)
            {
                // Not valid JSON: treat the raw text as a string literal.
                return JsonValue.Create(raw);
            }
        }

        private static JsonElement ReadJsonFile(string path)
        {
            EnsureFileExists(path);
            string content = File.ReadAllText(path);
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(content, JsoncSerializerOptions);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Failed to parse file '{path}' as JSON: {ex.Message}", ex);
            }
        }

        // ARM parameters files are wrapped as { "$schema": ..., "contentVersion": ..., "parameters": { ... } }.
        // The deployment body expects the inner "parameters" object. Unwrap it when present.
        private static JsonElement ReadParametersFile(string path) => UnwrapParameters(ReadJsonFile(path));

        // Compile the .bicepparam file at most once per invocation. 'bicep build-params --stdout'
        // emits an envelope carrying both the parameters and the template the file targets:
        //   { "parametersJson": "<stringified ARM params file>", "templateJson": ..., "templateSpecId": ... }
        private JsonElement GetBicepParamEnvelope()
        {
            if (bicepParamEnvelope is { } cached)
            {
                return cached;
            }

            JsonElement envelope = CompileBicepParam(null);
            bicepParamEnvelope = envelope;
            return envelope;
        }

        private JsonElement CompileBicepParam(string? parametersOverrides)
        {
            EnsureFileExists(ParametersFile!);
            EnsureBicepParamVersion();
            return JsonSerializer.Deserialize<JsonElement>(BicepCompiler.BuildParams(ParametersFile!, parametersOverrides));
        }

        // Check the Bicep CLI once before compiling a .bicepparam file.
        private void EnsureBicepParamVersion()
        {
            if (bicepVersionChecked)
            {
                return;
            }
            bicepVersionChecked = true;
            BicepCompiler.EnsureBicepParamVersion(withOverrides: Parameter is { Length: > 0 });
        }

        // The ARM params file inside "parametersJson" has the standard { $schema, contentVersion, parameters } shape.
        // Inline entries with a value recompile the file with them as overrides.
        private JsonElement ReadBicepParamParameters(JsonObject? inline)
        {
            JsonObject overrides = new();
            foreach (KeyValuePair<string, JsonNode?> entry in inline ?? [])
            {
                if (entry.Value is JsonObject parameter && parameter.TryGetPropertyValue("value", out JsonNode? value))
                {
                    overrides[entry.Key] = value?.DeepClone();
                }
            }

            JsonElement envelope = overrides.Count > 0 ? CompileBicepParam(overrides.ToJsonString()) : GetBicepParamEnvelope();
            if (!TryGetNonEmptyString(envelope, "parametersJson", out string? parametersJson))
            {
                throw new InvalidOperationException(
                    "Unexpected 'bicep build-params --stdout' output: missing 'parametersJson' string.");
            }

            return UnwrapParameters(JsonSerializer.Deserialize<JsonElement>(parametersJson!));
        }

        private static bool TryGetNonEmptyString(JsonElement element, string propertyName, out string? value)
        {
            value = element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(propertyName, out JsonElement property)
                && property.ValueKind == JsonValueKind.String
                    ? property.GetString()
                    : null;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static JsonElement UnwrapParameters(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("parameters", out JsonElement inner))
            {
                return inner;
            }
            return root;
        }

        private static bool IsBicepFile(string path) =>
            path.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase);

        private static bool IsBicepParamFile(string? path) =>
            path is not null && path.EndsWith(".bicepparam", StringComparison.OrdinalIgnoreCase);

        private static bool IsHttpUrl(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

        private static void EnsureFileExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}", path);
            }
        }
    }
}
