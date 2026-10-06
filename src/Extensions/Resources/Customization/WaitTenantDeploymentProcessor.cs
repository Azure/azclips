// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using DevLab.JmesPath;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class WaitTenantDeploymentProcessor : ResourcesProcessorBase<object, object>
{
    public string Name { get; set; } = string.Empty;

    public bool Created { get; set; }

    public bool Deleted { get; set; }

    public bool Exists { get; set; }

    public bool Updated { get; set; }

    public string? Custom { get; set; }

    public int Interval { get; set; } = 30;

    public int Timeout { get; set; } = 3600;

    public override async Task ProcessAsync()
    {
        int conditions = (Created ? 1 : 0) + (Deleted ? 1 : 0) + (Exists ? 1 : 0)
            + (Updated ? 1 : 0) + (string.IsNullOrWhiteSpace(Custom) ? 0 : 1);
        if (conditions != 1)
        {
            throw new ArgumentException("Specify exactly one wait condition: Created, Deleted, Exists, Updated, or Custom.");
        }
        if (Interval <= 0 || Timeout <= 0)
        {
            throw new ArgumentException("Interval and Timeout must be positive.");
        }

        DateTime deadline = DateTime.UtcNow.AddSeconds(Timeout);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                GetAtTenantScopeDeploymentsProcessor get =
                    CreateExtendedProcessor<GetAtTenantScopeDeploymentsProcessor>(p => p.DeploymentName = Name);
                using HttpResponseMessage response = await get.SendAsync();
                string rawBody = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
                DeploymentExtended? deployment = JsonSerializer.Deserialize<DeploymentExtended>(rawBody, JsonOptions);
                string? state = deployment?.Properties?.ProvisioningState;
                if (string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The deployment operation failed.");
                }
                if (Matches(deployment, rawBody))
                {
                    if (deployment is not null)
                    {
                        await WriteOutput(deployment);
                    }
                    return;
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                if (Deleted)
                {
                    return;
                }
            }

            TimeSpan remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(Interval, remaining.TotalSeconds)), Publisher.Token);
        }

        throw new TimeoutException($"Timed out waiting for tenant deployment '{Name}'.");
    }

    private bool Matches(DeploymentExtended? deployment, string rawBody)
    {
        if (deployment is null)
        {
            return false;
        }
        if (Exists)
        {
            return true;
        }
        if (Created || Updated)
        {
            return string.Equals(deployment.Properties?.ProvisioningState, "Succeeded", StringComparison.OrdinalIgnoreCase);
        }
        if (!string.IsNullOrWhiteSpace(Custom))
        {
            // Custom conditions are evaluated as real JMESPath expressions against the raw
            // deployment JSON (e.g. "properties.provisioningState=='Succeeded'"), matching
            // `az deployment tenant wait --custom` (custom_wait_command), instead of only
            // recognizing a single hardcoded "properties.provisioningState==<value>" literal.
            string result;
            try
            {
                result = new JmesPath().Transform(rawBody, Custom);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Custom must be a valid JMESPath expression: {ex.Message}", ex);
            }
            using JsonDocument document = JsonDocument.Parse(result);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.False => false,
                JsonValueKind.String => document.RootElement.GetString()?.Length > 0,
                JsonValueKind.Number => document.RootElement.TryGetDouble(out double number) && number != 0,
                JsonValueKind.Array or JsonValueKind.Object => document.RootElement.GetRawText() is not "[]" and not "{}",
                _ => true,
            };
        }
        return false;
    }
}
