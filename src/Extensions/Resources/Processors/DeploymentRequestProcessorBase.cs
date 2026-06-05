using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Microsoft.Azure.Cirrus.Extensions.Resources;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
{
    /// <summary>
    /// Shared base for resource-group-scoped deployment request processors
    /// (deployment <c>create</c> and <c>what-if</c>). Owns the template-source,
    /// parameters-source, mode, and inline-parameter argument surface plus the
    /// validation, template/parameters building, and file/bicep readers that both
    /// commands need. Command-specific members (rollback, validation-level, no-wait,
    /// result-format, the request body shape, and the HTTP verb) live on the derived
    /// processors.
    /// </summary>
    public abstract class DeploymentRequestProcessorBase<TResponse> : ResourcesProcessorBase<TResponse> where TResponse : class
    {
        protected const string ApiVersion = "2025-04-01";
        private static readonly Regex s_deploymentNamePattern = new("^[-\\w\\._\\(\\)]+$", RegexOptions.Compiled);

        [ArgumentInfo(
        Required = true,
        Description = @"The name of the resource group to deploy into.",
        SerializedName = @"ResourceGroupName")]
        public string ResourceGroupName { get; set; } = string.Empty;

        [ArgumentInfo(
        Required = true,
        Description = @"The name of the deployment.",
        SerializedName = @"Name")]
        public string Name { get; set; } = string.Empty;

        [ArgumentInfo(
        Required = false,
        Description = @"The URI of the ARM template to deploy.",
        SerializedName = @"TemplateUri")]
        public string? TemplateUri { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The path to a local ARM template file to deploy inline.",
        SerializedName = @"TemplateFile")]
        public string? TemplateFile { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The resource id of a Template Spec to deploy.",
        SerializedName = @"TemplateSpecId")]
        public string? TemplateSpecId { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The query string (such as a SAS token) to use with the template URI and/or the parameters URI.",
        SerializedName = @"QueryString")]
        public string? QueryString { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The URI of a parameters file.",
        SerializedName = @"ParametersUri")]
        public string? ParametersUri { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The path to a local parameters file to deploy inline.",
        SerializedName = @"ParametersFile")]
        public string? ParametersFile { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The deployment mode. Incremental (default) or Complete.",
        SerializedName = @"Mode")]
        public string? Mode { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Inline deployment parameter(s) in KEY=VALUE form. Repeatable. Each value is parsed as JSON when possible (so count=3 becomes a number and flag=true a bool) and otherwise treated as a string; quote a value to force a string (e.g. zip=\""01234\""). Inline values overlay same-named keys from a parameters file. Cannot be combined with ParametersUri.",
        SerializedName = @"Parameter")]
        public string[]? Parameter { get; set; }

        // Validates the shared template/parameters/mode/inline rules. Derived processors override
        // this to add command-specific validation (calling base first).
        protected virtual void ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(ResourceGroupName))
            {
                throw new ArgumentException("ResourceGroupName must be provided.");
            }
            if (string.IsNullOrWhiteSpace(Name) || Name.Length > 64 || !s_deploymentNamePattern.IsMatch(Name))
            {
                throw new ArgumentException("Deployment name must be 1-64 characters and match the pattern ^[-\\w\\._\\(\\)]+$.");
            }

            int templateSources = (string.IsNullOrWhiteSpace(TemplateUri) ? 0 : 1)
                                 + (string.IsNullOrWhiteSpace(TemplateFile) ? 0 : 1)
                                 + (string.IsNullOrWhiteSpace(TemplateSpecId) ? 0 : 1);
            if (templateSources != 1)
            {
                throw new ArgumentException("Exactly one of TemplateUri, TemplateFile, or TemplateSpecId must be provided.");
            }

            int parameterSources = (string.IsNullOrWhiteSpace(ParametersUri) ? 0 : 1)
                                  + (string.IsNullOrWhiteSpace(ParametersFile) ? 0 : 1);
            if (parameterSources > 1)
            {
                throw new ArgumentException("Provide at most one of ParametersUri or ParametersFile.");
            }

            // ARM allows EITHER an inline "parameters" object OR a "parametersLink", never both.
            if (Parameter is { Length: > 0 } && !string.IsNullOrWhiteSpace(ParametersUri))
            {
                throw new ArgumentException("Inline -Parameter cannot be combined with -ParametersUri; ARM does not allow inline parameters with a parameters link. Use -ParametersFile or remove -ParametersUri.");
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
        }

        protected static DeploymentMode ParseMode(string? mode) =>
            string.IsNullOrWhiteSpace(mode)
                ? DeploymentMode.Incremental
                : Enum.Parse<DeploymentMode>(mode, ignoreCase: true);

        protected object? BuildTemplate(out TemplateLink? templateLink)
        {
            templateLink = null;

            if (!string.IsNullOrWhiteSpace(TemplateUri))
            {
                templateLink = new TemplateLink
                {
                    Uri = TemplateUri,
                    QueryString = string.IsNullOrWhiteSpace(QueryString) ? null : QueryString,
                };
                return null;
            }

            if (!string.IsNullOrWhiteSpace(TemplateSpecId))
            {
                templateLink = new TemplateLink
                {
                    Id = TemplateSpecId,
                    QueryString = string.IsNullOrWhiteSpace(QueryString) ? null : QueryString,
                };
                return null;
            }

            // Local file: read and inline. A .bicep source is compiled to ARM JSON first.
            string templatePath = TemplateFile!;
            if (IsBicepFile(templatePath))
            {
                EnsureFileExists(templatePath);
                string armJson = BicepCompiler.BuildTemplate(templatePath);
                return JsonSerializer.Deserialize<JsonElement>(armJson);
            }

            return ReadJsonFile(templatePath);
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

            object? fileParameters = null;
            if (!string.IsNullOrWhiteSpace(ParametersFile))
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
                    throw new ArgumentException("The parameters file must contain a JSON object of parameters to combine with inline -Parameter values.");
                }
                parameters = JsonObject.Create(element)!;
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

        // Append a query string to a URI. ParametersLink has no dedicated queryString field, so the
        // query string must be embedded in the URI itself. A single leading '?' or '&' on the query
        // string is trimmed, then it is joined with '&' when the URI already has a query, else '?'.
        protected static string AppendQueryString(string uri, string queryString)
        {
            string trimmed = queryString.Length > 0 && (queryString[0] == '?' || queryString[0] == '&')
                ? queryString[1..]
                : queryString;
            string separator = uri.Contains('?') ? "&" : "?";
            return uri + separator + trimmed;
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
