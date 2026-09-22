// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public interface IManagementGroupDeploymentInput
{
    string ManagementGroupId { get; set; }
    string Name { get; set; }
    string Location { get; set; }
    object? Template { get; set; }
    string? TemplateUri { get; set; }
    string? TemplateSpecId { get; set; }
    string? QueryString { get; set; }
    IDictionary<string, DeploymentParameter>? Parameters { get; set; }
    string? ParametersUri { get; set; }
    DeploymentMode Mode { get; set; }
    string? ValidationLevel { get; set; }
    string? DeploymentDebugLogLevel { get; set; }
    object? TemplateObject { get; set; }
    object? TemplateParameterObject { get; set; }
    string? ModeInput { get; set; }
    string? TemplateFile { get; set; }
    string? ParametersFile { get; set; }
    string[]? Parameter { get; set; }
}

internal static class ManagementGroupDeploymentPreparation
{
    internal static void Prepare(IManagementGroupDeploymentInput input, JsonSerializerOptions jsonOptions)
    {
        if (input.TemplateObject is not null)
        {
            input.Template = NormalizePowerShellObject(input.TemplateObject);
        }

        if (!string.IsNullOrWhiteSpace(input.ModeInput))
        {
            if (int.TryParse(input.ModeInput, out _)
                || !Enum.TryParse(input.ModeInput, true, out DeploymentMode mode)
                || !Enum.IsDefined(typeof(DeploymentMode), mode))
            {
                throw new ArgumentException("Mode must be Incremental or Complete.");
            }
            input.Mode = mode;
        }

        if (input.TemplateParameterObject is IDictionary parameterObject)
        {
            input.Parameters = parameterObject.Keys.Cast<object>().ToDictionary(
                key => key.ToString()!,
                key => parameterObject[key] is DeploymentParameter parameter
                    ? parameter
                    : new DeploymentParameter { Value = NormalizePowerShellObject(parameterObject[key]) },
                StringComparer.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(input.ManagementGroupId))
        {
            throw new ArgumentException("ManagementGroupId must be provided.");
        }

        if (string.IsNullOrWhiteSpace(input.Location))
        {
            throw new ArgumentException("Location must be provided.");
        }

        if (input.Parameter is { Length: > 0 })
        {
            List<string> inlineParameters = [];
            foreach (string item in input.Parameter)
            {
                if (item.StartsWith('@') && string.IsNullOrWhiteSpace(input.ParametersFile))
                {
                    input.ParametersFile = item[1..];
                }
                else if (Uri.TryCreate(item, UriKind.Absolute, out Uri? parameterUri)
                    && parameterUri.Scheme is "http" or "https"
                    && string.IsNullOrWhiteSpace(input.ParametersUri))
                {
                    input.ParametersUri = item;
                }
                else if (item.TrimStart().StartsWith('{') && input.Parameters is null)
                {
                    input.Parameters = JsonSerializer.Deserialize<Dictionary<string, DeploymentParameter>>(item, jsonOptions);
                }
                else if (item.Contains('='))
                {
                    inlineParameters.Add(item);
                }
                else
                {
                    throw new ArgumentException(
                        $"Invalid parameters input '{item}'; expected @file, URI, JSON object, or KEY=VALUE.");
                }
            }
            input.Parameter = [.. inlineParameters];
        }

        string? bicepParametersOutput = null;
        if (input.ParametersFile?.EndsWith(".bicepparam", StringComparison.OrdinalIgnoreCase) == true)
        {
            bicepParametersOutput = BicepCompiler.BuildParams(input.ParametersFile);
            using JsonDocument envelope = JsonDocument.Parse(bicepParametersOutput);
            if (input.Template is null && string.IsNullOrWhiteSpace(input.TemplateFile)
                && string.IsNullOrWhiteSpace(input.TemplateUri) && string.IsNullOrWhiteSpace(input.TemplateSpecId))
            {
                if (envelope.RootElement.TryGetProperty("templateJson", out JsonElement templateJson)
                    && templateJson.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(templateJson.GetString()))
                {
                    input.Template = JsonSerializer.Deserialize<JsonElement>(templateJson.GetString()!);
                }
                else if (envelope.RootElement.TryGetProperty("templateSpecId", out JsonElement templateSpecId)
                    && templateSpecId.ValueKind == JsonValueKind.String)
                {
                    input.TemplateSpecId = templateSpecId.GetString();
                }
            }
        }

        int templateSources = (input.Template is null ? 0 : 1)
            + (string.IsNullOrWhiteSpace(input.TemplateFile) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(input.TemplateUri) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(input.TemplateSpecId) ? 0 : 1);
        if (templateSources != 1)
        {
            throw new ArgumentException(
                "Exactly one template source must be provided: inline template object, template file, template URI, or template spec.");
        }

        if (!string.IsNullOrWhiteSpace(input.QueryString) && string.IsNullOrWhiteSpace(input.TemplateUri))
        {
            throw new ArgumentException("QueryString requires TemplateUri.");
        }

        int parameterSources = (input.Parameters is null ? 0 : 1)
            + (string.IsNullOrWhiteSpace(input.ParametersFile) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(input.ParametersUri) ? 0 : 1);
        if (parameterSources > 1)
        {
            throw new ArgumentException("Provide at most one of Parameters, ParametersFile, or ParametersUri.");
        }

        if (input.Parameter is { Length: > 0 } && !string.IsNullOrWhiteSpace(input.ParametersUri))
        {
            throw new ArgumentException("Inline parameters cannot be combined with ParametersUri.");
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            input.Name = string.IsNullOrWhiteSpace(input.TemplateFile)
                ? DateTime.UtcNow.ToString("yyyyMMddHHmmss")
                : Path.GetFileNameWithoutExtension(input.TemplateFile);
        }

        if (input.Name.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(input.Name, @"^[-\w\._\(\)]+$"))
        {
            throw new ArgumentException("Deployment name must be 1-64 characters and match ^[-\\w\\._\\(\\)]+$.");
        }

        if (!string.IsNullOrWhiteSpace(input.ValidationLevel)
            && !new[] { "Template", "Provider", "ProviderNoRbac" }.Contains(input.ValidationLevel, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("ValidationLevel must be Template, Provider, or ProviderNoRbac.");
        }

        input.ValidationLevel = input.ValidationLevel?.Trim().ToLowerInvariant() switch
        {
            "template" => "Template",
            "provider" => "Provider",
            "providernorbac" => "ProviderNoRbac",
            _ => null,
        };

        input.DeploymentDebugLogLevel = input.DeploymentDebugLogLevel?.Trim().ToLowerInvariant() switch
        {
            null or "" or "none" => null,
            "requestcontent" => "requestContent",
            "responsecontent" => "responseContent",
            "all" => "requestContent,responseContent",
            _ => throw new ArgumentException("DeploymentDebugLogLevel must be None, RequestContent, ResponseContent, or All."),
        };

        if (!string.IsNullOrWhiteSpace(input.TemplateFile))
        {
            if (!File.Exists(input.TemplateFile))
            {
                throw new FileNotFoundException($"Template file not found: {input.TemplateFile}", input.TemplateFile);
            }

            string json = input.TemplateFile.EndsWith(".bicep", StringComparison.OrdinalIgnoreCase)
                ? BicepCompiler.BuildTemplate(input.TemplateFile)
                : File.ReadAllText(input.TemplateFile);
            input.Template = JsonSerializer.Deserialize<JsonElement>(json);
        }

        if (!string.IsNullOrWhiteSpace(input.QueryString))
        {
            input.QueryString = input.QueryString.Trim().TrimStart('?', '&');
        }

        Dictionary<string, DeploymentParameter> parameters = input.Parameters is null
            ? new(StringComparer.OrdinalIgnoreCase)
            : new(input.Parameters, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(input.ParametersFile))
        {
            string json;
            if (bicepParametersOutput is not null)
            {
                json = ExtractBicepParametersJson(bicepParametersOutput);
            }
            else
            {
                if (!File.Exists(input.ParametersFile))
                {
                    throw new FileNotFoundException($"Parameters file not found: {input.ParametersFile}", input.ParametersFile);
                }
                json = File.ReadAllText(input.ParametersFile);
            }

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement parameterRoot = document.RootElement.TryGetProperty("parameters", out JsonElement inner)
                ? inner
                : document.RootElement;
            parameters = JsonSerializer.Deserialize<Dictionary<string, DeploymentParameter>>(
                parameterRoot.GetRawText(), jsonOptions) ?? new(StringComparer.OrdinalIgnoreCase);
            parameters = new(parameters, StringComparer.OrdinalIgnoreCase);
        }

        if (input.Parameter is { Length: > 0 })
        {
            foreach (string item in input.Parameter)
            {
                int separator = item.IndexOf('=');
                if (separator < 1)
                {
                    throw new ArgumentException($"Invalid inline parameter '{item}'; expected KEY=VALUE.");
                }

                string key = item[..separator].Trim();
                if (key.Length == 0)
                {
                    throw new ArgumentException($"Invalid inline parameter '{item}'; the key is empty.");
                }

                string value = item[(separator + 1)..];
                object parsedValue;
                if (value.StartsWith('@'))
                {
                    string path = value[1..];
                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException($"Inline parameter file not found: {path}", path);
                    }
                    parsedValue = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path));
                }
                else
                {
                    try
                    {
                        parsedValue = JsonSerializer.Deserialize<JsonElement>(value);
                    }
                    catch (JsonException)
                    {
                        parsedValue = value;
                    }
                }

                parameters[key] = new DeploymentParameter { Value = parsedValue };
            }
        }

        input.Parameters = parameters.Count == 0 ? null : parameters;
    }

    private static object? NormalizePowerShellObject(object? value)
    {
        if (value is IDictionary dictionary)
        {
            Dictionary<string, object?> normalized = new(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                string key = entry.Key?.ToString()
                    ?? throw new ArgumentException("PowerShell hashtable keys cannot be null.");
                normalized[key] = NormalizePowerShellObject(entry.Value);
            }
            return normalized;
        }

        if (value is IEnumerable enumerable and not string)
        {
            return enumerable.Cast<object?>().Select(NormalizePowerShellObject).ToArray();
        }

        return value;
    }

    private static string ExtractBicepParametersJson(string output)
    {
        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("parametersJson", out JsonElement parametersJson))
        {
            return parametersJson.ValueKind switch
            {
                JsonValueKind.String => parametersJson.GetString()!,
                JsonValueKind.Object => parametersJson.GetRawText(),
                _ => throw new InvalidOperationException(
                    "Bicep parametersJson must be a JSON string or object."),
            };
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("parameters", out _))
        {
            return root.GetRawText();
        }

        throw new InvalidOperationException(
            "Bicep output must contain parametersJson or be an ARM parameters document.");
    }

    internal static string BuildArmErrorMessage(string operation, ErrorResponse? error)
    {
        StringBuilder builder = new($"{operation} failed.");
        AppendError(builder, error, 1);
        return builder.ToString();
    }

    private static void AppendError(StringBuilder builder, ErrorResponse? error, int indent)
    {
        if (error is null)
        {
            return;
        }

        string code = string.IsNullOrWhiteSpace(error.Code) ? "(no code)" : error.Code;
        string message = string.IsNullOrWhiteSpace(error.Message) ? "(no message)" : error.Message;
        builder.Append(Environment.NewLine)
            .Append(' ', indent * 2)
            .Append(code)
            .Append(": ")
            .Append(message);
        if (!string.IsNullOrWhiteSpace(error.Target))
        {
            builder.Append(" (target: ").Append(error.Target).Append(')');
        }

        if (error.Details is not null)
        {
            foreach (ErrorResponse detail in error.Details)
            {
                AppendError(builder, detail, indent + 1);
            }
        }
    }

}
