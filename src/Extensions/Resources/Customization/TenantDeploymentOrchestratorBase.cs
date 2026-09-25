// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
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
            Name = !string.IsNullOrWhiteSpace(TemplateFile)
                ? Path.GetFileNameWithoutExtension(TemplateFile)
                : DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        }

        Mode = "Incremental";
        ValidateCommonInputs();
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
        DeploymentProperties properties = new()
        {
            Mode = DeploymentMode.Incremental,
            Template = BuildTemplate(out TemplateLink? templateLink),
            TemplateLink = templateLink,
            Parameters = BuildParameters(out ParametersLink? parametersLink),
            ParametersLink = parametersLink,
            ValidationLevel = NormalizeValidationLevel(ValidationLevel) ?? "Provider",
            DebugSetting = string.IsNullOrWhiteSpace(DeploymentDebugLogLevel)
                ? null
                : new DebugSetting { DetailLevel = DeploymentDebugLogLevel },
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
        return new ScopedDeploymentWhatIf
        {
            Location = Location,
            Properties = new DeploymentWhatIfProperties
            {
                Mode = DeploymentMode.Incremental,
                Template = BuildTemplate(out TemplateLink? templateLink),
                TemplateLink = templateLink,
                Parameters = BuildParameters(out ParametersLink? parametersLink),
                ParametersLink = parametersLink,
                ValidationLevel = NormalizeValidationLevel(ValidationLevel) ?? "Provider",
                WhatIfSettings = new DeploymentWhatIfSettings
                {
                    ResultFormat = NormalizeResultFormat(resultFormat),
                },
            },
        };
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
