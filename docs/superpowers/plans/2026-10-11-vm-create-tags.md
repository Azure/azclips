# `az vm create --tags` / `New-AzVM -Tag` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `--tags` to `az vm create` and `-Tag` to `New-AzVM`, so the VM and every resource the command creates are tagged. Implement it the way the ManagedServiceIdentity (MSI) extension implements tags, without depending on MSI.

**Architecture:** Both surfaces bind to one `string[]? TagsInput` property on `NewVMProcessor_Orchestrator`. The first statement of its `SendAsync` parses the input with `VmTagsParser`, a Compute-local copy of MSI's tag parser. The resulting dictionary is passed to every atomic processor that PUTs a resource. `New-AzVM` serializes its `-Tag` hashtable to a single JSON object string, which the parser's JSON branch reads back.

**Tech Stack:** C# on .NET 10 (`net10.0`, SDK pinned in `global.json`), xUnit, System.Text.Json, PowerShell SDK 7.6.1, PlatyPS 1.0.1, bash.

**Spec:** `docs/superpowers/specs/2026-10-09-vm-create-tags-design.md`

## Global Constraints

- **Self-contained.** Compute takes no project, package or assembly reference on ManagedServiceIdentity. Nothing under `src/Extensions/ManagedServiceIdentity` or `src/PS/ManagedServiceIdentity` changes.
- **Copied parsing.** Tag parsing is a copy of MSI's `ParseTags`/`ParseDictionary`. Use these error messages verbatim:
  - `Invalid dictionary entry '{entry}'. Expected KEY=VALUE format.`
  - `Invalid dictionary entry '{entry}'. Keys cannot be empty.`
  - `Invalid dictionary entry. Keys cannot be empty.`
- **Single binding.** Both surfaces bind to `string[]? TagsInput` on `NewVMProcessor_Orchestrator`. The CLI metadata key is `tags`. The PowerShell metadata key is `Tag`, and it is looked up case-sensitively.
- **Descriptions, verbatim.**
  - CLI: `Space-separated tags in KEY=VALUE format, or a JSON object. Applied to the virtual machine and to every resource the command creates.`
  - PowerShell metadata: `Tags to apply to the virtual machine and to every resource the command creates.`
- **What gets tagged.** Tag every resource the command PUTs: the VM, the NIC, the VNet, and the NSG and public IP when the command creates them. Never PUT an existing NSG or public IP that is only attached.
- **No tags means no change.** With no tags, request bodies are unchanged. The new `Tags` properties are omitted when null. The NSG body keeps sending `"tags":null`, as it does today.
- **Untouched entries.** Do not change the unregistered `s_newVM_PS` and `s_newVM_CLI` (`vm create-basic`) metadata entries.
- **No extras.** No new NuGet packages and no ChangeLog entry.
- **Live Azure only with consent.** Re-recording a scenario (Task 5) signs in to Azure and creates billable resources. Do it only after the user explicitly approves.
- **Commits.** Follow the repo's subject style (`[Compute] ...`, `{Tests} ...`) and add the trailer `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.

## Before you start

- Run every command from the repository root. The commands are written for `pwsh`.
- Build once with `dotnet build azclips.slnx`. If the build fails while Core.Agent downloads the GitHub Copilot npm package (a TLS/SSL handshake error), follow the `building-the-solution` skill.
- The help tooling (Tasks 2 and 3) and the PowerShell help gate read `artifacts/Debug`. Rebuild the solution after code changes and before running them.
- The PowerShell help gate needs PlatyPS 1.0.1. Check with `Get-Module -ListAvailable Microsoft.PowerShell.PlatyPS`, and if it is missing run `pwsh -File build/Install-BuildDependencies.ps1`.

## File structure

| File | Change | Responsibility |
| --- | --- | --- |
| `src/Extensions/Compute/Customization/VmTagsParser.cs` | Create | Copy of MSI's tag parser: KEY=VALUE entries or one JSON object |
| `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs` | Modify | Owns `TagsInput`, parses it first, and passes the tags to the NIC orchestrator and the VM processor |
| `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs` | Modify | Passes the tags to the VNet and NIC, and to the NSG and public IP it creates |
| `src/Extensions/Compute/Processors/NewVMProcessor.cs`, `NewVirtualNetworkProcessor.cs`, `NewNetworkSecurityGroupProcessor.cs`, `NewPublicIPAddressProcessor.cs`, `NewNetworkInterfaceProcessor.cs` | Modify | Put `Tags` in each PUT body |
| `src/Extensions/Compute/Models/VirtualMachine.cs`, `VirtualNetwork.cs`, `PublicIPAddress.cs`, `NetworkInterface.cs` | Modify | `Tags` property, omitted from JSON when null |
| `src/Extensions/Compute/ExtensionIndex.cs` | Modify | `vm create --tags` and `New-AzVM -Tag` metadata, both bound to `TagsInput` |
| `src/PS/Compute/Cmdlets/VmCmdletArgumentConverter.cs` | Create | Copy of MSI's hashtable converter, plus packing the tags as one JSON string |
| `src/PS/Compute/Cmdlets/NewVMCmdlet.cs` | Modify | `-Tag <Hashtable>` parameter |
| `src/PS/Compute/Cmdlets.Compute.csproj` | Modify | `InternalsVisibleTo` for `Cmdlets.Tests` |
| `tests/Extensions.Compute.Tests/VmTagsParserTests.cs` | Create | Parser rules |
| `tests/Cmdlets.Tests/VmCmdletArgumentConverterTests.cs` | Create | Hashtable-to-JSON packing |
| `tests/Cmdlets.Tests/Cmdlets.Tests.csproj` | Modify | Aliased reference to `Cmdlets.Compute` |
| `tests/Cli.Tests/VmCreateTests.cs` | Modify | Metadata for both surfaces |
| `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | Modify | Which request bodies carry tags |
| `tests/Scenario.Tests/VmLifecycleScenarioTests.cs` | Modify | Reads the tags back through the Network extension, on both surfaces |
| `tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json` | Re-record | The lifecycle recording, now with tags and the Network reads |
| `src/CLI/Help/Docs/vm/create.md` | Modify | CLI help |
| `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json` | Regenerate | CLI help export fixture |
| `src/PS/Compute/help/New-AzVM.md` | Modify | PowerShell help |
| `tests/Scripts/CLI/VMCreate.sh`, `tests/Scripts/PS/VMCreate.ps1` | Modify | Live smoke checks |

---

### Task 1: Compute tag parser

**Files:**
- Create: `src/Extensions/Compute/Customization/VmTagsParser.cs`
- Test: `tests/Extensions.Compute.Tests/VmTagsParserTests.cs` (create)

**Interfaces:**
- Consumes: nothing.
- Produces: `internal static Dictionary<string, string>? VmTagsParser.ParseTags(string[]? tags)` in namespace `Microsoft.Azure.Azclips.Extensions.Compute.Processors`. It returns `null` for null or empty input. It throws `InvalidOperationException` for malformed entries, and `JsonException` for input that starts with `{` but is not strict JSON or has a value that is neither a string nor `null`. A JSON `null` value passes through as a null tag value, as in MSI.

- [ ] **Step 1: Write the failing tests**

Create `tests/Extensions.Compute.Tests/VmTagsParserTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

public class VmTagsParserTests
{
    [Fact]
    public void KeyValueEntries_BecomeTags()
    {
        var tags = VmTagsParser.ParseTags(["env=prod", "owner=team-a"]);

        Assert.NotNull(tags);
        Assert.Equal(2, tags.Count);
        Assert.Equal("prod", tags["env"]);
        Assert.Equal("team-a", tags["owner"]);
    }

    [Fact]
    public void Key_IsTrimmed()
    {
        var tags = VmTagsParser.ParseTags([" env =prod"]);

        var tag = Assert.Single(tags!);
        Assert.Equal("env", tag.Key);
        Assert.Equal("prod", tag.Value);
    }

    [Fact]
    public void Value_KeepsEverythingAfterTheFirstEquals()
    {
        var tags = VmTagsParser.ParseTags(["filter=a=b"]);

        Assert.Equal("a=b", tags!["filter"]);
    }

    [Fact]
    public void EmptyValue_IsKept()
    {
        var tags = VmTagsParser.ParseTags(["env="]);

        Assert.Equal(string.Empty, tags!["env"]);
    }

    [Fact]
    public void Keys_AreCaseInsensitive_AndTheLastValueWins()
    {
        var tags = VmTagsParser.ParseTags(["Env=dev", "env=prod"]);

        Assert.Single(tags!);
        Assert.Equal("prod", tags!["ENV"]);
    }

    [Fact]
    public void JsonObject_BecomesTags()
    {
        var tags = VmTagsParser.ParseTags(["{\"env\":\"prod\",\"owner\":\"team-a\"}"]);

        Assert.NotNull(tags);
        Assert.Equal(2, tags.Count);
        Assert.Equal("prod", tags["env"]);
        Assert.Equal("team-a", tags["owner"]);
    }

    [Fact]
    public void EntryWithoutEquals_IsRejected()
    {
        var error = Assert.Throws<InvalidOperationException>(() => VmTagsParser.ParseTags(["novalue"]));

        Assert.Equal("Invalid dictionary entry 'novalue'. Expected KEY=VALUE format.", error.Message);
    }

    [Theory]
    [InlineData("=prod")]
    [InlineData(" =prod")]
    public void EmptyKey_IsRejected(string entry)
    {
        var error = Assert.Throws<InvalidOperationException>(() => VmTagsParser.ParseTags([entry]));

        Assert.Equal($"Invalid dictionary entry '{entry}'. Keys cannot be empty.", error.Message);
    }

    [Theory]
    [InlineData("{\"\":\"prod\"}")]
    [InlineData("{\" \":\"prod\"}")]
    public void JsonObjectWithEmptyKey_IsRejected(string json)
    {
        var error = Assert.Throws<InvalidOperationException>(() => VmTagsParser.ParseTags([json]));

        Assert.Equal("Invalid dictionary entry. Keys cannot be empty.", error.Message);
    }

    // Only strict JSON whose values are strings is accepted: no shorthand, no numbers.
    [Theory]
    [InlineData("{env:prod}")]
    [InlineData("{\"env\":1}")]
    public void JsonObjectThatIsNotStrictJsonOfStrings_Throws(string json)
    {
        Assert.ThrowsAny<JsonException>(() => VmTagsParser.ParseTags([json]));
    }

    [Fact]
    public void NullOrEmptyInput_MeansNoTags()
    {
        Assert.Null(VmTagsParser.ParseTags(null));
        Assert.Null(VmTagsParser.ParseTags([]));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj --filter "FullyQualifiedName~VmTagsParserTests"`

Expected: the build FAILS with `error CS0103: The name 'VmTagsParser' does not exist in the current context`.

- [ ] **Step 3: Write the parser**

Create `src/Extensions/Compute/Customization/VmTagsParser.cs`. The method bodies are a straight copy of `ParseTags` and `ParseDictionary` from `src/Extensions/ManagedServiceIdentity/Customization/IdentityCliArgumentParser.cs`. The only change is that the helper is `private`.

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Parses <c>vm create</c> tags from KEY=VALUE entries or from one JSON object. A copy of the
/// ManagedServiceIdentity extension's tag parsing, so Compute does not depend on that extension.
/// </summary>
internal static class VmTagsParser
{
    internal static Dictionary<string, string>? ParseTags(string[]? tags) => ParseDictionary(tags);

    private static Dictionary<string, string>? ParseDictionary(string[]? dictionary)
    {
        if (dictionary is null || dictionary.Length == 0)
            return null;

        string raw = string.Join(' ', dictionary).Trim();
        if (raw.StartsWith('{'))
        {
            var parsedJson = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
            if (parsedJson is not null && parsedJson.Keys.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException("Invalid dictionary entry. Keys cannot be empty.");
            }

            return parsedJson;
        }

        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string entry in dictionary)
        {
            int separator = entry.IndexOf('=');
            if (separator < 0)
            {
                throw new InvalidOperationException($"Invalid dictionary entry '{entry}'. Expected KEY=VALUE format.");
            }

            string key = entry[..separator].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException($"Invalid dictionary entry '{entry}'. Keys cannot be empty.");
            }

            parsed[key] = entry[(separator + 1)..];
        }

        return parsed;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj --filter "FullyQualifiedName~VmTagsParserTests"`

Expected: PASS, 14 tests.

- [ ] **Step 5: Commit**

```powershell
git add src/Extensions/Compute/Customization/VmTagsParser.cs tests/Extensions.Compute.Tests/VmTagsParserTests.cs
git commit -m '[Compute] Add the tag parser for `az vm create --tags`' -m 'Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>'
```

---

### Task 2: `az vm create --tags` tags every resource the command creates

**Files:**
- Modify: `src/Extensions/Compute/Models/VirtualMachine.cs`, `VirtualNetwork.cs`, `PublicIPAddress.cs`, `NetworkInterface.cs`
- Modify: `src/Extensions/Compute/Processors/NewVMProcessor.cs`, `NewVirtualNetworkProcessor.cs`, `NewNetworkSecurityGroupProcessor.cs`, `NewPublicIPAddressProcessor.cs`, `NewNetworkInterfaceProcessor.cs`
- Modify: `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs`, `NewVMProcessor_Orchestrator.cs`
- Modify: `src/Extensions/Compute/ExtensionIndex.cs` (CLI entry only)
- Modify: `src/CLI/Help/Docs/vm/create.md`
- Regenerate: `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json`
- Test: `tests/Cli.Tests/VmCreateTests.cs`, `tests/Scenario.Tests/VmCreateOrchestrationTests.cs`

**Interfaces:**
- Consumes: `VmTagsParser.ParseTags(string[]?)` from Task 1.
- Produces:
  - `public string[]? TagsInput { get; set; }` on `NewVMProcessor_Orchestrator`, in namespace `Microsoft.Azure.Azclips.Extensions.Compute.Processors`.
  - `public Dictionary<string, string>? Tags { get; set; }` on `NewNetworkInterfaceProcessor_Orchestrator`, `NewVirtualNetworkProcessor`, `NewNetworkSecurityGroupProcessor`, `NewPublicIPAddressProcessor`, `NewNetworkInterfaceProcessor` and `NewVMProcessor`.
  - A `Tags` property (`Dictionary<string, string>?`, omitted when null) on the models `VirtualMachine`, `VirtualNetwork`, `PublicIPAddress` and `NetworkInterface`.
  - CLI metadata key `tags`, mapped to `TagsInput`.
  - Test helpers in `VmCreateOrchestrationTests`: the field `s_tagArgs` (`["--tags", "env=test", "owner=me"]`) and the method `AssertTestTags(string body)`.

- [ ] **Step 1: Write the failing metadata test**

In `tests/Cli.Tests/VmCreateTests.cs`, add this test directly after `VmCreate_ExposesOsDiskSecurityEncryptionParameters`:

```csharp
    [Fact]
    public void VmCreate_ExposesTagsParameter()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("vm create", ClientType.CLI);

        Assert.True(metadata.Parameters.TryGetValue("tags", out var parameter));
        Assert.Equal("TagsInput", parameter.Name);
        Assert.Equal(typeof(string[]), parameter.ParameterType);
        Assert.False(parameter.IsRequired);
    }
```

- [ ] **Step 2: Write the failing orchestration tests**

In `tests/Scenario.Tests/VmCreateOrchestrationTests.cs`:

1. Add `using System.Text.Json;` directly after `using System.Net;`.
2. Add this field directly after the `s_cliArgs` field:

```csharp
    private static readonly string[] s_tagArgs = ["--tags", "env=test", "owner=me"];
```

3. Add these tests directly before the `FailedDetailRead_FailsVmCreate_AfterTheVmIsCreated` test:

```csharp
    [Fact]
    public void Tags_AreSentOnEveryResourceTheCommandCreates()
    {
        // No --nsg or --public-ip-address, so the command creates the default NSG and public IP.
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgsWithoutNsg, .. s_tagArgs]);

        AssertTestTags(PutBody(record, "/virtualMachines/"));
        AssertTestTags(PutBody(record, "/networkInterfaces/"));
        AssertTestTags(PutBody(record, "/virtualNetworks/"));
        AssertTestTags(PutBody(record, "/networkSecurityGroups/scenario-vmNSG?"));
        AssertTestTags(PutBody(record, "/publicIPAddresses/scenario-vmPublicIP?"));
    }

    [Fact]
    public void Tags_JsonObjectForm_TagsTheVm()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgs, "--tags", "{\"env\":\"test\",\"owner\":\"me\"}"]);

        AssertTestTags(PutBody(record, "/virtualMachines/"));
    }

    [Fact]
    public void Tags_Omitted_LeaveTheRequestBodiesUntagged()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), s_cliArgsWithoutNsg);

        foreach (var uriFragment in new[] { "/virtualMachines/", "/networkInterfaces/", "/virtualNetworks/", "/publicIPAddresses/scenario-vmPublicIP?" })
        {
            using var document = JsonDocument.Parse(PutBody(record, uriFragment));
            Assert.False(document.RootElement.TryGetProperty("tags", out _), $"The {uriFragment} body has tags.");
        }
    }

    [Fact]
    public void Tags_LeaveAnAttachedNsgAndPublicIpUntouched()
    {
        var record = RecordCli(
            new FakeVmArmBackend(SubscriptionId, SharedNsgId, SharedPublicIpId),
            [.. s_cliArgsWithoutNsg, "--nsg", "shared-nsg", "--public-ip-address", "shared-ip", .. s_tagArgs]);

        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/publicIPAddresses/"));
        AssertTestTags(PutBody(record, "/virtualMachines/"));
    }

    [Fact]
    public void Tags_Malformed_FailBeforeAnyAzureRequest()
    {
        var (record, error) = RecordCliFailure(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgs, "--tags", "novalue"]);

        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal("Invalid dictionary entry 'novalue'. Expected KEY=VALUE format.", error.Message);

        // Nothing after the resource group create ran: no image lookup and no create.
        Assert.DoesNotContain(record.Entries, e => e.Request.Uri.Contains("/providers/Microsoft.Compute/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/providers/"));
    }
```

4. Add this helper directly after the `PutBody` helper:

```csharp
    /// <summary>Asserts the root <c>tags</c> of a request body are exactly env=test and owner=me.</summary>
    private static void AssertTestTags(string body)
    {
        using var document = JsonDocument.Parse(body);
        var tags = document.RootElement.GetProperty("tags");
        Assert.Equal(2, tags.EnumerateObject().Count());
        Assert.Equal("test", tags.GetProperty("env").GetString());
        Assert.Equal("me", tags.GetProperty("owner").GetString());
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:

```powershell
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~VmCreateTests"
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmCreateOrchestrationTests.Tags_"
```

Expected:
- `VmCreate_ExposesTagsParameter` FAILS at `Assert.True()`.
- `Tags_AreSentOnEveryResourceTheCommandCreates`, `Tags_JsonObjectForm_TagsTheVm`, `Tags_LeaveAnAttachedNsgAndPublicIpUntouched` and `Tags_Malformed_FailBeforeAnyAzureRequest` FAIL, because `vm create` rejects `--tags` as an unknown parameter.
- `Tags_Omitted_LeaveTheRequestBodiesUntagged` already PASSES. It pins today's untagged bodies so the change cannot alter them.

- [ ] **Step 4: Add `Tags` to the models**

`src/Extensions/Compute/Models/VirtualMachine.cs`: in `class VirtualMachine`, add `Tags` between `Location` and `Plan`, so the start of the class reads:

```csharp
public class VirtualMachine
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Location { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImagePlan? Plan { get; set; }
```

`src/Extensions/Compute/Models/VirtualNetwork.cs`: add `using System.Text.Json.Serialization;` directly above `using Microsoft.Azure.Azclips.ExtensionRuntime.Json;`, and make the class read:

```csharp
public class VirtualNetwork
{
    public VirtualNetworkProperties Properties { get; set; } = null!;
    public string Location { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
```

`src/Extensions/Compute/Models/PublicIPAddress.cs`: in `class PublicIPAddress`, add `Tags` directly after `Location`:

```csharp
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Location { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }
```

`src/Extensions/Compute/Models/NetworkInterface.cs`: make the start of `class NetworkInterface` read:

```csharp
public class NetworkInterface
{
    public string Id { get; set; } = null!;
    public string Location { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }

    public NetworkInterfaceProperties Properties { get; set; } = null!;
```

Do not change `NetworkSecurityGroup.cs`. It already has `public Dictionary<string, string>? Tags { get; set; }`, which keeps serializing as `"tags":null`.

- [ ] **Step 5: Put `Tags` in each atomic processor's PUT body**

Each of the five processors gets a `Tags` property and sets it in `CreateRequestBody()` directly after `Location = Location,`.

`src/Extensions/Compute/Processors/NewVMProcessor.cs` (this file uses a namespace block, so the indentation is one level deeper). Add after `public bool BootDiagnosticsEnabled { get; set; } = false;`:

```csharp
        public Dictionary<string, string>? Tags { get; set; }
```

and in `CreateRequestBody()`:

```csharp
            VirtualMachine body = new VirtualMachine
            {
                Location = Location,
                Tags = Tags,
```

`src/Extensions/Compute/Processors/NewVirtualNetworkProcessor.cs`. Add after `public List<string> AddressPrefixes { get; set; } = [];`:

```csharp
    public Dictionary<string, string>? Tags { get; set; }
```

and in `CreateRequestBody()`:

```csharp
        VirtualNetwork body = new VirtualNetwork
        {
            Location = Location,
            Tags = Tags,
```

`src/Extensions/Compute/Processors/NewNetworkSecurityGroupProcessor.cs`. Add after `public List<string> RuleAccesses { get; set; } = [];`:

```csharp
    public Dictionary<string, string>? Tags { get; set; }
```

and in `CreateRequestBody()`:

```csharp
        NetworkSecurityGroup body = new NetworkSecurityGroup
        {
            Location = Location,
            Tags = Tags,
```

`src/Extensions/Compute/Processors/NewPublicIPAddressProcessor.cs`. Add after `public string AllocationMethod { get; set; } = "Static";`:

```csharp
    public Dictionary<string, string>? Tags { get; set; }
```

and make `CreateRequestBody()` read:

```csharp
    protected override PublicIPAddress? CreateRequestBody() => new PublicIPAddress
    {
        Location = Location,
        Tags = Tags,
        Sku = new PublicIPAddressSku { Name = Sku },
        Properties = new PublicIPAddressProperties
        {
            PublicIPAllocationMethod = AllocationMethod,
        },
    };
```

`src/Extensions/Compute/Processors/NewNetworkInterfaceProcessor.cs`. Add after `public string PublicIpAddressId { get; set; } = string.Empty;`:

```csharp

    public Dictionary<string, string>? Tags { get; set; }
```

and in `CreateRequestBody()`:

```csharp
        NetworkInterface body = new NetworkInterface
        {
            Location = Location,
            Tags = Tags,
```

- [ ] **Step 6: Pass the tags through the NIC orchestrator**

In `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs`:

1. Add this property directly after `public string DefaultPublicIpAddressName { get; set; } = string.Empty;`:

```csharp

    /// <summary>Tags for the VNet, the NIC, and any NSG or public IP this orchestrator creates. Null sends none.</summary>
    public Dictionary<string, string>? Tags { get; set; }
```

2. In `SendAsync`, the VNet processor's configuration ends:

```csharp
            p.AddressPrefixes = AddressPrefixes;
            p.Tags = Tags;
        });
```

3. In `SendAsync`, the NIC processor's configuration ends:

```csharp
            p.PublicIpAddressId = publicIpAddressId ?? string.Empty;
            p.Tags = Tags;
        });
```

4. In `CreateNetworkSecurityGroupAsync`, the configuration ends:

```csharp
            p.RuleAccesses = [defaultRule.Properties.Access];
            p.Tags = Tags;
        }).SendAsync();
```

5. In `CreatePublicIpAddressAsync`, the configuration reads:

```csharp
        using HttpResponseMessage response = await CreateExtendedProcessor<NewPublicIPAddressProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = name;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.Tags = Tags;
        }).SendAsync();
```

Existing NSGs and public IPs are only looked up (`FindIdAsync`) and attached, so they never receive tags. Do not add tags anywhere else.

- [ ] **Step 7: Parse the tags first in the VM orchestrator**

In `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs`:

1. Add this property directly after `public string? Image { get; set; }`. It has no `[ParameterGroup]`, so help lists `--tags` under the general Arguments group:

```csharp

        public string[]? TagsInput { get; set; }
```

2. Make the parse the first statement of `SendAsync`:

```csharp
        internal override async Task<HttpResponseMessage> SendAsync()
        {
            // Parsed first, so malformed tags fail before the image lookup or any create.
            Dictionary<string, string>? tags = VmTagsParser.ParseTags(TagsInput);

            if (string.IsNullOrWhiteSpace(AdminPassword) && SshKeyValues.Count == 0)
```

3. In the `NewNetworkInterfaceProcessor_Orchestrator` configuration, add `p.Tags = tags;` directly after `p.OsType = operatingSystemType;`.
4. In the `NewVMProcessor` configuration, add `p.Tags = tags;` directly after `p.NetworkInterfaceId = nic.Id;`.

- [ ] **Step 8: Register `--tags`**

In `src/Extensions/Compute/ExtensionIndex.cs`, inside `s_newVMOrchestrator_CLI`, add this entry directly after the `["public-ip-address"]` entry:

```csharp
                ["tags"]                             = new("TagsInput", "tags", typeof(string[]), "Space-separated tags in KEY=VALUE format, or a JSON object. Applied to the virtual machine and to every resource the command creates.", IsRequired: false, Aliases: [], AllowedValues: []),
```

- [ ] **Step 9: Run the tests to verify they pass**

Run:

```powershell
dotnet build azclips.slnx
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~VmCreateTests"
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmCreate"
dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj
```

Expected: all PASS. The build comes first so the script scenarios run the freshly built `az` binary. The `~VmCreate` filter also replays the existing VM create recordings and script scenarios, which shows untagged requests are unchanged. The bash script scenario is skipped on machines without a usable bash.

- [ ] **Step 10: Document `--tags`**

In `src/CLI/Help/Docs/vm/create.md`, add one sentence to the `Description:` paragraph. Replace:

```text
or `""` to create none. The command returns the new virtual machine
```

with:

```text
or `""` to create none. Pass `--tags` to tag the virtual machine, its network interface and virtual network, and the network security group and public IP address when the command creates them; an existing network security group or public IP address that the command attaches keeps its own tags. The command returns the new virtual machine
```

Then append this example at the end of the file:

````markdown

### Create a VM with tags

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --tags env=prod owner=team-a
```
````

- [ ] **Step 11: Regenerate the CLI export fixture and validate help**

Run:

```powershell
dotnet build azclips.slnx
pwsh -NoProfile -File build/Update-CliExportFixtures.ps1
git --no-pager diff --stat -- tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export
```

Expected: only `vm.json` changes. If any other fixture changes, stop and investigate rather than committing unrelated drift. `git --no-pager diff -- tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json` should show exactly three changes:
- `vm create`'s `long_summary` gains the `--tags` sentence.
- `examples` gains `Create a VM with tags`.
- `parameters` gains this entry between `--subscription-id` and `--virtual-network-name`. The exporter may escape some characters, for example `'` as `\u0027`; that is expected.

```json
      {
        "name": "--tags",
        "required": false,
        "short_summary": "Space-separated tags in KEY=VALUE format, or a JSON object. Applied to the virtual machine and to every resource the command creates.",
        "long_summary": ""
      },
```

Then run:

```powershell
dotnet run --project tools/Help/Azclips.Help.Tool/Azclips.Help.Tool.csproj --no-build --configuration Debug -- --artifacts-root artifacts/Debug --output-root artifacts/help
dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj --no-build --configuration Debug
```

Expected: the validator exits with code 0 (warnings are allowed), and the help tool tests PASS, including `CliExportFixturesTests`.

- [ ] **Step 12: Commit**

```powershell
git add src/Extensions/Compute tests/Cli.Tests/VmCreateTests.cs tests/Scenario.Tests/VmCreateOrchestrationTests.cs src/CLI/Help/Docs/vm/create.md tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json
git commit -m '[Compute] `az vm create`: Add `--tags`' -m 'Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>'
```

---

### Task 3: `New-AzVM -Tag`

**Files:**
- Create: `src/PS/Compute/Cmdlets/VmCmdletArgumentConverter.cs`
- Modify: `src/PS/Compute/Cmdlets/NewVMCmdlet.cs`, `src/PS/Compute/Cmdlets.Compute.csproj`
- Modify: `src/Extensions/Compute/ExtensionIndex.cs` (PowerShell entry only)
- Modify: `src/PS/Compute/help/New-AzVM.md`
- Modify: `tests/Cmdlets.Tests/Cmdlets.Tests.csproj`
- Test: `tests/Cmdlets.Tests/VmCmdletArgumentConverterTests.cs` (create), `tests/Cli.Tests/VmCreateTests.cs`, `tests/Scenario.Tests/VmCreateOrchestrationTests.cs`

**Interfaces:**
- Consumes:
  - `NewVMProcessor_Orchestrator.TagsInput` (`string[]?`) from Task 2.
  - The test helpers `PsParameters()`, `RecordPowerShell(FakeVmArmBackend, Dictionary<string, object>)`, `PutBody(SessionRecord, string)` (existing) and `AssertTestTags(string)` (Task 2) in `VmCreateOrchestrationTests`.
- Produces:
  - `VmCmdletArgumentConverter.ToStringDictionary(Hashtable table)`, returning `Dictionary<string, string>`, and `VmCmdletArgumentConverter.ToTagsInput(Hashtable table)`, returning `string[]`. Both are `public static` methods on the `internal static` class `VmCmdletArgumentConverter` in namespace `Microsoft.Azure.Azclips.Cmdlets.Compute`.
  - The `New-AzVM` parameter `public Hashtable? Tag { get; set; }`.
  - PowerShell metadata key `Tag`, mapped to `TagsInput`.

- [ ] **Step 1: Write the failing tests**

In `tests/Cli.Tests/VmCreateTests.cs`, add this test at the end of the class:

```csharp
    [Fact]
    public void NewAzVM_ExposesTagParameter()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("New-AzVM", ClientType.PowerShell);

        Assert.True(metadata.Parameters.TryGetValue("Tag", out var parameter));
        Assert.Equal("TagsInput", parameter.Name);
        Assert.Equal(typeof(string[]), parameter.ParameterType);
        Assert.False(parameter.IsRequired);
    }
```

In `tests/Scenario.Tests/VmCreateOrchestrationTests.cs`, add this test directly after `Tags_Malformed_FailBeforeAnyAzureRequest`:

```csharp
    [Fact]
    public void Tags_PowerShell_TagsTheVm()
    {
        var parameters = PsParameters();
        // New-AzVM sends its -Tag hashtable as one JSON object string; see VmCmdletArgumentConverter.
        parameters["Tag"] = new[] { "{\"env\":\"test\",\"owner\":\"me\"}" };

        var record = RecordPowerShell(new FakeVmArmBackend(SubscriptionId), parameters);

        AssertTestTags(PutBody(record, "/virtualMachines/"));
    }
```

Create `tests/Cmdlets.Tests/VmCmdletArgumentConverterTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsCompute;

using System.Collections;
using System.Text.Json;
using VmCmdletArgumentConverter = CmdletsCompute::Microsoft.Azure.Azclips.Cmdlets.Compute.VmCmdletArgumentConverter;

namespace Cmdlets.Tests;

public class VmCmdletArgumentConverterTests
{
    [Fact]
    public void ToTagsInput_PacksTheHashtableAsOneJsonObject()
    {
        var tagsInput = VmCmdletArgumentConverter.ToTagsInput(new Hashtable
        {
            ["env"] = "prod",
            ["filter"] = "a=b c",
        });

        var json = Assert.Single(tagsInput);
        var tags = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        Assert.Equal(2, tags.Count);
        Assert.Equal("prod", tags["env"]);
        Assert.Equal("a=b c", tags["filter"]);
    }

    [Fact]
    public void ToTagsInput_ConvertsNullValuesToEmptyStrings()
    {
        var json = Assert.Single(VmCmdletArgumentConverter.ToTagsInput(new Hashtable { ["env"] = null }));

        Assert.Equal(string.Empty, JsonSerializer.Deserialize<Dictionary<string, string>>(json)!["env"]);
    }

    [Fact]
    public void ToTagsInput_ConvertsOtherValuesWithToString()
    {
        var json = Assert.Single(VmCmdletArgumentConverter.ToTagsInput(new Hashtable { ["count"] = 3, ["enabled"] = true }));

        var tags = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        Assert.Equal("3", tags["count"]);
        Assert.Equal("True", tags["enabled"]);
    }

    [Fact]
    public void ToTagsInput_EmptyHashtable_IsAnEmptyJsonObject()
    {
        Assert.Equal(["{}"], VmCmdletArgumentConverter.ToTagsInput(new Hashtable()));
    }
}
```

- [ ] **Step 2: Wire the test project to the Compute cmdlets**

`Common/CmdletOutputStreamsFactory.cs` is compiled into every cmdlet assembly as an internal type. Once `Cmdlets.Compute` grants `InternalsVisibleTo` to `Cmdlets.Tests`, a plain reference would make that type ambiguous with Accounts' copy (CS0433). The existing Config reference already solves this with an alias, so do the same.

In `src/PS/Compute/Cmdlets.Compute.csproj`, add these item groups directly after the `<Import Project="$(MSBuildThisFileDirectory)..\..\..\Shared.props" />` line. They are the same as in `Cmdlets.Accounts.csproj`:

```xml
    <ItemGroup Condition="'$(Configuration)' == 'Release'">
        <InternalsVisibleTo Include="Cmdlets.Tests" Key="$(MSSharedLibPublicKey)" />
    </ItemGroup>
    <ItemGroup Condition="'$(Configuration)' != 'Release'">
        <InternalsVisibleTo Include="Cmdlets.Tests" />
    </ItemGroup>
```

In `tests/Cmdlets.Tests/Cmdlets.Tests.csproj`, replace the comment and the Config reference at the top of the first `<ItemGroup>` with:

```xml
    <!--
      Common/CmdletOutputStreamsFactory.cs compiles into Cmdlets.Config, Cmdlets.Accounts and Cmdlets.Compute,
      so its internal types reach this project more than once (CS0433). The aliases hide the Config and
      Compute copies; a test needing one of their types must add `extern alias CmdletsConfig;` or
      `extern alias CmdletsCompute;`.
    -->
    <ProjectReference Include="../../src/PS/Config/Cmdlets.Config.csproj">
      <Aliases>CmdletsConfig</Aliases>
    </ProjectReference>
    <ProjectReference Include="../../src/PS/Compute/Cmdlets.Compute.csproj">
      <Aliases>CmdletsCompute</Aliases>
    </ProjectReference>
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:

```powershell
dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj --filter "FullyQualifiedName~VmCmdletArgumentConverterTests"
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~VmCreateTests"
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmCreateOrchestrationTests.Tags_PowerShell"
```

Expected:
- `Cmdlets.Tests` FAILS to build with CS0234, because `VmCmdletArgumentConverter` does not exist in `Microsoft.Azure.Azclips.Cmdlets.Compute`.
- `NewAzVM_ExposesTagParameter` FAILS at `Assert.True()`.
- `Tags_PowerShell_TagsTheVm` FAILS because the engine cannot bind `Tag` (`Failed to parse arguments for command 'New-AzVM'`).

- [ ] **Step 4: Write the converter**

Create `src/PS/Compute/Cmdlets/VmCmdletArgumentConverter.cs`. `ToStringDictionary` is a straight copy of `IdentityCmdletArgumentConverter.ToStringDictionary` from `src/PS/ManagedServiceIdentity/Cmdlets/IdentityCmdletArgumentConverter.cs`.

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Text.Json;

namespace Microsoft.Azure.Azclips.Cmdlets.Compute;

/// <summary>
/// Converts New-AzVM arguments into the shapes the engine binds. A copy of the
/// ManagedServiceIdentity module's converter, so Compute does not depend on that module.
/// </summary>
internal static class VmCmdletArgumentConverter
{
    public static Dictionary<string, string> ToStringDictionary(Hashtable table)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in table)
        {
            if (entry.Key is null)
            {
                throw new ArgumentException("Key cannot be null.", nameof(table));
            }

            result[entry.Key.ToString()!] = entry.Value?.ToString() ?? string.Empty;
        }

        return result;
    }

    /// <summary>
    /// Packs <paramref name="table"/> into the one-element array that vm create's TagsInput binds.
    /// It is a JSON object, so keys and values containing '=' or spaces survive parsing.
    /// </summary>
    public static string[] ToTagsInput(Hashtable table) => [JsonSerializer.Serialize(ToStringDictionary(table))];
}
```

- [ ] **Step 5: Add `-Tag` to `New-AzVM`**

In `src/PS/Compute/Cmdlets/NewVMCmdlet.cs`:

1. Add `using System.Collections;` as the first `using`.
2. Add this parameter between `PublicIpAddressName` and `Profile`:

```csharp
    [Parameter(Mandatory = false)]
    public Hashtable? Tag { get; set; }
```

3. In `ProcessRecord`, directly after the `PublicIpAddressName` line, add:

```csharp
        // Omitted or $null sends no tags.
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tag)) && Tag is not null) { parameters.Add("Tag", VmCmdletArgumentConverter.ToTagsInput(Tag)); }
```

- [ ] **Step 6: Register `-Tag`**

In `src/Extensions/Compute/ExtensionIndex.cs`, inside `s_newVMOrchestrator_PS`, add this entry directly after the `["PublicIpAddressName"]` entry:

```csharp
                ["Tag"]                            = new("TagsInput", "Tag", typeof(string[]), "Tags to apply to the virtual machine and to every resource the command creates.", IsRequired: false, Aliases: [], AllowedValues: []),
```

- [ ] **Step 7: Run the tests to verify they pass**

Run:

```powershell
dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~VmCreateTests"
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmCreateOrchestrationTests"
```

Expected: all PASS. Running all of `Cmdlets.Tests` shows the alias keeps `CmdletOutputStreamsFactory` unambiguous.

- [ ] **Step 8: Document `-Tag`**

In `src/PS/Compute/help/New-AzVM.md`, make three edits.

1. In the SYNTAX block, replace the last two lines:

```text
 [-AcceleratedNetworking] [-PublicIpAddressName <string>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

with:

```text
 [-AcceleratedNetworking] [-PublicIpAddressName <string>] [-Tag <hashtable>] [-Profile <string>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

2. Insert this example after the `Create a VM without a public IP address` example, directly before `## PARAMETERS`:

````markdown
### Create a VM with tags

Provision the same stack and tag the virtual machine, network interface, virtual network, network security group, and public IP address with `env=prod` and `owner=team-a`.

```powershell
$adminPassword = 'P@ssw0rd-replace-me!'
New-AzVM -Name myvm -Location eastus -ResourceGroupName myrg -VmSize Standard_D2s_v3 -AdminUsername azureuser -AdminPassword $adminPassword -NetworkInterfaceName mynic -VirtualNetworkName myvnet -Tag @{ env = 'prod'; owner = 'team-a' }
```

````

3. Insert this parameter section directly before `### -VirtualNetworkName`, which puts it after `### -SubscriptionId`:

````markdown
### -Tag

Tags to apply to the virtual machine, its network interface and virtual network, and the network security group and public IP address when the command creates them.
An existing network security group or public IP address that the command attaches keeps its own tags.

```yaml
Type: System.Collections.Hashtable
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

````

- [ ] **Step 9: Validate the PowerShell help**

Run:

```powershell
dotnet build azclips.slnx
pwsh -NoProfile -File build/Test-PSHelpMarkdown.ps1 -Module Compute
dotnet run --project tools/Help/Azclips.Help.Tool/Azclips.Help.Tool.csproj --no-build --configuration Debug -- --artifacts-root artifacts/Debug --output-root artifacts/help
```

Expected: the gate prints `Validating help drift for module 'Compute' ...` and no `PSL13_` or `PSL14_` lines, and exits with code 0. The help tool exits with code 0.

If the gate reports `PSL14_ParameterDrift` for `New-AzVM.md`, do the following:
1. Run `pwsh -NoProfile -File build/New-PSHelpMarkdown.ps1 -Module Compute -Refresh`.
2. Restore every other file it touched with `git checkout -- <file>`.
3. In `New-AzVM.md`, keep only the `-Tag` changes and restore the hand-written `-Tag` description.
4. Rerun the gate.

- [ ] **Step 10: Commit**

```powershell
git add src/PS/Compute src/Extensions/Compute/ExtensionIndex.cs tests/Cmdlets.Tests tests/Cli.Tests/VmCreateTests.cs tests/Scenario.Tests/VmCreateOrchestrationTests.cs
git commit -m '[Compute] `New-AzVM`: Add `-Tag`' -m 'Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>'
```

---

### Task 4: Live smoke scripts check the tags

**Files:**
- Modify: `tests/Scripts/CLI/VMCreate.sh`, `tests/Scripts/PS/VMCreate.ps1`

**Interfaces:**
- Consumes: `az vm create --tags` (Task 2) and `New-AzVM -Tag` (Task 3), plus each script's existing helpers. In bash these are `fail`, `json_field`, `network_url`, `check_public_ip` and `check_nsg`. In PowerShell they are `Add-Failure`, `Get-NetworkResource`, `New-SmokeVM`, `Test-PublicIp` and `Test-Nsg`. The last two already accept an expected `azclips-smoke` tag.
- Produces: nothing for other tasks.

These scripts run against a live subscription, so this task checks only their syntax. Running them live is out of scope here.

- [ ] **Step 1: Tag case 1 in `tests/Scripts/CLI/VMCreate.sh`**

1. In the header comment, replace case 1's second line (`creates <vm>NSG with only the rdp rule`) so case 1 reads:

```bash
#   1. vm create --security-type TrustedLaunch    -> creates <vm>PublicIP; publicIps is set;
#                --tags azclips-smoke=vm-tags        creates <vm>NSG with only the rdp rule;
#                                                    the VM, NIC, VNet, NSG and public IP
#                                                    all carry the azclips-smoke tag
```

2. Directly after the `EXISTING_NSG_RULE=...` line, add:

```bash
# Case 1 passes this as --tags azclips-smoke=<value> and checks it on every resource it creates.
VM_TAG="vm-tags"
```

3. Directly before the `"$AZ" group create \` line, add:

```bash
# The network resource <type>/<name> must carry the azclips-smoke tag expected_tag.
check_network_tag()
{
    local label="$1" type="$2" name="$3" expected_tag="$4"
    local actual_tag

    if ! actual_tag=$("$AZ" rest --method GET --uri "$(network_url "$type" "$name")" --query 'json.tags."azclips-smoke"' --output tsv 2>&1)
    then
        fail "$label: could not read $type '$name': $actual_tag"
        return
    fi
    if [[ "$actual_tag" != "$expected_tag" ]]
    then
        fail "$label: $type '$name' azclips-smoke tag '$actual_tag' did not match '$expected_tag'"
        return
    fi

    echo "OK: $label: $type '$name' has azclips-smoke tag '$expected_tag'"
}

```

4. Replace the case 1 banner:

```bash
echo "== 1. vm create --security-type TrustedLaunch (default public IP ${DEFAULT_VM}PublicIP) =="
```

with:

```bash
echo "== 1. vm create --security-type TrustedLaunch --tags azclips-smoke=$VM_TAG (default public IP ${DEFAULT_VM}PublicIP) =="
```

5. Replace the case 1 block:

```bash
if output=$(create_vm "$DEFAULT_VM" --security-type TrustedLaunch)
then
    check_public_ip "default" "$output" yes "${DEFAULT_VM}PublicIP"
    check_nsg "default" "$DEFAULT_VM" "$DEFAULT_NSG_ID" "$RDP_NSG_RULE"
else
    fail "default: vm create failed"
fi
```

with:

```bash
if output=$(create_vm "$DEFAULT_VM" --security-type TrustedLaunch --tags "azclips-smoke=$VM_TAG")
then
    check_public_ip "default" "$output" yes "${DEFAULT_VM}PublicIP" "$VM_TAG"
    check_nsg "default" "$DEFAULT_VM" "$DEFAULT_NSG_ID" "$RDP_NSG_RULE" "$VM_TAG"
    vm_tag=$(json_field azclips-smoke "$output")
    if [[ "$vm_tag" == "$VM_TAG" ]]
    then
        echo "OK: default: VM '$DEFAULT_VM' has azclips-smoke tag '$VM_TAG'"
    else
        fail "default: VM '$DEFAULT_VM' azclips-smoke tag '$vm_tag' did not match '$VM_TAG'"
    fi
    check_network_tag "default" networkInterfaces "$DEFAULT_VM-nic" "$VM_TAG"
    check_network_tag "default" virtualNetworks "$DEFAULT_VM-vnet" "$VM_TAG"
else
    fail "default: vm create failed"
fi
```

- [ ] **Step 2: Tag case 1 in `tests/Scripts/PS/VMCreate.ps1`**

1. In the header comment, replace case 1's second line (`creates <vm>NSG with only the rdp rule`) so case 1 reads:

```powershell
#   1. New-AzVM -SecurityType TrustedLaunch        -> creates <vm>PublicIP; PublicIps is set;
#               -Tag @{'azclips-smoke'='vm-tags'}     creates <vm>NSG with only the rdp rule;
#                                                     the VM, NIC, VNet, NSG and public IP
#                                                     all carry the azclips-smoke tag
```

2. Directly after the `$existingNsgRule   = ...` line, add:

```powershell

# Case 1 passes this as -Tag @{ 'azclips-smoke' = <value> } and checks it on every resource it creates.
$vmTag             = 'vm-tags'
```

3. Directly before the `# Guard the cleanup block: each resource is only deleted once it exists.` comment, add:

```powershell
# The network resource $Type/$Name must carry the azclips-smoke tag $ExpectedSmokeTag.
function Test-NetworkTag([string]$Label, [string]$Type, [string]$Name, [string]$ExpectedSmokeTag)
{
    try
    {
        $resource = Get-NetworkResource -Type $Type -Name $Name
    }
    catch
    {
        Add-Failure "${Label}: could not read $Type '$Name': $($_.Exception.Message)"
        return
    }

    if ($resource.tags.'azclips-smoke' -ne $ExpectedSmokeTag)
    {
        Add-Failure "${Label}: $Type '$Name' azclips-smoke tag '$($resource.tags.'azclips-smoke')' did not match '$ExpectedSmokeTag'"
        return
    }

    Write-Host "OK: ${Label}: $Type '$Name' has azclips-smoke tag '$ExpectedSmokeTag'"
}

```

4. Replace the case 1 banner:

```powershell
    Write-Host "== 1. New-AzVM -SecurityType TrustedLaunch (default public IP ${defaultVm}PublicIP) =="
```

with:

```powershell
    Write-Host "== 1. New-AzVM -SecurityType TrustedLaunch -Tag azclips-smoke=$vmTag (default public IP ${defaultVm}PublicIP) =="
```

5. Replace the case 1 create and checks:

```powershell
    $defaultResult = New-SmokeVM -Name $defaultVm -Extra @{ SecurityType = 'TrustedLaunch' }
    Test-PublicIp -Label 'default' -Vm $defaultResult -ExpectPublicIp $true -ExpectedPublicIpName "${defaultVm}PublicIP"
    Test-Nsg -Label 'default' -VmName $defaultVm -ExpectedId $defaultNsgId -ExpectedRule $defaultNsgRule
```

with:

```powershell
    $defaultResult = New-SmokeVM -Name $defaultVm -Extra @{ SecurityType = 'TrustedLaunch'; Tag = @{ 'azclips-smoke' = $vmTag } }
    Test-PublicIp -Label 'default' -Vm $defaultResult -ExpectPublicIp $true -ExpectedPublicIpName "${defaultVm}PublicIP" -ExpectedSmokeTag $vmTag
    Test-Nsg -Label 'default' -VmName $defaultVm -ExpectedId $defaultNsgId -ExpectedRule $defaultNsgRule -ExpectedSmokeTag $vmTag
    $defaultVmTag = if ($defaultResult.Tags) { $defaultResult.Tags['azclips-smoke'] } else { $null }
    if ($defaultVmTag -eq $vmTag)
    {
        Write-Host "OK: default: VM '$defaultVm' has azclips-smoke tag '$vmTag'"
    }
    else
    {
        Add-Failure "default: VM '$defaultVm' azclips-smoke tag '$defaultVmTag' did not match '$vmTag'"
    }
    Test-NetworkTag -Label 'default' -Type 'networkInterfaces' -Name "$defaultVm-nic" -ExpectedSmokeTag $vmTag
    Test-NetworkTag -Label 'default' -Type 'virtualNetworks' -Name "$defaultVm-vnet" -ExpectedSmokeTag $vmTag
```

- [ ] **Step 3: Check both scripts' syntax**

Run (on Windows, run the bash check with Git Bash, for example `& 'C:\Program Files\Git\bin\bash.exe' -n tests/Scripts/CLI/VMCreate.sh`):

```powershell
bash -n tests/Scripts/CLI/VMCreate.sh
$errors = $null
$null = [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path tests/Scripts/PS/VMCreate.ps1).Path, [ref]$null, [ref]$errors)
$errors.Count
```

Expected: `bash -n` prints nothing and exits with code 0, and `$errors.Count` prints `0`.

- [ ] **Step 4: Commit**

```powershell
git add tests/Scripts/CLI/VMCreate.sh tests/Scripts/PS/VMCreate.ps1
git commit -m '{Tests} Check `vm create` tags in the live smoke scripts' -m 'Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>'
```

---

### Task 5: The VM lifecycle scenario reads the tags back through the Network extension

**Files:**
- Modify: `tests/Scenario.Tests/VmLifecycleScenarioTests.cs`
- Re-record: `tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json`

**Interfaces:**
- Consumes:
  - `az vm create --tags` and `VirtualMachine.Tags` (Task 2).
  - `New-AzVM`'s `Tag` engine parameter, which is a one-element JSON `string[]` (Task 3).
  - The Network extension's existing `network vnet show`, `network public-ip show`, `Get-AzVirtualNetwork_Get` and `Get-AzPublicIpAddress_Get` commands. Each surface uses the same Network processors. They return `Microsoft.Azure.Azclips.Extensions.Network.Models.VirtualNetwork` and `PublicIpAddress`, and both have `Dictionary<string, string>? Tags`.
- Produces: nothing for other tasks.

This test is record/playback.
- **Record mode** (`AZURE_TEST_MODE=Record`) runs the CLI surface against live Azure and rewrites the committed recording.
- **Playback** replays that recording on both surfaces without credentials. It matches requests on method and URI only, so both surfaces must make the same requests in the same order.
- The new Network reads and the tags are not in today's recording, so it has to be re-recorded (Step 3). Commit the test change only together with the new recording, because otherwise playback fails in CI.

- [ ] **Step 1: Update the scenario**

In `tests/Scenario.Tests/VmLifecycleScenarioTests.cs`:

1. Directly after `using Microsoft.Azure.Azclips.Extensions.Compute.Models;`, add these aliases. Compute's models also have a `VirtualNetwork`, so the Network types need aliases:

```csharp
using NetworkPublicIpAddress = Microsoft.Azure.Azclips.Extensions.Network.Models.PublicIpAddress;
using NetworkVirtualNetwork = Microsoft.Azure.Azclips.Extensions.Network.Models.VirtualNetwork;
```

2. Replace the class summary with:

```csharp
/// <summary>
/// The virtual machine lifecycle: create, list, show, stop, deallocate, start, delete, driven through
/// the real dispatcher/binder and ARM request pipeline. Two virtual machines are created so both
/// delete shapes are covered: a plain delete, and one with <c>--force-deletion</c>/<c>-ForceDeletion</c>,
/// which adds the <c>forceDeletion</c> query parameter. Every create passes tags. After the first one,
/// the Network extension reads back the virtual network and default public IP that create made, and
/// they must carry the tags too. Record mode runs the CLI surface against live Azure and rewrites the
/// recording; playback replays the committed recording on both surfaces without credentials.
/// </summary>
```

3. Add the Network extension to the systems under test:

```csharp
    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute, ExtensionUnderTest.Network];
```

4. In the record-mode block, directly after `AssertVm(RunCreate(host, subscriptionId, VmName, "1"), VmName);`, add:

```csharp
            AssertVirtualNetworkTagged(RunVnetShow(host, subscriptionId, "1"));
            AssertPublicIpTagged(RunPublicIpShow(host, subscriptionId, VmName));
```

5. In the CLI playback block, directly after `AssertVm(RunCreate(cli, SubscriptionId, VmName, "1"), VmName, SubscriptionId);`, add:

```csharp
            AssertVirtualNetworkTagged(RunVnetShow(cli, SubscriptionId, "1"));
            AssertPublicIpTagged(RunPublicIpShow(cli, SubscriptionId, VmName));
```

6. In the PowerShell playback block, directly after `AssertVm(ps.RunPowerShell("New-AzVM", PsCreateParameters(SubscriptionId, VmName, "1"), extension: "Compute"), VmName, SubscriptionId);`, add:

```csharp
            AssertVirtualNetworkTagged(ps.RunPowerShell("Get-AzVirtualNetwork_Get", PsNetworkParameters(SubscriptionId, "scenario-lifecycle-vnet-1"), extension: "Network"));
            AssertPublicIpTagged(ps.RunPowerShell("Get-AzPublicIpAddress_Get", PsNetworkParameters(SubscriptionId, $"{VmName}PublicIP"), extension: "Network"));
```

7. In `RunCreate`, end the argument list with the tags. Its last line becomes:

```csharp
            "--address-prefixes", "10.0.0.0/16",
            "--tags", "env=scenario", "suite=vm-lifecycle");
```

8. In `PsCreateParameters`, add this entry after `["AddressPrefixes"]`:

```csharp
        // New-AzVM sends its -Tag hashtable as one JSON object string.
        ["Tag"] = new[] { "{\"env\":\"scenario\",\"suite\":\"vm-lifecycle\"}" },
```

9. Directly after `RunCreate`, add:

```csharp
    private static IReadOnlyList<object> RunVnetShow(ScenarioHost host, string subscriptionId, string suffix) =>
        host.RunCli("network", "vnet", "show",
            "--subscription-id", subscriptionId,
            "--name", $"scenario-lifecycle-vnet-{suffix}",
            "--resource-group", ResourceGroupName);

    /// <summary><c>network public-ip show</c> for the default public IP <c>vm create</c> made for <paramref name="vmName"/>.</summary>
    private static IReadOnlyList<object> RunPublicIpShow(ScenarioHost host, string subscriptionId, string vmName) =>
        host.RunCli("network", "public-ip", "show",
            "--subscription-id", subscriptionId,
            "--name", $"{vmName}PublicIP",
            "--resource-group", ResourceGroupName);
```

10. Directly after `PsVmParameters`, add:

```csharp
    private static Dictionary<string, object> PsNetworkParameters(string subscriptionId, string name) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = name,
        ["ResourceGroupName"] = ResourceGroupName,
    };
```

11. In `AssertVm`, add `AssertTagged(vm.Tags);` directly after `Assert.Equal("Succeeded", vm.Properties.ProvisioningState);`. Then add these methods directly after `AssertVm`:

```csharp
    /// <summary>The tags every <c>vm create</c> in this scenario passes.</summary>
    private static void AssertTagged(Dictionary<string, string>? tags)
    {
        Assert.NotNull(tags);
        Assert.Equal(2, tags.Count);
        Assert.Equal("scenario", tags["env"]);
        Assert.Equal("vm-lifecycle", tags["suite"]);
    }

    private static void AssertVirtualNetworkTagged(IReadOnlyList<object> outputs) =>
        AssertTagged(Assert.IsType<NetworkVirtualNetwork>(Assert.Single(outputs)).Tags);

    private static void AssertPublicIpTagged(IReadOnlyList<object> outputs) =>
        AssertTagged(Assert.IsType<NetworkPublicIpAddress>(Assert.Single(outputs)).Tags);
```

- [ ] **Step 2: Run playback to verify it fails**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmLifecycleScenarioTests"`

Expected: FAIL with `Assert.NotNull() Failure` in `AssertTagged`, right after the first `vm create`. The committed recording predates tags.

- [ ] **Step 3: Re-record against live Azure (STOP: get the user's explicit go-ahead first)**

Before running anything, tell the user what this step does:
- It signs in with the built CLI.
- It creates two `Standard_D2s_v3` VMs and their network resources in resource group `scenario-vm-lifecycle-rg` (`westus2`), which takes roughly 20 minutes.
- Like today's recording run, it deletes the VMs but leaves the resource group and its network resources.

Run it only if the user approves. If they decline, revert the test with `git checkout -- tests/Scenario.Tests/VmLifecycleScenarioTests.cs` and report Task 5 as not done.

```powershell
dotnet build azclips.slnx
./artifacts/Debug/Core.Cli/az login --subscription-id <subscription-id>
python devops/scripts/live/refresh_recordings.py --filter VmLifecycleScenarioTests
```

On Windows the CLI binary is `az.exe`. The script runs these stages:
1. Plays back the class.
2. Re-records it with `AZURE_TEST_MODE=Record`, because playback fails.
3. Replays the new recording.
4. Scans the changed recordings for secrets.

Expected: the summary lists `refreshed: VmLifecycleScenarioTests` and `secret scan: clean`, and the script exits with code 0.

- [ ] **Step 4: Verify the new recording**

Run:

```powershell
git --no-pager diff --stat -- tests/Scenario.Tests/SessionRecords
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmLifecycleScenarioTests"
Remove-Item refresh-summary.md -ErrorAction SilentlyContinue
```

Expected:
- Only the lifecycle recording changed.
- Playback PASSES on both surfaces. This proves that the VM, the virtual network and the public IP carry the tags in the recorded responses.
- `refresh-summary.md` is the script's report, not a deliverable, so the last command removes it.

- [ ] **Step 5: Commit**

```powershell
git add tests/Scenario.Tests/VmLifecycleScenarioTests.cs tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json
git commit -m '{Tests} Read `vm create` tags back through Network in the VM lifecycle scenario' -m 'Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>'
```

---

## Final verification

Run all of these and confirm each result before reporting the work done:

```powershell
dotnet build azclips.slnx
dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj
dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~VmCreateTests"
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmCreate"
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj --filter "FullyQualifiedName~VmLifecycleScenarioTests"
dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj --no-build --configuration Debug
dotnet run --project tools/Help/Azclips.Help.Tool/Azclips.Help.Tool.csproj --no-build --configuration Debug -- --artifacts-root artifacts/Debug --output-root artifacts/help
pwsh -NoProfile -File build/Test-PSHelpMarkdown.ps1 -Module Compute
git --no-pager diff --stat main...HEAD -- src/Extensions/ManagedServiceIdentity src/PS/ManagedServiceIdentity
Select-String -Path src/Extensions/Compute/*.csproj, src/PS/Compute/*.csproj -Pattern ManagedServiceIdentity
git status --short
```

Expected:
- The build succeeds, every test run PASSES, and the help validator and PowerShell help gate exit with code 0.
- The MSI `git diff --stat` and the `Select-String` check print nothing, which shows MSI is untouched and Compute does not reference it.
- `git status --short` prints nothing, because all work is committed.

When you report the work, state the following:
- The live smoke scripts (`tests/Scripts/CLI/VMCreate.sh`, `tests/Scripts/PS/VMCreate.ps1`) were syntax-checked but not run, because they need a live subscription.
- Whether the lifecycle recording was refreshed in Task 5, or Task 5 was skipped because the user declined the live run. If Task 5 was skipped, leave the `VmLifecycleScenarioTests` command out of the run above.
