// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// PowerShell-only <see cref="TestSubscriptionDeploymentProcessor_Orchestrator"/> that mirrors Az.Resources
    /// Test-AzDeployment: a failed validation is output as a list of <see cref="PSResourceManagerError"/> instead of
    /// failing, and a passing one outputs nothing but writes its diagnostics as a warning unless
    /// <see cref="SuppressDiagnostics"/> is set. Only the PowerShell command table routes here, so the CLI keeps
    /// raising a failed validation and printing the full result of a passing one.
    /// </summary>
    public class TestSubscriptionDeploymentProcessor_PSOutput : TestSubscriptionDeploymentProcessor_Orchestrator
    {
        /// <summary>When set, the diagnostics of a passing validation are not written as a warning.</summary>
        public bool SuppressDiagnostics { get; set; }

        protected override async Task WriteValidationResult(DeploymentValidateResult result)
        {
            if (result.Error is not null)
            {
                // Az.Resources writes the errors as one list object.
                await WriteOutput(new List<PSResourceManagerError> { PSResourceManagerError.From(result.Error) });
                return;
            }

            await Publisher.Publish(Events.Verbose, new EventData { Message = "Template is valid." });

            if (!SuppressDiagnostics && FormatDiagnostics(result.Properties?.Diagnostics) is { } diagnostics)
            {
                await Publisher.Publish(Events.Warning, new EventData { Message = diagnostics });
            }
        }

        // Az.Resources' what-if diagnostics format, without the colors.
        private static string? FormatDiagnostics(object? diagnostics)
        {
            if (diagnostics is null)
            {
                return null;
            }

            JsonElement list = diagnostics is JsonElement element ? element : JsonSerializer.SerializeToElement(diagnostics);
            if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
            {
                return null;
            }

            var builder = new StringBuilder().AppendLine().AppendLine();
            builder.Append($"Diagnostics ({list.GetArrayLength()}): ").AppendLine();
            foreach (JsonElement diagnostic in list.EnumerateArray())
            {
                builder.Append($"({Read(diagnostic, "target")}) {Read(diagnostic, "message")} ({Read(diagnostic, "code")})").AppendLine();
            }
            return builder.ToString();
        }

        private static string? Read(JsonElement diagnostic, string name) =>
            diagnostic.ValueKind == JsonValueKind.Object && diagnostic.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
