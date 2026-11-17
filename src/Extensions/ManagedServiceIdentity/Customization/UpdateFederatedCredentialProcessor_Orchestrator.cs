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

public class UpdateFederatedCredentialProcessor_Orchestrator : UpdateFederatedCredentialProcessor
{
    private static readonly string[] ReadOnlyFederatedCredentialPatchBodyPaths =
    [
        JsonPropertyName<FederatedIdentityCredential>(nameof(FederatedIdentityCredential.Id)),
        JsonPropertyName<FederatedIdentityCredential>(nameof(FederatedIdentityCredential.Name)),
        JsonPropertyName<FederatedIdentityCredential>(nameof(FederatedIdentityCredential.Type)),
        JsonPropertyName<FederatedIdentityCredential>(nameof(FederatedIdentityCredential.SystemData)),
    ];

    private JsonObject? patchBodyOverride;

    [ArgumentInfo(Required = false, Description = @"Generic update set expressions.", SerializedName = @"Set")]
    public string[]? Set { get; set; }

    [ArgumentInfo(Required = false, Description = @"Generic update add expressions.", SerializedName = @"Add")]
    public string[]? Add { get; set; }

    [ArgumentInfo(Required = false, Description = @"Generic update remove expressions.", SerializedName = @"Remove")]
    public string[]? Remove { get; set; }

    [ArgumentInfo(Required = false, Description = @"Preserve string literals for generic updates.", SerializedName = @"ForceString")]
    public string? ForceString { get; set; }

    protected override Task BeforeProcessAsync()
    {
        FederatedCredentialValidator.ValidateClaimsMatchingExpression(ClaimsMatchingExpressionVersion, ClaimsMatchingExpressionValue);
        return base.BeforeProcessAsync();
    }

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
        // PUT needs the existing fields even without --set/--add/--remove.
        var processor = CreateExtendedProcessor<ShowFederatedCredentialProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroup = ResourceGroup;
            p.IdentityName = IdentityName;
            p.Name = Name;
        });

        using HttpResponseMessage response = await processor.SendAsync();

        JsonObject? patchBody = await ReadResponseBodyAsync<JsonObject>(response);
        if (patchBody is not null)
        {
            JsonUpdateHelper.MergePatch(patchBody, BuildPatchBody());
            JsonUpdateHelper.ApplyGenericUpdate(patchBody, Set, Add, Remove, ParseForceString());
            JsonUpdateHelper.RemovePaths(patchBody, ReadOnlyFederatedCredentialPatchBodyPaths);
        }
        patchBodyOverride = patchBody;

        return await base.SendAsync();
    }

    private JsonObject BuildPatchBody()
    {
        var body = new JsonObject();
        var properties = new JsonObject();

        if (Audiences is not null)
        {
            properties["audiences"] = JsonSerializer.SerializeToNode(Audiences, JsonOptions);
        }
        if (!string.IsNullOrWhiteSpace(Issuer))
        {
            properties["issuer"] = Issuer;
        }
        bool claimsMatchingExpressionRequested = ClaimsMatchingExpressionVersion is not null || !string.IsNullOrWhiteSpace(ClaimsMatchingExpressionValue);

        if (Subject is not null)
        {
            properties["subject"] = Subject;
        }
        if (claimsMatchingExpressionRequested)
        {
            properties["claimsMatchingExpression"] = JsonSerializer.SerializeToNode(
                new ClaimsMatchingExpression
                {
                    LanguageVersion = ClaimsMatchingExpressionVersion ?? 0,
                    Value = ClaimsMatchingExpressionValue
                },
                JsonOptions);
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

    private static string JsonPropertyName<T>(string propertyName) =>
        typeof(T).GetProperty(propertyName)?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? throw new InvalidOperationException($"Property '{typeof(T).Name}.{propertyName}' does not define {nameof(JsonPropertyNameAttribute)}.");
}
