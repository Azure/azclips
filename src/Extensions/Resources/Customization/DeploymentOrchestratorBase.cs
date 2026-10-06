// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Azure.Azclips.Extensions.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Shared orchestrator base for resource-group-scoped deployment commands (create and
    /// what-if). Owns the common user-facing input surface (template-source, parameters-source,
    /// mode, inline parameters, validation-level) together with the validation and the
    /// template/parameters/bicep building that both commands share, so there is a single copy of
    /// that logic. Command-specific options (rollback for create, result-format for what-if) and
    /// the request dispatch live in the derived orchestrators.
    /// </summary>
    public abstract class DeploymentOrchestratorBase<TRequest, TResponse> : ResourcesProcessorBase<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        protected static readonly Regex DeploymentNamePattern = new("^[-\\w\\._\\(\\)]+$", RegexOptions.Compiled);

        public string ResourceGroupName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? TemplateUri { get; set; }

        public string? TemplateFile { get; set; }

        public string? TemplateSpecId { get; set; }

        public object? TemplateObject { get; set; }

        public string? QueryString { get; set; }

        public string? ParametersUri { get; set; }

        public string? ParametersFile { get; set; }

        public object? ParametersObject { get; set; }

        public string? Mode { get; set; }

        public string[]? Parameter { get; set; }

        public string? ValidationLevel { get; set; }

        // Validates the template/parameters/mode/inline/validation-level rules shared by the
        // create and what-if commands. Fails fast with deterministic ArgumentExceptions. Derived
        // orchestrators call this first, then add their own command-specific rules.
        protected void ValidateCommonInputs()
        {
            ValidateScopeInputs();
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 64 || !DeploymentNamePattern.IsMatch(Name))
            {
                throw new ArgumentException("Deployment name must be 1-64 characters and match the pattern ^[-\\w\\._\\(\\)]+$.");
            }

            int templateSources = (string.IsNullOrWhiteSpace(TemplateUri) ? 0 : 1)
                                 + (string.IsNullOrWhiteSpace(TemplateFile) ? 0 : 1)
                                 + (string.IsNullOrWhiteSpace(TemplateSpecId) ? 0 : 1)
                                 + (TemplateObject is null ? 0 : 1);
            if (templateSources != 1)
            {
                throw new ArgumentException(
                    "Exactly one of TemplateUri, TemplateFile, TemplateSpecId, or TemplateObject must be provided.");
            }

            int parameterSources = (string.IsNullOrWhiteSpace(ParametersUri) ? 0 : 1)
                                  + (string.IsNullOrWhiteSpace(ParametersFile) ? 0 : 1)
                                  + (ParametersObject is null ? 0 : 1);
            if (parameterSources > 1)
            {
                throw new ArgumentException(
                    "Provide at most one of ParametersUri, ParametersFile, or ParametersObject.");
            }

            // ARM allows EITHER an inline "parameters" object OR a "parametersLink", never both.
            if (Parameter is { Length: > 0 } && !string.IsNullOrWhiteSpace(ParametersUri))
            {
                throw new ArgumentException("Inline parameters cannot be combined with a parameters URI; ARM does not allow inline parameters together with a parameters link. Provide a parameters file instead, or remove the parameters URI.");
            }

            // Validate inline parameter tokens up front so malformed input fails fast.
            if (Parameter is { Length: > 0 })
            {
                foreach (string pair in Parameter)
                {
                    _ = ParseInlineParameter(pair);
                }
            }

            if (!string.IsNullOrWhiteSpace(Mode)
                && !Mode.Equals("Incremental", StringComparison.OrdinalIgnoreCase)
                && !Mode.Equals("Complete", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Mode must be either 'Incremental' or 'Complete'.");
            }

            if (!string.IsNullOrWhiteSpace(ValidationLevel) && NormalizeValidationLevel(ValidationLevel) is null)
            {
                throw new ArgumentException("ValidationLevel must be one of 'Template', 'Provider', or 'ProviderNoRbac'.");
            }
        }

        protected virtual void ValidateScopeInputs()
        {
            if (string.IsNullOrWhiteSpace(ResourceGroupName))
            {
                throw new ArgumentException("ResourceGroupName must be provided.");
            }
        }

        // The ARM deployment scope this orchestrator targets (resourceGroup/subscription/
        // managementGroup/tenant). Used only to validate a local .bicep file's target scope
        // (its "$schema") against the command being run, matching `az deployment <scope>`'s
        // `validate_bicep_target_scope`. Derived scope orchestrators override this.
        protected virtual string DeploymentScope => "resourceGroup";

        protected static DeploymentMode ParseMode(string? mode) =>
            string.IsNullOrWhiteSpace(mode)
                ? DeploymentMode.Incremental
                : Enum.Parse<DeploymentMode>(mode, ignoreCase: true);

        protected object? BuildTemplate(out TemplateLink? templateLink)
        {
            templateLink = null;

            if (TemplateObject is not null)
            {
                return TemplateObject;
            }

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
                templateLink = new TemplateLink
                {
                    Id = TemplateSpecId,
                    QueryString = NormalizeQueryString(QueryString),
                };
                return null;
            }

            // Local file: read and inline. A .bicep source is compiled to ARM JSON first.
            string templatePath = TemplateFile!;
            if (IsBicepFile(templatePath))
            {
                EnsureFileExists(templatePath);
                string armJson = BicepCompiler.BuildTemplate(templatePath);
                JsonElement compiled = JsonSerializer.Deserialize<JsonElement>(armJson);
                ValidateBicepTargetScope(compiled);
                return compiled;
            }

            return ReadJsonFile(templatePath);
        }

        // Compiled Bicep carries its target scope in "$schema" (e.g. ".../managementGroupDeploymentTemplate.json#").
        // Reject it up front when it doesn't match this command's deployment scope, instead of
        // letting ARM fail later with a less specific error. Matches `az deployment <scope>
        // create`'s validate_bicep_target_scope. Only compiled Bicep is checked: hand-authored ARM
        // JSON templates don't reliably set "$schema" to a value reflecting intended scope.
        private void ValidateBicepTargetScope(JsonElement template)
        {
            if (template.ValueKind != JsonValueKind.Object
                || !template.TryGetProperty("$schema", out JsonElement schemaElement)
                || schemaElement.ValueKind != JsonValueKind.String)
            {
                return;
            }

            string? templateScope = ResolveSchemaTargetScope(schemaElement.GetString());
            if (templateScope is not null
                && !string.Equals(templateScope, DeploymentScope, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"The Bicep template's target scope '{templateScope}' does not match the deployment command's scope '{DeploymentScope}'.");
            }
        }

        private static string? ResolveSchemaTargetScope(string? schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                return null;
            }

            if (schema.Contains("managementGroupDeploymentTemplate.json", StringComparison.OrdinalIgnoreCase))
            {
                return "managementGroup";
            }
            if (schema.Contains("tenantDeploymentTemplate.json", StringComparison.OrdinalIgnoreCase))
            {
                return "tenant";
            }
            if (schema.Contains("subscriptionDeploymentTemplate.json", StringComparison.OrdinalIgnoreCase))
            {
                return "subscription";
            }
            if (schema.Contains("deploymentTemplate.json", StringComparison.OrdinalIgnoreCase))
            {
                return "resourceGroup";
            }

            return null;
        }

        protected object? BuildParameters(out ParametersLink? parametersLink)
        {
            parametersLink = null;

            if (!string.IsNullOrWhiteSpace(ParametersUri))
            {
                // ARM's ParametersLink has no dedicated queryString field (unlike TemplateLink),
                // so any SAS/query string must be embedded directly in the URI.
                string uri = string.IsNullOrWhiteSpace(QueryString)
                    ? ParametersUri
                    : AppendQueryString(ParametersUri, QueryString);
                parametersLink = new ParametersLink { Uri = uri };
                return null;
            }

            object? fileParameters = ParametersObject;
            if (fileParameters is null && !string.IsNullOrWhiteSpace(ParametersFile))
            {
                // A .bicepparam source is compiled to an ARM parameters file first.
                if (IsBicepParamFile(ParametersFile))
                {
                    EnsureFileExists(ParametersFile);
                    fileParameters = ReadBicepParamFile(ParametersFile);
                }
                else
                {
                    fileParameters = ReadParametersFile(ParametersFile);
                }
            }

            // No inline overlay: preserve the original file-or-null behavior.
            if (Parameter is not { Length: > 0 })
            {
                return fileParameters;
            }

            // Inline parameters present: start from the file's inner parameters object (the base)
            // and overlay each KEY=VALUE on top, so inline values win per key.
            JsonObject parameters;
            if (fileParameters is JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("The parameters file must contain a JSON object of parameters to combine with inline parameter values.");
                }
                parameters = JsonObject.Create(element)!;
            }
            else if (fileParameters is not null)
            {
                parameters = JsonSerializer.SerializeToNode(fileParameters, JsonOptions) as JsonObject
                    ?? throw new ArgumentException("The parameters object must serialize to a JSON object.");
            }
            else
            {
                parameters = new JsonObject();
            }

            foreach (string pair in Parameter)
            {
                (string key, JsonNode? value) = ParseInlineParameter(pair);
                // ARM parameter names are case-insensitive, but JsonObject's indexer is
                // case-sensitive. Reuse any existing same-named key (from the file or an earlier
                // inline token, matched case-insensitively) so the body never carries two keys
                // differing only by case. The existing/file casing is kept, the inline value
                // wins, and for duplicate inline keys the last one wins.
                string targetKey = FindExistingKeyIgnoreCase(parameters, key) ?? key;
                parameters[targetKey] = new JsonObject { ["value"] = value };
            }

            return parameters;
        }

        // Returns the canonical-cased validation level when valid, or null when the input is
        // empty (not supplied) or unrecognized. Callers reject unrecognized values up front.
        protected static string? NormalizeValidationLevel(string? validationLevel) =>
            string.IsNullOrWhiteSpace(validationLevel)
                ? null
                : validationLevel.Trim().ToLowerInvariant() switch
                {
                    "template" => "Template",
                    "provider" => "Provider",
                    "providernorbac" => "ProviderNoRbac",
                    _ => null,
                };

        // Trims a single leading '?' or '&' from a query string and returns null when empty/whitespace.
        // ARM's TemplateLink.queryString field stores the raw query WITHOUT a leading separator.
        protected static string? NormalizeQueryString(string? queryString)
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

        // Append a query string to a URI. ParametersLink has no dedicated queryString field, so the
        // query string must be embedded in the URI itself. A single leading '?' or '&' on the query
        // string is trimmed, then it is joined with '&' when the URI already has a query, else '?'.
        protected static string AppendQueryString(string uri, string queryString)
        {
            // A pathological query string of exactly "?" or "&" trims to empty; appending a
            // separator would leave a stray trailing '?'/'&', so return the URI unchanged.
            string? trimmed = NormalizeQueryString(queryString);
            if (trimmed is null)
            {
                return uri;
            }
            string separator = uri.Contains('?') ? "&" : "?";
            return uri + separator + trimmed;
        }

        // Find an existing key in 'parameters' that equals 'key' ignoring case, returning the
        // existing key (so its casing is preserved) or null when no such key exists.
        protected static string? FindExistingKeyIgnoreCase(JsonObject parameters, string key)
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

        // Parse a single inline "KEY=VALUE" token. The value is parsed as JSON when possible so
        // numbers, booleans, arrays, and objects become typed; otherwise it is treated as a string.
        protected static (string Key, JsonNode? Value) ParseInlineParameter(string pair)
        {
            if (string.IsNullOrWhiteSpace(pair))
            {
                throw new ArgumentException("Invalid inline parameter: the token is empty; expected KEY=VALUE.");
            }

            int separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException($"Invalid inline parameter '{pair}': expected KEY=VALUE with a non-empty key.");
            }

            string key = pair[..separator].Trim();
            if (key.Length == 0)
            {
                throw new ArgumentException($"Invalid inline parameter '{pair}': the key is empty.");
            }

            string raw = pair[(separator + 1)..];
            return (key, ParseTypedValue(raw));
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

        private static object ReadJsonFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}", path);
            }
            string content = File.ReadAllText(path);
            return JsonSerializer.Deserialize<JsonElement>(content);
        }

        // ARM parameters files are wrapped as { "$schema": ..., "contentVersion": ..., "parameters": { ... } }.
        // The deployment body expects the inner "parameters" object. Unwrap it when present.
        private static object ReadParametersFile(string path)
        {
            JsonElement root = (JsonElement)ReadJsonFile(path);
            return UnwrapParameters(root);
        }

        // Compile a .bicepparam file to ARM parameters JSON and unwrap to the inner "parameters" object.
        // 'bicep build-params --stdout' emits an envelope:
        //   { "parametersJson": "<stringified ARM params file>", "templateJson": ..., "templateSpecId": ... }
        // The ARM params file inside "parametersJson" has the standard { $schema, contentVersion, parameters } shape.
        private static object ReadBicepParamFile(string path)
        {
            string envelopeJson = BicepCompiler.BuildParams(path);
            using JsonDocument envelope = JsonDocument.Parse(envelopeJson);
            if (!envelope.RootElement.TryGetProperty("parametersJson", out JsonElement parametersJsonElement)
                || parametersJsonElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    "Unexpected 'bicep build-params --stdout' output: missing 'parametersJson' string.");
            }

            string parametersJson = parametersJsonElement.GetString()!;
            JsonElement root = JsonSerializer.Deserialize<JsonElement>(parametersJson);
            return UnwrapParameters(root);
        }

        private static object UnwrapParameters(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("parameters", out JsonElement inner))
            {
                return inner;
            }
            return root;
        }

        private static bool IsBicepFile(string path) =>
            path.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase);

        private static bool IsBicepParamFile(string path) =>
            path.EndsWith(".bicepparam", StringComparison.OrdinalIgnoreCase);

        private static void EnsureFileExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found: {path}", path);
            }
        }
    }
}
