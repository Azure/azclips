// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Core.Abstractions.Runtime;
using DevLab.JmesPath;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary><c>az deployment mg wait</c>: polls GET until the requested condition holds.</summary>
public class WaitManagementGroupDeploymentProcessor_Orchestrator : GetAtManagementGroupScopeDeploymentsProcessor
{
    public bool Created { get; set; }

    public bool Updated { get; set; }

    public bool Deleted { get; set; }

    public bool Exists { get; set; }

    /// <summary>JMESPath condition evaluated against the GET body; met when the result is truthy.</summary>
    public string? Custom { get; set; }

    public int Interval { get; set; } = 30;

    public int Timeout { get; set; } = 3600;

    public override async Task ProcessAsync()
    {
        if (!Created && !Updated && !Deleted && !Exists && string.IsNullOrWhiteSpace(Custom))
        {
            throw new ArgumentException("incorrect usage: --created | --updated | --deleted | --exists | --custom JMESPATH");
        }
        if (Interval <= 0 || Timeout <= 0)
        {
            throw new ArgumentException("Interval and Timeout must be positive.");
        }

        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            for (int elapsed = 0; ; elapsed += Interval)
            {
                if (await IsConditionMetAsync())
                {
                    return;
                }
                if (elapsed + Interval >= Timeout)
                {
                    throw new TimeoutException($"Wait operation timed-out after {Timeout} seconds");
                }
                await Task.Delay(TimeSpan.FromSeconds(Interval), Publisher.Token);
            }
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    // Sent directly (not via SendAsync) so an expected 404 is not published as a failure.
    private async Task<bool> IsConditionMetAsync()
    {
        using HttpRequestMessage request = await CreateRequestMessage();
        using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            if (Deleted)
            {
                return true;
            }
            // Matches az's generic wait: 404 keeps polling only for --created/--exists/--custom.
            if (!Created && !Exists && string.IsNullOrWhiteSpace(Custom))
            {
                await response.EnsureSuccessAsync(Publisher);
            }
            return false;
        }

        await response.EnsureSuccessAsync(Publisher);
        if (Exists)
        {
            return true;
        }

        string rawBody = await response.Content.ReadAsStringAsync();
        DeploymentExtended? deployment = JsonSerializer.Deserialize<DeploymentExtended>(rawBody, JsonOptions);
        string? state = deployment?.Properties?.ProvisioningState;
        if (string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The operation failed.");
        }
        return ((Created || Updated) && string.Equals(state, "Succeeded", StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(Custom) && IsTruthy(new JmesPath().Transform(rawBody, Custom)));
    }

    // Python bool() semantics, as used by az's verify_property.
    private static bool IsTruthy(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement value = doc.RootElement;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => value.GetString()!.Length > 0,
            JsonValueKind.Number => value.GetDouble() != 0,
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            _ => false,
        };
    }
}
