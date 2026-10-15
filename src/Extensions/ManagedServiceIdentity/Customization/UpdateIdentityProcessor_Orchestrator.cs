// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Processors;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;

public class UpdateIdentityProcessor_Orchestrator : UpdateIdentityProcessor
{
    private static readonly string[] ReadOnlyIdentityPatchBodyPaths =
    [
        JsonPropertyName<UserAssignedIdentity>(nameof(UserAssignedIdentity.Id)),
        JsonPropertyName<UserAssignedIdentity>(nameof(UserAssignedIdentity.Name)),
        JsonPropertyName<UserAssignedIdentity>(nameof(UserAssignedIdentity.Type)),
        JsonPropertyName<UserAssignedIdentity>(nameof(UserAssignedIdentity.SystemData)),
        JsonPath<UserAssignedIdentity, UserAssignedIdentityProperties>(
            nameof(UserAssignedIdentity.Properties),
            nameof(UserAssignedIdentityProperties.TenantId)),
        JsonPath<UserAssignedIdentity, UserAssignedIdentityProperties>(
            nameof(UserAssignedIdentity.Properties),
            nameof(UserAssignedIdentityProperties.PrincipalId)),
        JsonPath<UserAssignedIdentity, UserAssignedIdentityProperties>(
            nameof(UserAssignedIdentity.Properties),
            nameof(UserAssignedIdentityProperties.ClientId)),
    ];

    private JsonObject? patchBodyOverride;

    [ArgumentInfo(
    Required = false,
    Description = @"One or more identity resource IDs.",
    SerializedName = @"Ids")]
    public string[]? Ids { get; set; }

    [ArgumentInfo(Required = false, Description = @"Generic update set expressions.", SerializedName = @"Set")]
    public string[]? Set { get; set; }

    [ArgumentInfo(Required = false, Description = @"Generic update add expressions.", SerializedName = @"Add")]
    public string[]? Add { get; set; }

    [ArgumentInfo(Required = false, Description = @"Generic update remove expressions.", SerializedName = @"Remove")]
    public string[]? Remove { get; set; }

    [ArgumentInfo(Required = false, Description = @"Preserve string literals for generic updates.", SerializedName = @"ForceString")]
    public string? ForceString { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Resource tags.",
    SerializedName = @"TagsInput")]
    public string[]? TagsInput { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Restrictions on which resource providers this identity can be assigned to.",
    SerializedName = @"ResourceRestriction")]
    public string[]? ResourceRestriction { get; set; }

    private bool HasJsonUpdateOperations() =>
        Set is { Length: > 0 } || Add is { Length: > 0 } || Remove is { Length: > 0 };

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

    public override async Task ProcessAsync()
    {
        if (Ids is null || Ids.Length == 0)
        {
            await base.ProcessAsync();
            return;
        }
                
        try
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            var results = new List<UserAssignedIdentity>();

            //handle client customisation to enable batch processing of multiple identities
            foreach (string id in Ids)
            {
                using HttpResponseMessage response = await CreateUpdateProcessor(id).SendAsync();
                if (await ReadResponseBodyAsync<UserAssignedIdentity>(response) is { } identity)
                {
                    results.Add(identity);
                }
            }

            await WriteOutput(results.ToArray());
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        Tags = IdentityCliArgumentParser.ParseTags(TagsInput) ?? Tags;
        ResourceRestrictionProviders = ResourceRestriction is null
            ? ResourceRestrictionProviders
            : IdentityCliArgumentParser.ParseResourceRestrictionProviders(ResourceRestriction);

        if (HasJsonUpdateOperations())
        {
            var processor = CreateExtendedProcessor<ShowIdentityProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.ResourceGroup = ResourceGroup;
                p.ResourceName = ResourceName;
            });

            //need to get the current state of the resource to apply the patch operations
            using HttpResponseMessage response = await processor.SendAsync();

            JsonObject? patchBody = await ReadResponseBodyAsync<JsonObject>(response);
            if (patchBody is not null)
            {
                JsonUpdateHelper.MergePatch(patchBody, BuildPatchBody());
                JsonUpdateHelper.ApplyGenericUpdate(patchBody, Set, Add, Remove, ParseForceString());
                JsonUpdateHelper.RemovePaths(patchBody, ReadOnlyIdentityPatchBodyPaths);
            }
            patchBodyOverride = patchBody;
        }
        else
        {
            patchBodyOverride = null;
        }

        return await base.SendAsync();
    }

    private UpdateIdentityProcessor_Orchestrator CreateUpdateProcessor(string id)
    {
        var identityId = IdentityIdResolver.ParseIdentityResourceId(id);
        return CreateExtendedProcessor<UpdateIdentityProcessor_Orchestrator>(p =>
        {
            p.SubscriptionId = identityId.SubscriptionId;
            p.ResourceGroup = identityId.ResourceGroup;
            p.ResourceName = identityId.ResourceName;
            p.Tags = Tags;
            p.TagsInput = TagsInput;
            p.IsolationScope = IsolationScope;
            p.ResourceRestrictionProviders = ResourceRestrictionProviders;
            p.ResourceRestriction = ResourceRestriction;
            p.Set = Set;
            p.Add = Add;
            p.Remove = Remove;
            p.ForceString = ForceString;
        });
    }

    private JsonObject BuildPatchBody()
    {
        var body = new JsonObject();

        if (Tags is not null)
        {
            body["tags"] = JsonSerializer.SerializeToNode(Tags, JsonOptions);
        }

        var properties = new JsonObject();
        if (ResourceRestrictionProviders.Count > 0)
        {
            properties["assignmentRestrictions"] = JsonSerializer.SerializeToNode(
                new AssignmentRestrictions { Providers = ResourceRestrictionProviders },
                JsonOptions);
        }
        if (!string.IsNullOrWhiteSpace(IsolationScope))
        {
            properties["isolationScope"] = IsolationScope;
        }
        if (properties.Count > 0)
        {
            body["properties"] = properties;
        }

        return body;
    }

    private bool ParseForceString()
    {
        if (string.IsNullOrWhiteSpace(ForceString))
            return false;

        return ForceString.Trim().ToLowerInvariant() switch
        {
            "1" or "t" or "true" or "y" or "yes" => true,
            "0" or "f" or "false" or "n" or "no" => false,
            _ => throw AzclipsErrorException.InvalidInput($"Invalid --force-string value '{ForceString}'.")
        };
    }

    private static string JsonPath<TParent, TChild>(string parentPropertyName, string childPropertyName) =>
        $"{JsonPropertyName<TParent>(parentPropertyName)}.{JsonPropertyName<TChild>(childPropertyName)}";

    private static string JsonPropertyName<T>(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? throw new InvalidOperationException($"Property '{typeof(T).Name}.{propertyName}' does not define {nameof(JsonPropertyNameAttribute)}.");
}
