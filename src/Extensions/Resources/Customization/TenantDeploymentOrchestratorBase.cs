// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public abstract class TenantDeploymentOrchestratorBase<TRequest, TResponse> : DeploymentOrchestratorBase<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    public string Location { get; set; } = string.Empty;

    public IDictionary<string, string>? Tags { get; set; }

    public string? DeploymentDebugLogLevel { get; set; }

    protected override void ValidateScopeInputs()
    {
        if (string.IsNullOrWhiteSpace(Location))
        {
            throw new ArgumentException("Location must be provided.");
        }
    }

    protected void PrepareAndValidate()
    {
        NormalizeParameterTokens();
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = DeriveDefaultDeploymentName();
        }

        Mode = "Incremental";
        ValidateCommonInputs();
    }

    // Mirrors Azure CLI's `_validate_deployment_name`/`_validate_deployment_name_with_template_specs`:
    // derive a name from the template file's basename, then the template URI's last path segment,
    // then the template spec's resource name, falling back to the literal "deployment1" only when
    // none of those sources is available.
    private string DeriveDefaultDeploymentName()
    {
        string? baseName = null;
        if (!string.IsNullOrWhiteSpace(TemplateFile))
        {
            baseName = Path.GetFileNameWithoutExtension(TemplateFile);
        }
        else if (!string.IsNullOrWhiteSpace(TemplateUri)
            && Uri.TryCreate(TemplateUri, UriKind.Absolute, out Uri? uri))
        {
            baseName = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
        }
        else if (!string.IsNullOrWhiteSpace(TemplateSpecId))
        {
            baseName = ExtractTemplateSpecName(TemplateSpecId);
        }

        return string.IsNullOrWhiteSpace(baseName) ? "deployment1" : baseName;
    }

    private static string? ExtractTemplateSpecName(string templateSpecId)
    {
        string[] segments = templateSpecId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int index = Array.FindIndex(
            segments, segment => string.Equals(segment, "templateSpecs", StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < segments.Length ? segments[index + 1] : null;
    }

    private void NormalizeParameterTokens()
    {
        if (Parameter is not { Length: > 0 })
        {
            return;
        }

        List<string> overlays = [];
        foreach (string token in Parameter)
        {
            if (token.StartsWith('@') && token.Length > 1)
            {
                if (ParametersFile is not null || ParametersObject is not null || ParametersUri is not null)
                {
                    throw new ArgumentException("Only one parameter file, object, or URI source can be provided.");
                }
                ParametersFile = token[1..];
                continue;
            }

            if (token.TrimStart().StartsWith('{'))
            {
                if (ParametersFile is not null || ParametersObject is not null || ParametersUri is not null)
                {
                    throw new ArgumentException("Only one parameter file, object, or URI source can be provided.");
                }
                ParametersObject = JsonSerializer.Deserialize<JsonElement>(token);
                continue;
            }

            if (Uri.TryCreate(token, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                if (ParametersFile is not null || ParametersObject is not null || ParametersUri is not null)
                {
                    throw new ArgumentException("Only one parameter file, object, or URI source can be provided.");
                }
                ParametersUri = token;
                continue;
            }

            overlays.Add(token);
        }

        Parameter = overlays.Count == 0 ? null : [.. overlays];
    }

    protected ScopedDeployment BuildDeployment()
    {
        object? template = BuildTemplate(out TemplateLink? templateLink);
        string? debugLevel = NormalizeDebugSetting(DeploymentDebugLogLevel);
        DeploymentProperties properties = new()
        {
            Mode = DeploymentMode.Incremental,
            Template = template,
            TemplateLink = templateLink,
            Parameters = BuildTypedParameters(template, out ParametersLink? parametersLink),
            ParametersLink = parametersLink,
            ValidationLevel = NormalizeValidationLevel(ValidationLevel) ?? "Provider",
            DebugSetting = debugLevel is null
                ? null
                : new DebugSetting { DetailLevel = debugLevel },
        };

        return new ScopedDeployment
        {
            Location = Location,
            Properties = properties,
            Tags = Tags,
        };
    }

    protected ScopedDeploymentWhatIf BuildWhatIf(string? resultFormat)
    {
        object? template = BuildTemplate(out TemplateLink? templateLink);
        return new ScopedDeploymentWhatIf
        {
            Location = Location,
            Properties = new DeploymentWhatIfProperties
            {
                Mode = DeploymentMode.Incremental,
                Template = template,
                TemplateLink = templateLink,
                Parameters = BuildTypedParameters(template, out ParametersLink? parametersLink),
                ParametersLink = parametersLink,
                ValidationLevel = NormalizeValidationLevel(ValidationLevel) ?? "Provider",
                WhatIfSettings = new DeploymentWhatIfSettings
                {
                    ResultFormat = NormalizeResultFormat(resultFormat),
                },
            },
        };
    }

    // Overlays inline KEY=VALUE parameter tokens using the template's declared parameter type
    // (string/securestring/bool/int/object/array/secureobject, following "$ref"), matching
    // Azure CLI's `_try_parse_key_value_object` instead of guessing the type from the token's
    // own JSON shape like the shared DeploymentOrchestratorBase.BuildParameters overlay does.
    // The shared file/URI/object routing in NormalizeParameterTokens (and the shared
    // BuildParameters base-parameter resolution) is reused unchanged.
    private object? BuildTypedParameters(object? template, out ParametersLink? parametersLink)
    {
        if (Parameter is not { Length: > 0 })
        {
            return BuildParameters(out parametersLink);
        }

        string[] tokens = Parameter;
        object? baseParameters;
        Parameter = null;
        try
        {
            baseParameters = BuildParameters(out parametersLink);
        }
        finally
        {
            Parameter = tokens;
        }

        // ARM rejects inline parameters combined with a parameters link; NormalizeParameterTokens
        // already enforces a single parameter source before this runs.
        if (parametersLink is not null)
        {
            return baseParameters;
        }

        JsonObject parameters = baseParameters switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.Object => JsonObject.Create(element)!,
            JsonObject existing => existing,
            null => new JsonObject(),
            object other => JsonSerializer.SerializeToNode(other, JsonOptions) as JsonObject
                ?? throw new ArgumentException("The parameters object must serialize to a JSON object."),
        };

        JsonObject? templateObject = AsJsonObject(template) ?? AsJsonObject(GetTemplateForParameterTyping());
        foreach (string token in tokens)
        {
            (string key, JsonNode? value) = ParseTemplateTypedParameter(token, templateObject);
            string targetKey = FindExistingKeyIgnoreCase(parameters, key) ?? key;
            parameters[targetKey] = new JsonObject { ["value"] = value };
        }

        return parameters;
    }

    private static JsonObject? AsJsonObject(object? value) => value switch
    {
        null => null,
        JsonObject obj => obj,
        JsonElement element => JsonObject.Create(element),
        _ => JsonSerializer.SerializeToNode(value) as JsonObject,
    };

    // Fetches the template body purely to read its parameter type declarations when the template
    // itself wasn't resolved locally (TemplateUri/TemplateSpecId defer the actual template
    // content to the service, unlike TemplateFile/TemplateObject). Mirrors Azure CLI resolving
    // the template object before interpreting KEY=VALUE tokens in `_try_parse_key_value_object`.
    private object? GetTemplateForParameterTyping()
    {
        if (!string.IsNullOrWhiteSpace(TemplateUri))
        {
            string uri = string.IsNullOrWhiteSpace(QueryString)
                ? TemplateUri
                : AppendQueryString(TemplateUri, QueryString);
            using HttpClient client = new();
            return JsonNode.Parse(client.GetStringAsync(uri).GetAwaiter().GetResult())
                ?? throw new ArgumentException($"The template URI '{TemplateUri}' returned an empty response.");
        }

        if (string.IsNullOrWhiteSpace(TemplateSpecId))
        {
            return null;
        }

        string separator = TemplateSpecId.Contains('?') ? "&" : "?";
        string uriString = TemplateSpecId.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? TemplateSpecId
            : $"https://management.azure.com{TemplateSpecId}";
        using HttpRequestMessage request = new(HttpMethod.Get, $"{uriString}{separator}api-version=2022-02-01");
        using HttpResponseMessage response = HttpPipeline.SendAsync(request, Publisher).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        JsonNode root = JsonNode.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult())
            ?? throw new InvalidOperationException($"Template spec '{TemplateSpecId}' returned an empty response.");
        return root["properties"]?["mainTemplate"]
            ?? throw new InvalidOperationException($"Template spec '{TemplateSpecId}' has no main template.");
    }

    private static (string Key, JsonNode? Value) ParseTemplateTypedParameter(string token, JsonObject? template)
    {
        int separator = token.IndexOf('=');
        if (separator <= 0)
        {
            throw new ArgumentException(
                $"Unable to parse parameter '{token}'. Expected a JSON object, file, URI, or KEY=VALUE.");
        }

        string key = token[..separator].Trim();
        string rawValue = token[(separator + 1)..];
        JsonObject? definitions = template?["parameters"] as JsonObject;
        JsonObject? parameterDefinition = definitions?[key] as JsonObject;
        if (definitions is not null && parameterDefinition is null)
        {
            string? matchedKey = definitions.Select(entry => entry.Key)
                .FirstOrDefault(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase));
            parameterDefinition = matchedKey is null ? null : definitions[matchedKey] as JsonObject;
            key = matchedKey ?? key;
        }

        if (definitions is not null && parameterDefinition is null)
        {
            throw new ArgumentException($"Unrecognized template parameter '{key}'.");
        }

        string? type = ResolveParameterType(parameterDefinition, template);
        JsonNode? value = type?.ToLowerInvariant() switch
        {
            "string" or "securestring" => JsonValue.Create(rawValue),
            "bool" => JsonValue.Create(bool.Parse(rawValue)),
            "int" => JsonValue.Create(long.Parse(rawValue, CultureInfo.InvariantCulture)),
            "object" or "secureobject" or "array" => JsonNode.Parse(rawValue),
            _ => ParseJsonOrString(rawValue),
        };
        return (key, value);
    }

    // Resolves a parameter definition's declared "type", following a single level of "$ref"
    // indirection (as emitted by Bicep-compiled templates) into the template's "definitions".
    private static string? ResolveParameterType(JsonObject? definition, JsonObject? template)
    {
        HashSet<string> visited = [];
        while (definition is not null)
        {
            if (definition["type"]?.GetValue<string>() is { } type)
            {
                return type;
            }
            if (definition["$ref"]?.GetValue<string>() is not { } reference
                || template is null
                || !visited.Add(reference)
                || !reference.StartsWith("#/", StringComparison.Ordinal))
            {
                return null;
            }

            JsonNode? current = template;
            foreach (string segment in reference[2..].Split('/'))
            {
                current = current?[segment.Replace("~1", "/").Replace("~0", "~")];
            }
            definition = current as JsonObject;
        }
        return null;
    }

    private static JsonNode? ParseJsonOrString(string value)
    {
        try
        {
            return JsonNode.Parse(value);
        }
        catch (JsonException)
        {
            return JsonValue.Create(value);
        }
    }

    protected static string NormalizeResultFormat(string? resultFormat) =>
        string.IsNullOrWhiteSpace(resultFormat)
            ? "FullResourcePayloads"
            : resultFormat.Trim().ToLowerInvariant() switch
            {
                "resourceidonly" => "ResourceIdOnly",
                "fullresourcepayloads" => "FullResourcePayloads",
                _ => throw new ArgumentException("ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'."),
            };

    private static string? NormalizeDebugSetting(string? detailLevel) =>
        string.IsNullOrWhiteSpace(detailLevel)
            ? null
            : detailLevel.Trim().ToLowerInvariant() switch
            {
                "requestcontent" => "RequestContent",
                "responsecontent" => "ResponseContent",
                "all" => "RequestContent,ResponseContent",
                "none" => null,
                _ => throw new ArgumentException(
                    "DeploymentDebugLogLevel must be one of 'RequestContent', 'ResponseContent', 'All', or 'None'."),
            };

    protected T? Deserialize<T>(string rawBody) =>
        JsonSerializer.Deserialize<T>(rawBody, JsonOptions);

    protected static void ThrowIfError(ErrorResponse? error, string operation)
    {
        if (error is null)
        {
            return;
        }

        StringBuilder message = new($"{operation} failed.");
        AppendError(message, error, 1);
        throw new InvalidOperationException(message.ToString());
    }

    private static void AppendError(StringBuilder message, ErrorResponse error, int depth)
    {
        message.AppendLine()
            .Append(' ', depth * 2)
            .Append(string.IsNullOrWhiteSpace(error.Code) ? "(no code)" : error.Code)
            .Append(": ")
            .Append(string.IsNullOrWhiteSpace(error.Message) ? "(no message)" : error.Message);

        if (error.Details is null)
        {
            return;
        }

        foreach (ErrorResponse detail in error.Details)
        {
            AppendError(message, detail, depth + 1);
        }
    }
}
