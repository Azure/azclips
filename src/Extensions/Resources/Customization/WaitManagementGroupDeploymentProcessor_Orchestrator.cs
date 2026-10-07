// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using DevLab.JmesPath;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class WaitManagementGroupDeploymentProcessor_Orchestrator : ResourcesProcessorBase<object, object>
{
    public string ManagementGroupId { get; set; } = string.Empty;

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
        if (!Created && !Deleted && !Exists && !Updated && string.IsNullOrWhiteSpace(Custom))
        {
            throw new ArgumentException(
                "incorrect usage: --created | --updated | --deleted | --exists | --custom JMESPATH");
        }

        if (string.IsNullOrWhiteSpace(ManagementGroupId) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("ManagementGroupId and Name must be provided.");
        }
        if (Interval <= 0 || Timeout <= 0)
        {
            throw new ArgumentException("Interval and Timeout must be positive.");
        }

        for (int elapsed = 0; elapsed < Timeout; elapsed += Interval)
        {
            try
            {
                GetAtManagementGroupScopeDeploymentsProcessor get =
                    CreateExtendedProcessor<GetAtManagementGroupScopeDeploymentsProcessor>(p =>
                    {
                        p.GroupId = ManagementGroupId;
                        p.DeploymentName = Name;
                    });
                using HttpResponseMessage response = await get.SendAsync();
                string rawBody = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
                DeploymentExtended? deployment = string.IsNullOrWhiteSpace(rawBody)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<DeploymentExtended>(rawBody, JsonOptions);

                if (Exists)
                {
                    return;
                }

                string? provisioningState = deployment?.Properties?.ProvisioningState;
                if (string.Equals(provisioningState, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The operation failed.");
                }

                if (((Created || Updated)
                        && string.Equals(provisioningState, "Succeeded", StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(Custom) && MatchesCustom(rawBody)))
                {
                    return;
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                if (Deleted)
                {
                    return;
                }

                // Match azure-cli-core/.../command_operation.py:WaitCommandOperation.wait
                // (lines 446-450): a 404 is swallowed only for --created, --exists, or --custom.
                if (!Created && !Exists && string.IsNullOrWhiteSpace(Custom))
                {
                    throw;
                }
            }

            if (elapsed + Interval < Timeout)
            {
                await Task.Delay(TimeSpan.FromSeconds(Interval), Publisher.Token);
            }
        }

        throw new TimeoutException($"Wait operation timed-out after {Timeout} seconds");
    }

    private bool MatchesCustom(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(Custom) || string.IsNullOrWhiteSpace(rawBody))
        {
            return false;
        }

        string result = new JmesPath().Transform(rawBody, Custom);
        using var document = System.Text.Json.JsonDocument.Parse(result);
        return document.RootElement.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.String => !string.IsNullOrEmpty(document.RootElement.GetString()),
            System.Text.Json.JsonValueKind.Number => document.RootElement.TryGetDouble(out double number) && number != 0,
            System.Text.Json.JsonValueKind.Array => document.RootElement.GetArrayLength() > 0,
            System.Text.Json.JsonValueKind.Object => document.RootElement.EnumerateObject().Any(),
            _ => true,
        };
    }
}
