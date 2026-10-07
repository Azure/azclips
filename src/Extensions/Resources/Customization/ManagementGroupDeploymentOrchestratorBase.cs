// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public abstract class ManagementGroupDeploymentOrchestratorBase<TRequest, TResponse>
    : DeploymentOrchestratorBase<TRequest, TResponse>
    where TRequest : class
    where TResponse : class
{
    public string ManagementGroupId { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public IDictionary<string, string>? Tags { get; set; }

    public string? DeploymentDebugLogLevel { get; set; }

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

    protected override string DeploymentScope => "managementGroup";

    protected void PrepareAndValidate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = DeriveDefaultDeploymentName();
        }

        ValidateManagementGroupInputs();
    }

    // Azure CLI default: file -> template-URI -> template-spec name -> "deployment1".
    // Azure PowerShell default: file -> template-URI -> random GUID (no template-spec/"deployment1" fallback).
    private string DeriveDefaultDeploymentName()
    {
        if (!string.IsNullOrWhiteSpace(TemplateFile))
        {
            return Path.GetFileNameWithoutExtension(TemplateFile);
        }

        if (!string.IsNullOrWhiteSpace(TemplateUri)
            && Uri.TryCreate(TemplateUri, UriKind.Absolute, out Uri? templateUri))
        {
            string segment = templateUri.Segments.Length > 0 ? templateUri.Segments[^1].TrimEnd('/') : string.Empty;
            if (!string.IsNullOrWhiteSpace(segment))
            {
                return Path.GetFileNameWithoutExtension(segment);
            }
        }

        if (AzclipsServiceProvider.GetRequiredService<ClientType>() != ClientType.CLI)
        {
            return Guid.NewGuid().ToString();
        }

        if (!string.IsNullOrWhiteSpace(TemplateSpecId))
        {
            string? specName = ExtractTemplateSpecName(TemplateSpecId);
            if (!string.IsNullOrWhiteSpace(specName))
            {
                return specName;
            }
        }

        return "deployment1";
    }

    private static string? ExtractTemplateSpecName(string templateSpecId)
    {
        string trimmed = templateSpecId.TrimEnd('/');
        string[] segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? null : segments[^1];
    }

    private void ValidateManagementGroupInputs()
    {
        string[]? parameterTokens = Parameter;
        Parameter = null;
        try
        {
            ValidateCommonInputs();
        }
        finally
        {
            Parameter = parameterTokens;
        }

        foreach (string token in parameterTokens ?? [])
        {
            if (!token.StartsWith('@')
                && !token.TrimStart().StartsWith('{')
                && !File.Exists(token)
                && (!Uri.TryCreate(token, UriKind.Absolute, out Uri? uri)
                    || uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                && token.IndexOf('=') <= 0)
            {
                throw new ArgumentException(
                    $"Unable to parse parameter '{token}'. Expected a JSON object, file, URI, or KEY=VALUE.");
            }
        }
    }

    // `forceIncrementalMode` matches upstream: `az deployment mg validate` hardcodes
    // mode='Incremental' and never accepts a `mode` argument at all (unlike `create`, which
    // forwards the caller's `--mode` into the same body used for its own preflight validate).
    protected ScopedDeployment BuildDeployment(bool forceIncrementalMode = false)
    {
        object? template = BuildTemplate(out TemplateLink? templateLink);
        string? debugLevel = NormalizeDebugSetting(DeploymentDebugLogLevel);
        DeploymentProperties properties = new()
        {
            Mode = forceIncrementalMode ? DeploymentMode.Incremental : ParseMode(Mode),
            Template = template,
            TemplateLink = templateLink,
            Parameters = BuildParameters(GetTemplateForParameterTyping(template), out ParametersLink? parametersLink),
            ParametersLink = parametersLink,
            ValidationLevel = NormalizeValidationLevel(ValidationLevel),
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

    // `forceIncrementalMode` matches upstream: every what-if preview (the standalone `what-if`
    // command and the `--confirm-with-what-if`/`--what-if` preview inside `create`) hardcodes
    // DeploymentMode.incremental and never forwards the caller's `--mode`
    // (`_what_if_deploy_arm_template_at_management_group_core` takes no `mode` argument).
    protected ScopedDeploymentWhatIf BuildWhatIf(string? resultFormat, bool forceIncrementalMode = true)
    {
        object? template = BuildTemplate(out TemplateLink? templateLink);
        return new()
        {
            Location = Location,
            Properties = new DeploymentWhatIfProperties
            {
                Mode = forceIncrementalMode ? DeploymentMode.Incremental : ParseMode(Mode),
                Template = template,
                TemplateLink = templateLink,
                Parameters = BuildParameters(GetTemplateForParameterTyping(template), out ParametersLink? parametersLink),
                ParametersLink = parametersLink,
                ValidationLevel = NormalizeValidationLevel(ValidationLevel),
                WhatIfSettings = new DeploymentWhatIfSettings
                {
                    ResultFormat = NormalizeResultFormat(resultFormat),
                },
            },
        };
    }

    private object? BuildParameters(object? template, out ParametersLink? parametersLink)
    {
        if (Parameter is not { Length: > 0 })
        {
            return BuildParameters(out parametersLink);
        }

        parametersLink = null;
        JsonObject parameters = new();
        MergeParameterSource(parameters, NormalizeParametersObject(ParametersObject));
        if (!string.IsNullOrWhiteSpace(ParametersFile))
        {
            MergeParameterSource(parameters, ReadParameterSource(ParametersFile));
        }
        if (!string.IsNullOrWhiteSpace(ParametersUri))
        {
            string uri = string.IsNullOrWhiteSpace(QueryString)
                ? ParametersUri
                : AppendQueryString(ParametersUri, QueryString);
            MergeParameterSource(parameters, ReadParameterSource(uri));
        }

        foreach (string token in Parameter ?? [])
        {
            object? source = TryReadParameterSource(token);
            if (source is not null)
            {
                MergeParameterSource(parameters, source);
                continue;
            }

            (string key, JsonNode? value) = ParseTemplateParameter(token, template);
            string targetKey = FindExistingKeyIgnoreCase(parameters, key) ?? key;
            parameters[targetKey] = new JsonObject { ["value"] = value };
        }

        return parameters.Count == 0 ? null : parameters;
    }

    private static object? TryReadParameterSource(string token)
    {
        string candidate = token.StartsWith('@') && token.Length > 1 ? token[1..] : token;
        if (token.StartsWith('@')
            || File.Exists(candidate)
            || Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return ReadParameterSource(candidate);
        }

        if (token.TrimStart().StartsWith('{'))
        {
            return JsonNode.Parse(token)
                ?? throw new ArgumentException("The parameter JSON source is empty.");
        }

        return null;
    }

    private static object ReadParameterSource(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            using HttpClient client = new();
            return JsonNode.Parse(client.GetStringAsync(uri).GetAwaiter().GetResult())
                ?? throw new ArgumentException($"The parameter URI '{source}' returned an empty response.");
        }

        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Parameter file not found: {source}", source);
        }

        if (source.EndsWith(".bicepparam", StringComparison.OrdinalIgnoreCase))
        {
            string envelopeJson = BicepCompiler.BuildParams(source);
            using JsonDocument envelope = JsonDocument.Parse(envelopeJson);
            if (!envelope.RootElement.TryGetProperty("parametersJson", out JsonElement parametersJson)
                || parametersJson.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    "Unexpected 'bicep build-params --stdout' output: missing 'parametersJson' string.");
            }
            return JsonNode.Parse(parametersJson.GetString()!)
                ?? throw new InvalidOperationException($"Bicep parameter file '{source}' produced no parameters.");
        }

        return JsonNode.Parse(File.ReadAllText(source))
            ?? throw new ArgumentException($"The parameter file '{source}' is empty.");
    }

    private object? GetTemplateForParameterTyping(object? template)
    {
        if (template is not null)
        {
            return template;
        }

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
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"{uriString}{separator}api-version=2022-02-01");
        using HttpResponseMessage response =
            HttpPipeline.SendAsync(request, Publisher).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        JsonNode root = JsonNode.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult())
            ?? throw new InvalidOperationException($"Template spec '{TemplateSpecId}' returned an empty response.");
        return root["properties"]?["mainTemplate"]
            ?? throw new InvalidOperationException($"Template spec '{TemplateSpecId}' has no main template.");
    }

    private static void MergeParameterSource(JsonObject target, object? source)
    {
        if (source is null)
        {
            return;
        }

        JsonObject sourceObject = source as JsonObject
            ?? JsonSerializer.SerializeToNode(source) as JsonObject
            ?? throw new ArgumentException("A parameter source must contain a JSON object.");
        if (sourceObject["parameters"] is JsonObject wrapped)
        {
            sourceObject = wrapped;
        }

        foreach ((string key, JsonNode? value) in sourceObject)
        {
            string targetKey = FindExistingKeyIgnoreCase(target, key) ?? key;
            target[targetKey] = value?.DeepClone();
        }
    }

    private static (string Key, JsonNode? Value) ParseTemplateParameter(string token, object? template)
    {
        int separator = token.IndexOf('=');
        if (separator <= 0)
        {
            throw new ArgumentException($"Unable to parse parameter '{token}'. Expected a JSON object, file, URI, or KEY=VALUE.");
        }

        string key = token[..separator].Trim();
        string rawValue = token[(separator + 1)..];
        JsonObject? templateObject = template as JsonObject
            ?? JsonSerializer.SerializeToNode(template) as JsonObject;
        JsonObject? parameterDefinition = templateObject?["parameters"]?[key] as JsonObject;
        if (templateObject?["parameters"] is JsonObject definitions && parameterDefinition is null)
        {
            string? matchedKey = definitions.Select(item => item.Key)
                .FirstOrDefault(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase));
            parameterDefinition = matchedKey is null ? null : definitions[matchedKey] as JsonObject;
            key = matchedKey ?? key;
        }

        if (templateObject?["parameters"] is not null && parameterDefinition is null)
        {
            throw new ArgumentException($"Unrecognized template parameter '{key}'.");
        }

        string? type = ResolveParameterType(parameterDefinition, templateObject);
        JsonNode? value = type?.ToLowerInvariant() switch
        {
            "string" or "securestring" => JsonValue.Create(rawValue),
            "bool" => JsonValue.Create(string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase)),
            "int" => JsonValue.Create(long.Parse(rawValue, System.Globalization.CultureInfo.InvariantCulture)),
            "object" or "secureobject" or "array" => JsonNode.Parse(rawValue),
            _ => JsonValue.Create(rawValue),
        };
        return (key, value);
    }

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

    protected static string NormalizeResultFormat(string? resultFormat) =>
        string.IsNullOrWhiteSpace(resultFormat)
            ? "FullResourcePayloads"
            : resultFormat.Trim().ToLowerInvariant() switch
            {
                "resourceidonly" => "ResourceIdOnly",
                "fullresourcepayloads" => "FullResourcePayloads",
                _ => throw new ArgumentException(
                    "ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'."),
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
