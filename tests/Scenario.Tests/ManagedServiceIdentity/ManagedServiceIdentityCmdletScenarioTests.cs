// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.Cmdlets.ManagedServiceIdentity;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models;
using Tests.Common.Stubs;

namespace Scenario.Tests;

/// <summary>
/// Runs the real managed identity cmdlet classes in a runspace against an in-memory ARM backend,
/// the layer <c>ScenarioHost.RunPowerShell</c> bypasses: PowerShell parameter-set resolution
/// (Get routes to _List or _Get), Hashtable tag conversion, ValidateSet, ShouldProcess/WhatIf,
/// list enumeration across pages, and error-stream behavior on a missing resource.
/// </summary>
[Collection("Scenario Tests")]
public class ManagedServiceIdentityCmdletScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-identity-rg";
    private const string IdentityName = "scenario-identity";
    private const string SecondIdentityName = "scenario-identity-2";
    private const string Location = "westus2";

    [Fact]
    public void PowerShellCmdlets_Lifecycle_ConvertParametersAndRouteParameterSets()
    {
        var backend = new FakeIdentityArmBackend(SubscriptionId);
        using var host = CreateHost(backend);
        using var runspace = OpenRunspace();

        var created = Invoke(runspace, "New-AzUserAssignedIdentity", new(ResourceParameters(IdentityName))
        {
            ["Location"] = Location,
            ["Tag"] = new Hashtable { ["env"] = "scenario", ["owner"] = "ps" },
            ["IsolationScope"] = "Regional",
            ["ResourceRestriction"] = new[] { "Microsoft.Compute" },
        });
        var identity = Assert.IsType<UserAssignedIdentity>(Assert.Single(created));
        Assert.Equal(IdentityName, identity.Name);
        Assert.Equal("Regional", identity.Properties?.IsolationScope);
        Assert.Equal("Microsoft.Compute", Assert.Single(identity.Properties?.AssignmentRestrictions?.Providers ?? []));

        // The Hashtable tag is converted to the string dictionary the processor serializes.
        var putTags = backend.LastPutBody!["tags"]!.AsObject();
        Assert.Equal("scenario", putTags["env"]!.GetValue<string>());
        Assert.Equal("ps", putTags["owner"]!.GetValue<string>());
        Assert.Equal(Location, backend.LastPutBody!["location"]!.GetValue<string>());

        Invoke(runspace, "New-AzUserAssignedIdentity", new(ResourceParameters(SecondIdentityName)) { ["Location"] = Location });

        // No -Name selects the List parameter set; the resource-group scope follows nextLink and
        // enumerates both pages into individual objects.
        backend.Requests.Clear();
        var listedInGroup = Invoke(runspace, "Get-AzUserAssignedIdentity", new()
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
        });
        Assert.Equal([IdentityName, SecondIdentityName], listedInGroup.Cast<UserAssignedIdentity>().Select(i => i.Name!).ToArray());
        Assert.Collection(backend.Requests,
            first => Assert.StartsWith($"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities?", first.PathAndQuery),
            second => Assert.Contains("$skiptoken=page2", second.PathAndQuery));

        // Without a resource group the list is subscription-scoped.
        backend.Requests.Clear();
        var listedInSubscription = Invoke(runspace, "Get-AzUserAssignedIdentity", new() { ["SubscriptionId"] = SubscriptionId });
        Assert.Equal([IdentityName, SecondIdentityName], listedInSubscription.Cast<UserAssignedIdentity>().Select(i => i.Name!).ToArray());
        Assert.StartsWith($"/subscriptions/{SubscriptionId}/providers/Microsoft.ManagedIdentity/userAssignedIdentities?", backend.Requests[0].PathAndQuery);

        // -Name selects the Get parameter set and returns the single identity, not a list.
        backend.Requests.Clear();
        var shown = Assert.IsType<UserAssignedIdentity>(Assert.Single(Invoke(runspace, "Get-AzUserAssignedIdentity", ResourceParameters(IdentityName))));
        Assert.Equal(identity.Id, shown.Id);
        var showRequest = Assert.Single(backend.Requests);
        Assert.Equal(HttpMethod.Get, showRequest.Method);
        Assert.StartsWith($"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{IdentityName}?", showRequest.PathAndQuery);

        backend.Requests.Clear();
        var updated = Invoke(runspace, "Update-AzUserAssignedIdentity", new(ResourceParameters(IdentityName))
        {
            ["Tag"] = new Hashtable { ["env"] = "scenario-updated" },
            ["IsolationScope"] = "None",
            ["ResourceRestriction"] = new[] { "Microsoft.Web" },
        });
        var updatedIdentity = Assert.IsType<UserAssignedIdentity>(Assert.Single(updated));
        Assert.Equal("None", updatedIdentity.Properties?.IsolationScope);
        Assert.Equal("scenario-updated", updatedIdentity.Tags?["env"]);
        Assert.Equal("Microsoft.Web", Assert.Single(updatedIdentity.Properties?.AssignmentRestrictions?.Providers ?? []));
        Assert.Equal(HttpMethod.Patch, Assert.Single(backend.Requests).Method);
        var patch = backend.LastPatchBody!;
        Assert.Equal("scenario-updated", patch["tags"]!["env"]!.GetValue<string>());
        Assert.Equal("None", patch["properties"]!["isolationScope"]!.GetValue<string>());
        Assert.Equal("Microsoft.Web", patch["properties"]!["assignmentRestrictions"]!["providers"]![0]!.GetValue<string>());

        // Remove is ConfirmImpact.High; -Confirm:$false is required in a host without UI.
        backend.Requests.Clear();
        var removed = Invoke(runspace, "Remove-AzUserAssignedIdentity", new(ResourceParameters(IdentityName)) { ["Confirm"] = false });
        Assert.Empty(removed);
        Assert.Equal(HttpMethod.Delete, Assert.Single(backend.Requests).Method);
        Assert.False(backend.Contains(IdentityName));
        Assert.True(backend.Contains(SecondIdentityName));
    }

    [Fact]
    public void PowerShellCmdlets_WhatIf_SendsNoMutatingRequests()
    {
        var backend = new FakeIdentityArmBackend(SubscriptionId);
        using var host = CreateHost(backend);
        using var runspace = OpenRunspace();

        Assert.Empty(Invoke(runspace, "New-AzUserAssignedIdentity", new(ResourceParameters(IdentityName)) { ["Location"] = Location, ["WhatIf"] = true }));
        Assert.Empty(backend.Requests);
        Assert.False(backend.Contains(IdentityName));

        Invoke(runspace, "New-AzUserAssignedIdentity", new(ResourceParameters(IdentityName)) { ["Location"] = Location });
        backend.Requests.Clear();

        Assert.Empty(Invoke(runspace, "Update-AzUserAssignedIdentity", new(ResourceParameters(IdentityName)) { ["IsolationScope"] = "Regional", ["WhatIf"] = true }));
        Assert.Empty(Invoke(runspace, "Remove-AzUserAssignedIdentity", new(ResourceParameters(IdentityName)) { ["WhatIf"] = true }));

        Assert.Empty(backend.Requests);
        Assert.True(backend.Contains(IdentityName));
    }

    [Fact]
    public void Remove_WithoutConfirmInNonInteractiveHost_FailsBeforeSendingRequest()
    {
        var backend = new FakeIdentityArmBackend(SubscriptionId);
        using var host = CreateHost(backend);
        using var runspace = OpenRunspace();
        Invoke(runspace, "New-AzUserAssignedIdentity", new(ResourceParameters(IdentityName)) { ["Location"] = Location });
        backend.Requests.Clear();

        // ConfirmImpact.High exceeds the default $ConfirmPreference, so ShouldProcess prompts; a host
        // without UI cannot answer, which must stop the delete rather than proceed silently.
        var thrown = Assert.Throws<CmdletInvocationException>(() => InvokeRaw(runspace, "Remove-AzUserAssignedIdentity", ResourceParameters(IdentityName)));
        Assert.IsType<System.Management.Automation.Host.HostException>(thrown.InnerException);

        Assert.Empty(backend.Requests);
        Assert.True(backend.Contains(IdentityName));
    }

    [Theory]
    [InlineData("Get-AzUserAssignedIdentity", "GET")]
    [InlineData("Update-AzUserAssignedIdentity", "PATCH")]
    [InlineData("Remove-AzUserAssignedIdentity", "DELETE")]
    public void MissingIdentity_ThrowsResourceNotFound(string command, string expectedMethod)
    {
        var backend = new FakeIdentityArmBackend(SubscriptionId);
        using var host = CreateHost(backend);
        using var runspace = OpenRunspace();

        var parameters = new Dictionary<string, object>(ResourceParameters("missing-identity"));
        if (command.StartsWith("Update", StringComparison.Ordinal)) { parameters["IsolationScope"] = "None"; }
        if (command.StartsWith("Remove", StringComparison.Ordinal)) { parameters["Confirm"] = false; }

        // ARM failures surface as a terminating error carrying the ARM error code, not as output.
        var thrown = Assert.Throws<CmdletInvocationException>(() => InvokeRaw(runspace, command, parameters));
        Assert.Contains("404", thrown.Message);
        Assert.Contains("ResourceNotFound", thrown.Message);

        var request = Assert.Single(backend.Requests);
        Assert.Equal(expectedMethod, request.Method.Method);
        Assert.Contains("/userAssignedIdentities/missing-identity?", request.PathAndQuery);
    }

    [Fact]
    public void Get_NameWithoutResourceGroup_FailsParameterBinding()
    {
        var backend = new FakeIdentityArmBackend(SubscriptionId);
        using var host = CreateHost(backend);
        using var runspace = OpenRunspace();

        // -Name only exists in the Get set, which also makes -ResourceGroupName mandatory; a host
        // without UI cannot prompt for it, so binding fails before any request is sent.
        var thrown = Assert.Throws<ParameterBindingException>(() => InvokeRaw(runspace, "Get-AzUserAssignedIdentity", new()
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = IdentityName,
        }));
        Assert.Contains("ResourceGroupName", thrown.Message);
        Assert.Empty(backend.Requests);
    }

    [Theory]
    [InlineData("New-AzUserAssignedIdentity")]
    [InlineData("Update-AzUserAssignedIdentity")]
    public void InvalidIsolationScope_FailsValidateSet(string command)
    {
        var backend = new FakeIdentityArmBackend(SubscriptionId);
        using var host = CreateHost(backend);
        using var runspace = OpenRunspace();

        var parameters = new Dictionary<string, object>(ResourceParameters(IdentityName)) { ["IsolationScope"] = "Global" };
        if (command.StartsWith("New", StringComparison.Ordinal)) { parameters["Location"] = Location; }

        // The concrete ParameterBindingValidationException is internal to PowerShell; the public
        // base plus the ValidateSet error id pins the failure to argument validation.
        var thrown = Assert.ThrowsAny<ParameterBindingException>(() => InvokeRaw(runspace, command, parameters));
        Assert.Equal("ParameterArgumentValidationError", thrown.ErrorId);
        Assert.Equal("IsolationScope", thrown.ParameterName);
        Assert.Empty(backend.Requests);
    }

    [Fact]
    public void SubscriptionIdOmitted_ResolvesFromActiveProfile()
    {
        var backend = new FakeIdentityArmBackend(SubscriptionId);
        using var host = CreateHost(backend);
        using var runspace = OpenRunspace();
        var profileSubscription = new StubProfileService().GetActiveProfileAsync().GetAwaiter().GetResult()!.SubscriptionId!;

        // Users rarely pass -SubscriptionId; an unresolved one would produce /subscriptions//...
        Invoke(runspace, "New-AzUserAssignedIdentity", new()
        {
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = IdentityName,
            ["Location"] = Location,
        });
        Invoke(runspace, "Get-AzUserAssignedIdentity", new() { ["ResourceGroupName"] = ResourceGroupName });
        Invoke(runspace, "Get-AzUserAssignedIdentity", new() { ["ResourceGroupName"] = ResourceGroupName, ["Name"] = IdentityName });

        Assert.Equal(3, backend.Requests.Count);
        Assert.All(backend.Requests, request =>
            Assert.StartsWith($"/subscriptions/{profileSubscription}/resourceGroups/{ResourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities", request.PathAndQuery));
    }

    [Fact]
    public void New_TagWithNullValue_SendsEmptyString()
    {
        var backend = new FakeIdentityArmBackend(SubscriptionId);
        using var host = CreateHost(backend);
        using var runspace = OpenRunspace();

        Invoke(runspace, "New-AzUserAssignedIdentity", new(ResourceParameters(IdentityName))
        {
            ["Location"] = Location,
            ["Tag"] = new Hashtable { ["env"] = null, ["owner"] = 42 },
        });

        var tags = backend.LastPutBody!["tags"]!.AsObject();
        Assert.Equal(string.Empty, tags["env"]!.GetValue<string>());
        Assert.Equal("42", tags["owner"]!.GetValue<string>());
    }

    [Fact]
    public void Update_Location_IsSentInPatchBody()
    {
        var backend = new FakeIdentityArmBackend(SubscriptionId);
        using var host = CreateHost(backend);
        using var runspace = OpenRunspace();
        Invoke(runspace, "New-AzUserAssignedIdentity", new(ResourceParameters(IdentityName)) { ["Location"] = Location });

        Invoke(runspace, "Update-AzUserAssignedIdentity", new(ResourceParameters(IdentityName)) { ["Location"] = "eastus" });

        var patch = backend.LastPatchBody!;
        Assert.Equal("eastus", patch["location"]!.GetValue<string>());
        Assert.Null(patch["tags"]);
        Assert.Null(patch["properties"]);
    }

    private static ScenarioHost CreateHost(FakeIdentityArmBackend backend) => new(
        ClientType.PowerShell,
        new PassThroughInterceptor(),
        backend,
        extensions: [ExtensionUnderTest.ManagedServiceIdentity]);

    private static Runspace OpenRunspace()
    {
        var sessionState = InitialSessionState.CreateDefault2();
        sessionState.Commands.Add(new SessionStateCmdletEntry("New-AzUserAssignedIdentity", typeof(NewAzUserAssignedIdentityCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Get-AzUserAssignedIdentity", typeof(GetAzUserAssignedIdentityCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Update-AzUserAssignedIdentity", typeof(UpdateAzUserAssignedIdentityCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Remove-AzUserAssignedIdentity", typeof(RemoveAzUserAssignedIdentityCmdlet), null));
        var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();
        return runspace;
    }

    private static List<object> Invoke(Runspace runspace, string command, Dictionary<string, object> parameters)
    {
        using var powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;
        var outputs = powerShell.AddCommand(command).AddParameters(parameters).Invoke();

        Assert.Empty(powerShell.Streams.Error);
        return outputs.Select(output => output.BaseObject).ToList();
    }

    private static void InvokeRaw(Runspace runspace, string command, Dictionary<string, object> parameters)
    {
        using var powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;
        powerShell.AddCommand(command).AddParameters(parameters).Invoke();
    }

    private static Dictionary<string, object> ResourceParameters(string name) => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = name,
    };

    private sealed class PassThroughInterceptor : ITracingInterceptor
    {
        public SendAsyncStep SendAsync => (request, publisher, next) => next.SendAsync(request, publisher);
    }
}
