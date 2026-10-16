// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class UpdateResourceGroupProcessor_Orchestrator : UpdateResourceGroupProcessor
{
    private JsonObject? patchBodyOverride;

    [ArgumentInfo(Required = false, Description = "Resource tags.", SerializedName = "TagsInput")]
    public string[]? TagsInput { get; set; }

    [ArgumentInfo(Required = false, Description = "Generic update set expressions.", SerializedName = "Set")]
    public string[]? Set { get; set; }

    [ArgumentInfo(Required = false, Description = "Preserve string literals for generic updates.", SerializedName = "ForceString")]
    public bool ForceString { get; set; }

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        if (patchBodyOverride is null)
        {
            return await base.CreateRequestMessage();
        }

        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        HttpRequestMessage request = new(Method, CreateRequestUri())
        {
            Content = JsonContent.Create(patchBodyOverride, typeof(JsonObject), options: JsonOptions)
        };
        CreateRequestHeaders(request);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        Tags = ParseTags(TagsInput) ?? Tags;
        patchBodyOverride = null;

        if (Set is { Length: > 0 })
        {
            string[] normalizedSet = NormalizeSetPaths(Set);
            var getProcessor = CreateExtendedProcessor<GetResourceGroupProcessor>(processor =>
            {
                processor.SubscriptionId = SubscriptionId;
                processor.Name = ResourceGroupName;
            });

            using HttpResponseMessage response = await getProcessor.SendAsync();
            string content = await response.Content.ReadAsStringAsync();
            JsonObject current = JsonNode.Parse(content)?.AsObject()
                ?? throw new InvalidOperationException("The resource group response did not contain a JSON object.");

            if (Tags is not null)
            {
                current["tags"] = JsonSerializer.SerializeToNode(Tags, JsonOptions);
            }

            JsonUpdateHelper.ApplyGenericUpdate(current, normalizedSet, add: null, remove: null, ForceString);
            patchBodyOverride = CreatePatchBody(current);
        }

        return await base.SendAsync();
    }

    private static Dictionary<string, string>? ParseTags(string[]? tags)
    {
        if (tags is null)
        {
            return null;
        }

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string entry in tags)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            int separator = entry.IndexOf('=');
            string key = (separator < 0 ? entry : entry[..separator]).Trim();
            if (key.Length == 0)
            {
                throw AzclipsErrorException.InvalidInput($"Invalid tag '{entry}'. Tag keys cannot be empty.");
            }

            parsed[key] = separator < 0 ? string.Empty : entry[(separator + 1)..];
        }

        return parsed;
    }

    private static string[] NormalizeSetPaths(IEnumerable<string> expressions)
    {
        return [.. expressions.Select(expression =>
        {
            int separator = expression.IndexOf('=');
            if (separator <= 0)
            {
                throw AzclipsErrorException.InvalidInput($"Invalid --set expression '{expression}'. Expected PATH=VALUE.");
            }

            string path = expression[..separator];
            string root = path.Split('.', 2)[0];
            string canonicalRoot = root.ToLowerInvariant() switch
            {
                "name" => "name",
                "managedby" => "managedBy",
                "tags" => "tags",
                _ => throw AzclipsErrorException.InvalidInput(
                    $"The --set path '{path}' cannot be updated by the resource group PATCH API. Supported roots are name, managedBy, and tags."),
            };

            return canonicalRoot + path[root.Length..] + expression[separator..];
        })];
    }

    private static JsonObject CreatePatchBody(JsonObject current)
    {
        var patch = new JsonObject();
        foreach (string propertyName in new[] { "name", "managedBy", "tags" })
        {
            if (current.TryGetPropertyValue(propertyName, out JsonNode? value))
            {
                patch[propertyName] = value?.DeepClone();
            }
        }
        return patch;
    }
}
