// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant wait</c>: polls GET until the condition is met (legacy CLI generic wait).
/// A 404 is read directly from the pipeline so an expected "not found" is not reported as a failure.
/// </summary>
public class WaitTenantDeploymentProcessor_Orchestrator : GetAtTenantScopeDeploymentsProcessor
{
    public bool Created { get; set; }

    public bool Updated { get; set; }

    public bool Deleted { get; set; }

    public bool Exists { get; set; }

    public string? Custom { get; set; }

    public int Interval { get; set; } = 30;

    public int Timeout { get; set; } = 3600;

    public override async Task ProcessAsync()
    {
        if (!(Created || Updated || Deleted || Exists || !string.IsNullOrEmpty(Custom)))
        {
            throw new ArgumentException("incorrect usage: --created | --updated | --deleted | --exists | --custom JMESPATH");
        }

        if (Interval == 0)
        {
            throw new ArgumentException("range() arg 3 must not be zero");
        }

        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            // Legacy: for _ in range(0, timeout, interval).
            for (int elapsed = 0; Interval > 0 && elapsed < Timeout; elapsed += Interval)
            {
                if (await IsConditionMetAsync())
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(Interval), Publisher.Token);
            }

            // Legacy returns (not raises) CLIError on timeout, which serializes to {} with exit code 0.
            await WriteOutput(new SerializedJson("{}"));
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

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

            if (!(Created || Exists || !string.IsNullOrEmpty(Custom)))
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

        string json = await response.Content.ReadAsStringAsync();
        DeploymentExtended? deployment = JsonSerializer.Deserialize<DeploymentExtended>(json, JsonOptions);
        string? state = deployment?.Properties?.ProvisioningState;
        if (string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The operation failed");
        }

        return ((Created || Updated) && string.Equals(state, "Succeeded", StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrEmpty(Custom) && IsTruthy(JsonNode.Parse(s_jmesPath.Transform(json, Custom))));
    }

    private static readonly DevLab.JmesPath.JmesPath s_jmesPath = new();

    // Python bool() semantics used by legacy verify_property.
    private static bool IsTruthy(JsonNode? node) => node switch
    {
        null => false,
        JsonArray array => array.Count > 0,
        JsonObject obj => obj.Count > 0,
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>().Length > 0,
        JsonValue value when value.GetValueKind() == JsonValueKind.Number => value.GetValue<double>() != 0,
        JsonValue value => value.GetValueKind() == JsonValueKind.True,
        _ => true,
    };
}
