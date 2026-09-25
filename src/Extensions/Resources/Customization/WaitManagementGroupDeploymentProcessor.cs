// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class WaitManagementGroupDeploymentProcessor : ResourcesProcessorBase<object, object>
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
        int conditions = (Created ? 1 : 0) + (Deleted ? 1 : 0) + (Exists ? 1 : 0)
            + (Updated ? 1 : 0) + (string.IsNullOrWhiteSpace(Custom) ? 0 : 1);
        if (conditions != 1)
        {
            throw new ArgumentException(
                "Specify exactly one wait condition: Created, Deleted, Exists, Updated, or Custom.");
        }
        if (string.IsNullOrWhiteSpace(ManagementGroupId) || string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("ManagementGroupId and Name must be provided.");
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
                GetAtManagementGroupScopeDeploymentsProcessor get =
                    CreateExtendedProcessor<GetAtManagementGroupScopeDeploymentsProcessor>(p =>
                    {
                        p.GroupId = ManagementGroupId;
                        p.DeploymentName = Name;
                    });
                using HttpResponseMessage response = await get.SendAsync();
                string rawBody = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
                DeploymentExtended? deployment =
                    JsonSerializer.Deserialize<DeploymentExtended>(rawBody, JsonOptions);
                if (Matches(deployment))
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

            await Task.Delay(TimeSpan.FromSeconds(Interval), Publisher.Token);
        }

        throw new TimeoutException(
            $"Timed out waiting for management group deployment '{Name}'.");
    }

    private bool Matches(DeploymentExtended? deployment)
    {
        if (deployment is null)
        {
            return false;
        }
        if (Created || Exists)
        {
            return true;
        }
        if (Updated)
        {
            return string.Equals(
                deployment.Properties?.ProvisioningState,
                "Succeeded",
                StringComparison.OrdinalIgnoreCase);
        }
        if (!string.IsNullOrWhiteSpace(Custom))
        {
            const string prefix = "properties.provisioningState==";
            if (!Custom.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Only custom conditions in the form '{prefix}<value>' are supported.");
            }
            string expected = Custom[prefix.Length..].Trim(' ', '\'', '"');
            return string.Equals(
                deployment.Properties?.ProvisioningState,
                expected,
                StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
