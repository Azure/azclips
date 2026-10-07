# `az vm create --tags` / `New-AzVM -Tag` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `--tags` to `az vm create` and `-Tag` to `New-AzVM`. The tags go on the VM and on every network resource the command `PUT`s, and each surface parses its input exactly as its legacy tool does.

**Architecture:** Both surfaces bind one `object?` property, `NewVMProcessor_Orchestrator.Tags`. The CLI delivers a `List<string>` of `key[=value]` tokens; PowerShell delivers the `Hashtable` untouched. A new internal helper, `ResourceTags.Normalize`, turns either into a `Dictionary<string, string>?` before any request. The orchestrators hand that dictionary to the VNet, NSG, public IP, NIC and VM processors, which copy it onto their request bodies. `VirtualMachine.Tags` is read back from ARM, so create, show and list output show the tags.

**Tech Stack:** C# with nullable enabled on the .NET SDK pinned in `global.json` (10.0.401); xUnit 2.9.3; scenario tests with record/playback against a fake ARM; PowerShell 7 with PlatyPS for PowerShell help; bash and PowerShell smoke scripts.

**Spec:** `docs/superpowers/specs/2026-10-02-vm-create-tags-design.md`

## Global Constraints

- `--tags` help text, verbatim: `Space-separated tags: key[=value] [key[=value] ...]. Applied to the VM and to each network resource the command creates or updates. Use "" to clear existing tags.`
- `-Tag` help text, verbatim: `Key-value pairs in the form of a hash table, applied to the VM and to each network resource the command creates or updates. For example: @{key0="value0";key1=$null;key2="value2"}.`
- `ResourceTags.Normalize(object? value)` checks, in this order:
  - `null` gives `null`.
  - `IDictionary` gives each key and value through `ToString()`, with a null value as `""`.
  - `IEnumerable<string>` splits each non-empty token on its first `=`. A token without `=` gets the value `""`. Nothing is trimmed.
  - Anything else throws `ArgumentException` with the message `Tags must be KEY[=VALUE] strings or a dictionary, not <type full name>.`
  - The result uses the default, case-sensitive comparer, and the last duplicate wins.
- There is no other client-side validation of tags. ARM judges count, length, characters and empty keys.
- `-Tag` is an optional `Hashtable` with no alias and no pipeline binding. `-Tag $null` counts as omitted.
- Without tags, every request body stays byte-identical to today's. New `Tags` model properties carry `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`. Do not modify `src/Extensions/Compute/Models/NetworkSecurityGroup.cs`.
- The Network api-version stays `2025-05-01`, and the Compute api-version stays `2025-11-01`.
- Only `vm create` / `New-AzVM` change. Do not add tags to the dormant metadata that no dispatch table registers: `s_newVM_PS`, `s_newVM_CLI`, `s_newVirtualNetwork_CLI`, `s_newNetworkSecurityGroup_CLI` and `s_newNetworkInterface_CLI`.
- Compute orchestrators and helpers live under `src/Extensions/Compute/Customization/` but declare the `Microsoft.Azure.Azclips.Extensions.Compute.Processors` namespace.
- `src` builds with `TreatWarningsAsErrors=true`, so product changes must add no warnings.
- Every `dotnet build` and `dotnet test` passes `-p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`, per the `building-the-solution` skill.
- If `build/build.ps1`'s own `dotnet build` hits the npm TLS error, set `$env:CopilotNpmRegistryUrl = 'https://packagefeedproxy.microsoft.io/npm/'` in that pwsh session and re-run. MSBuild reads environment variables as properties.
- Do not edit `CHANGELOG.md` or `src/PS/Compute/ChangeLog.md`.
- Commit prefixes: `[Compute]` for product-code changes, `{Compute}` for test-only and docs-only changes.
- End every commit message with the trailer `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`.
- No red windows: every task leaves the build, the suites it touches and the help validators green.

---

## File Structure

| File | Task | Responsibility |
|---|---|---|
| `src/Extensions/Compute/Customization/ResourceTags.cs` (new) | 1 | Legacy CLI and PowerShell tag conversion. |
| `tests/Extensions.Compute.Tests/ResourceTagsTests.cs` (new) | 1 | Unit tests for every parsing rule. |
| `src/Extensions/Compute/Models/VirtualMachine.cs`, `NetworkInterface.cs`, `VirtualNetwork.cs`, `PublicIPAddress.cs` | 2 | `Tags`, omitted when null. |
| `src/Extensions/Compute/Processors/NewVMProcessor.cs`, `NewNetworkInterfaceProcessor.cs`, `NewVirtualNetworkProcessor.cs`, `NewNetworkSecurityGroupProcessor.cs`, `NewPublicIPAddressProcessor.cs` | 2 | Copy `Tags` onto the request body. |
| `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs` | 2 | Pass the tags to the VNet, NSG, public IP and NIC `PUT`s. |
| `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs` | 2 | The bound `object? Tags`; normalized once and passed down. |
| `src/Extensions/Compute/ExtensionIndex.cs` | 2, 3 | `["tags"]` for `vm create` (2); `["Tag"]` for `New-AzVM` (3). |
| `tests/Scenario.Tests/FakeVmArmBackend.cs` | 2 | Echo a VM `PUT`'s tags in later VM `GET`s. |
| `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | 2, 3 | CLI tag tests (2); PowerShell tag test (3). |
| `src/CLI/Help/Docs/vm/create.md` | 2 | CLI help prose and example. |
| `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json` | 2 | Regenerated CLI export fixture. |
| `src/PS/Compute/Cmdlets/NewVMCmdlet.cs` | 3 | `-Tag`, forwarded unchanged. |
| `src/PS/Compute/help/New-AzVM.md` | 3 | PowerShell help. |
| `tests/Scripts/CLI/VMCreate.sh`, `tests/Scripts/PS/VMCreate.ps1` | 4 | Live smoke checks. |

**How the orchestration tests work.**
- `ScenarioHost` runs real `vm create` / `New-AzVM` invocations through the dispatcher, binder and engine.
- A `RecordPlaybackInterceptor` in `Record` mode captures every request into a `SessionRecord`, and `FakeVmArmBackend` answers them.
- Each test asserts on `record.Entries`: each entry's `Request.Method`, `Request.Uri` (the full URI, including `?api-version=`) and `Request.Body` (the compact JSON that was sent).
- `RunPowerShell` hands the parameter dictionary straight to the engine, in the shape `NewVMCmdlet` builds. A `Hashtable` passed as `Tag` therefore reaches the PowerShell argument-parsing provider exactly as the cmdlet forwards it.

---

### Task 1: Parse tags like each legacy tool

**Files:**
- Create: `src/Extensions/Compute/Customization/ResourceTags.cs`
- Test: `tests/Extensions.Compute.Tests/ResourceTagsTests.cs`

**Interfaces:**
- Produces: `internal static Dictionary<string, string>? ResourceTags.Normalize(object? value)` in namespace `Microsoft.Azure.Azclips.Extensions.Compute.Processors`. `Extensions.Compute.Tests` already sees Compute's internals through `InternalsVisibleTo`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Extensions.Compute.Tests/ResourceTagsTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

/// <summary>
/// Unit tests for <c>ResourceTags</c>, which parses vm create's tags the way each legacy tool
/// does: <c>az vm create --tags</c> tokens like azure-cli's <c>validate_tags</c>, and the
/// <c>New-AzVM -Tag</c> hashtable like azure-powershell's
/// <c>TagsConversionHelper.CreateTagDictionary</c>.
/// </summary>
public class ResourceTagsTests
{
    [Fact]
    public void Null_MeansNoTags()
    {
        Assert.Null(ResourceTags.Normalize(null));
    }

    [Fact]
    public void Tokens_AreKeyValuePairs()
    {
        AssertTags(new() { ["env"] = "prod", ["owner"] = "team-a" }, new List<string> { "env=prod", "owner=team-a" });
    }

    [Fact]
    public void Tokens_KeyWithoutAValue_GetsAnEmptyValue()
    {
        AssertTags(new() { ["owner"] = "" }, new List<string> { "owner" });
    }

    [Fact]
    public void Tokens_SplitOnTheFirstEquals()
    {
        AssertTags(new() { ["team"] = "a=b" }, new List<string> { "team=a=b" });
    }

    [Fact]
    public void Tokens_AreNotTrimmed()
    {
        AssertTags(new() { [" a "] = " b " }, new List<string> { " a = b " });
    }

    [Fact]
    public void Tokens_EmptyKey_IsLeftForArmToJudge()
    {
        AssertTags(new() { [""] = "v" }, new List<string> { "=v" });
    }

    [Fact]
    public void Tokens_LastDuplicateWins()
    {
        AssertTags(new() { ["env"] = "b" }, new List<string> { "env=a", "env=b" });
    }

    [Fact]
    public void Tokens_KeysAreCaseSensitive()
    {
        AssertTags(new() { ["Env"] = "a", ["env"] = "b" }, new List<string> { "Env=a", "env=b" });
    }

    [Fact]
    public void Tokens_EmptyTokens_AreSkipped()
    {
        AssertTags(new() { ["env"] = "prod" }, new List<string> { "", "env=prod" });
    }

    [Fact]
    public void Tokens_None_GiveEmptyTags()
    {
        AssertTags(new(), new List<string>());
    }

    [Fact]
    public void Tokens_OnlyAnEmptyToken_GivesEmptyTags()
    {
        AssertTags(new(), new List<string> { "" });
    }

    [Fact]
    public void Tokens_AsAStringArray_AreAccepted()
    {
        AssertTags(new() { ["env"] = "prod" }, new[] { "env=prod" });
    }

    [Fact]
    public void Hashtable_KeysAndValues_GoThroughToString()
    {
        AssertTags(new() { ["env"] = "prod", ["count"] = "5" }, new Hashtable { ["env"] = "prod", ["count"] = 5 });
    }

    [Fact]
    public void Hashtable_NullValue_BecomesEmpty()
    {
        AssertTags(new() { ["owner"] = "" }, new Hashtable { ["owner"] = null });
    }

    [Fact]
    public void Hashtable_Empty_GivesEmptyTags()
    {
        AssertTags(new(), new Hashtable());
    }

    [Fact]
    public void Dictionary_IsAccepted()
    {
        AssertTags(new() { ["env"] = "prod" }, new Dictionary<string, string> { ["env"] = "prod" });
    }

    [Fact]
    public void UnsupportedType_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => ResourceTags.Normalize(42));

        Assert.Equal("Tags must be KEY[=VALUE] strings or a dictionary, not System.Int32.", error.Message);
    }

    // xUnit compares dictionaries by content: key casing counts, order does not.
    private static void AssertTags(Dictionary<string, string> expected, object input)
    {
        Dictionary<string, string>? actual = ResourceTags.Normalize(input);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/ --filter "FullyQualifiedName~ResourceTagsTests"`
Expected: the build fails with `CS0103: The name 'ResourceTags' does not exist in the current context`.

- [ ] **Step 3: Implement `ResourceTags`**

Create `src/Extensions/Compute/Customization/ResourceTags.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Converts the value bound to vm create's tags into the tags sent to ARM, the way each legacy
/// tool does: <c>az vm create --tags</c> KEY[=VALUE] tokens like azure-cli's
/// <c>validate_tags</c>, and the <c>New-AzVM -Tag</c> hashtable like azure-powershell's
/// <c>TagsConversionHelper.CreateTagDictionary</c>. Neither tool validates tags; ARM does.
/// </summary>
internal static class ResourceTags
{
    /// <summary>
    /// Returns null when no tags were given, so requests carry no tags property; otherwise the
    /// tags, possibly empty. Keys are case-sensitive and the last duplicate wins.
    /// </summary>
    internal static Dictionary<string, string>? Normalize(object? value) => value switch
    {
        null => null,
        IDictionary table => FromTable(table),
        IEnumerable<string> tokens => FromTokens(tokens),
        object other => throw new ArgumentException(
            $"Tags must be KEY[=VALUE] strings or a dictionary, not {other.GetType().FullName}."),
    };

    // New-AzVM -Tag: keys and values through ToString(), and a $null value as "".
    private static Dictionary<string, string> FromTable(IDictionary table)
    {
        var tags = new Dictionary<string, string>();
        foreach (DictionaryEntry entry in table)
        {
            tags[entry.Key.ToString() ?? string.Empty] = entry.Value?.ToString() ?? string.Empty;
        }
        return tags;
    }

    // az vm create --tags: split on the first '=', a missing value is "", nothing is trimmed,
    // and an empty token, as from --tags "", is skipped.
    private static Dictionary<string, string> FromTokens(IEnumerable<string> tokens)
    {
        var tags = new Dictionary<string, string>();
        foreach (string token in tokens)
        {
            if (token.Length == 0)
            {
                continue;
            }

            int separator = token.IndexOf('=');
            if (separator < 0)
            {
                tags[token] = string.Empty;
            }
            else
            {
                tags[token[..separator]] = token[(separator + 1)..];
            }
        }
        return tags;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/ --filter "FullyQualifiedName~ResourceTagsTests"`
Expected: 17 passed, 0 failed. The build reports no new warnings for `Extensions.Compute`.

- [ ] **Step 5: Commit**

```bash
git add src/Extensions/Compute/Customization/ResourceTags.cs tests/Extensions.Compute.Tests/ResourceTagsTests.cs
git commit -m "[Compute] Add ResourceTags: parse vm create's tags like legacy CLI and PowerShell" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 2: `az vm create --tags`

**Files:**
- Modify: `tests/Scenario.Tests/FakeVmArmBackend.cs` (VM branch, one field, one method)
- Modify: `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` (one using, three fields, six tests, two helpers)
- Modify: `src/Extensions/Compute/Models/VirtualMachine.cs`, `NetworkInterface.cs`, `VirtualNetwork.cs`, `PublicIPAddress.cs`
- Modify: `src/Extensions/Compute/Processors/NewVMProcessor.cs`, `NewNetworkInterfaceProcessor.cs`, `NewVirtualNetworkProcessor.cs`, `NewNetworkSecurityGroupProcessor.cs`, `NewPublicIPAddressProcessor.cs`
- Modify: `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs`, `NewVMProcessor_Orchestrator.cs`
- Modify: `src/Extensions/Compute/ExtensionIndex.cs` (`s_newVMOrchestrator_CLI`)
- Modify: `src/CLI/Help/Docs/vm/create.md`
- Regenerate: `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json`

**Interfaces:**
- Consumes: `ResourceTags.Normalize(object? value)` from Task 1.
- Produces:
  - `NewVMProcessor_Orchestrator.Tags` (`object?`). CLI `["tags"]` binds it here; PowerShell `["Tag"]` binds it in Task 3.
  - `Dictionary<string, string>? Tags` on `NewNetworkInterfaceProcessor_Orchestrator`, `NewVMProcessor`, `NewNetworkInterfaceProcessor`, `NewVirtualNetworkProcessor`, `NewNetworkSecurityGroupProcessor` and `NewPublicIPAddressProcessor`.
  - `Dictionary<string, string>? Tags` on the `VirtualMachine`, `NetworkInterface`, `VirtualNetwork` and `PublicIPAddress` models.
  - In `VmCreateOrchestrationTests`: `s_tagArgs` (`string[]`), `s_expectedTags` (`Dictionary<string, string>`), `s_taggedPuts` (`string[]`), `private static string? PutTagsJson(SessionRecord record, string uriFragment)` and `private static Dictionary<string, string> PutTags(SessionRecord record, string uriFragment)`.

- [ ] **Step 1: Make the fake ARM echo a VM's tags**

In `tests/Scenario.Tests/FakeVmArmBackend.cs`, make four edits. Leave the rest of the `/virtualMachines/` branch, including its `FailVmDetailRead` block, unchanged.

(a) Add a field after `private readonly HashSet<string> _existingIds;`:

```csharp
    // The tags of the last VM PUT, which later VM GETs return the way ARM would.
    private JsonElement? _vmTags;
```

(b) In the `/virtualMachines/` branch of `SendAsync`, replace the PUT handling:

```csharp
            if (request.Method == HttpMethod.Put)
            {
                var accepted = Json(new { status = "InProgress" }, HttpStatusCode.Accepted);
                accepted.Headers.TryAddWithoutValidation(
                    "Azure-AsyncOperation",
                    $"https://management.azure.com/subscriptions/{_subscriptionId}/providers/Microsoft.Compute/locations/westus2/operations/vm-create?api-version=2025-11-01");
                return Task.FromResult(accepted);
            }
```

with:

```csharp
            if (request.Method == HttpMethod.Put)
            {
                return AcceptVmCreateAsync(request, cancellationToken);
            }
```

(c) Replace the branch's last statement, which answers every other VM `GET`:

```csharp
            return Task.FromResult(Json(new
            {
                id = Id("Microsoft.Compute/virtualMachines", name),
                name,
                location = "westus2",
                properties = new
                {
                    provisioningState = "Succeeded",
                    hardwareProfile = new { vmSize = "Standard_D2s_v3" },
                    storageProfile = new { imageReference = new { publisher = "MicrosoftWindowsServer", offer = "WindowsServer", sku = "2022-datacenter-azure-edition", version = "latest" } },
                    osProfile = new { computerName = name, adminUsername = "azadmin", windowsConfiguration = new { } },
                    networkProfile = new { networkInterfaces = new[] { new { id = Id("Microsoft.Network/networkInterfaces", "scenario-vm-nic") } } },
                },
            }, HttpStatusCode.OK));
```

with:

```csharp
            var vm = new Dictionary<string, object?>
            {
                ["id"] = Id("Microsoft.Compute/virtualMachines", name),
                ["name"] = name,
                ["location"] = "westus2",
                ["properties"] = new
                {
                    provisioningState = "Succeeded",
                    hardwareProfile = new { vmSize = "Standard_D2s_v3" },
                    storageProfile = new { imageReference = new { publisher = "MicrosoftWindowsServer", offer = "WindowsServer", sku = "2022-datacenter-azure-edition", version = "latest" } },
                    osProfile = new { computerName = name, adminUsername = "azadmin", windowsConfiguration = new { } },
                    networkProfile = new { networkInterfaces = new[] { new { id = Id("Microsoft.Network/networkInterfaces", "scenario-vm-nic") } } },
                },
            };

            // Like ARM, return no tags property when the create sent none.
            if (_vmTags is { } tags)
            {
                vm["tags"] = tags;
            }

            return Task.FromResult(Json(vm, HttpStatusCode.OK));
```

(d) Add this method directly before `private HttpResponseMessage TrackedResource(`:

```csharp
    /// <summary>Accepts a VM create as a long-running operation and remembers its tags.</summary>
    private async Task<HttpResponseMessage> AcceptVmCreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)))
        {
            _vmTags = body.RootElement.TryGetProperty("tags", out var tags) ? tags.Clone() : null;
        }

        var accepted = Json(new { status = "InProgress" }, HttpStatusCode.Accepted);
        accepted.Headers.TryAddWithoutValidation(
            "Azure-AsyncOperation",
            $"https://management.azure.com/subscriptions/{_subscriptionId}/providers/Microsoft.Compute/locations/westus2/operations/vm-create?api-version=2025-11-01");
        return accepted;
    }
```

The VM `GET` body serializes exactly as before: a `Dictionary<string, object?>` keeps insertion order, and `tags` is added only after a tagged create. `FailVmDetailRead` still answers the post-create `$expand=instanceView` read with a 500 before this point. The file already has `using System.Text.Json;`. The recorder reads every request body first, which buffers it, so the fake can read it again.

- [ ] **Step 2: Write the CLI tag tests**

In `tests/Scenario.Tests/VmCreateOrchestrationTests.cs`:

(a) Add `using System.Text.Json;` directly after `using System.Net;`.

(b) Add these fields directly after the `s_cliArgs` field:

```csharp
    // vm create's legacy CLI tag syntax: a pair, and a key without a value, which gets "".
    // The capital E pins that tag keys go out verbatim, not camel-cased like property names.
    private static readonly string[] s_tagArgs = ["--tags", "Env=prod", "owner"];

    private static readonly Dictionary<string, string> s_expectedTags = new() { ["Env"] = "prod", ["owner"] = "" };

    // Every resource the default vm create PUTs, and so tags.
    private static readonly string[] s_taggedPuts =
    [
        "/virtualNetworks/",
        "/networkSecurityGroups/",
        "/publicIPAddresses/",
        "/networkInterfaces/",
        "/virtualMachines/",
    ];
```

(c) Add these tests directly before `private static void AssertVm(`:

```csharp
    [Fact]
    public void Tags_GoOnTheVmAndEveryNetworkResourceItPuts()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgsWithoutNsg, .. s_tagArgs]);

        foreach (var put in s_taggedPuts)
        {
            Assert.Equal(s_expectedTags, PutTags(record, put));
        }
    }

    [Fact]
    public void Tags_Omitted_LeaveTheRequestBodiesAsTheyWere()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), s_cliArgsWithoutNsg);

        Assert.Null(PutTagsJson(record, "/virtualNetworks/"));
        Assert.Null(PutTagsJson(record, "/publicIPAddresses/"));
        Assert.Null(PutTagsJson(record, "/networkInterfaces/"));
        Assert.Null(PutTagsJson(record, "/virtualMachines/"));

        // The NSG model has always written its tags, null included, and the NIC embeds it as its NSG reference.
        Assert.Equal("null", PutTagsJson(record, "/networkSecurityGroups/"));
        Assert.Contains("\"location\":null,\"tags\":null", PutBody(record, "/networkInterfaces/"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tags_EmptyStringOrNoValue_SendEmptyTags(bool passEmptyString)
    {
        string[] tagArgs = passEmptyString ? ["--tags", ""] : ["--tags"];

        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgsWithoutNsg, .. tagArgs]);

        foreach (var put in s_taggedPuts)
        {
            Assert.Equal("{}", PutTagsJson(record, put));
        }
    }

    [Fact]
    public void Tags_DoNotReachAnAttachedNsgOrPublicIp()
    {
        var record = RecordCli(
            new FakeVmArmBackend(SubscriptionId, SharedNsgId, SharedPublicIpId),
            [.. s_cliArgsWithoutNsg, "--nsg", "shared-nsg", "--public-ip-address", "shared-ip", .. s_tagArgs]);

        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/networkSecurityGroups/"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/publicIPAddresses/"));
        Assert.Equal(s_expectedTags, PutTags(record, "/virtualNetworks/"));
        Assert.Equal(s_expectedTags, PutTags(record, "/networkInterfaces/"));
        Assert.Equal(s_expectedTags, PutTags(record, "/virtualMachines/"));
    }

    [Fact]
    public void Tags_RepeatedFlags_Accumulate()
    {
        // A documented difference: legacy argparse keeps only the last --tags; every AzClips CLI list parameter accumulates.
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgsWithoutNsg, "--tags", "Env=prod", "--tags", "owner"]);

        Assert.Equal(s_expectedTags, PutTags(record, "/virtualMachines/"));
    }

    [Fact]
    public void Tags_AreInTheCreateOutput()
    {
        IReadOnlyList<object> outputs;
        using (var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()), new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            outputs = host.RunCli([.. s_cliArgsWithoutNsg, .. s_tagArgs]);
        }

        AssertVm(outputs);
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(s_expectedTags, vm.Tags);
    }
```

(d) Add these helpers directly after `PutBody`, before `RecordVmBody`:

```csharp
    /// <summary>
    /// The top-level "tags" of the first PUT whose URI contains <paramref name="uriFragment"/>,
    /// as raw JSON; null when that body has no "tags" property.
    /// </summary>
    private static string? PutTagsJson(SessionRecord record, string uriFragment)
    {
        using var body = JsonDocument.Parse(PutBody(record, uriFragment));
        return body.RootElement.TryGetProperty("tags", out var tags) ? tags.GetRawText() : null;
    }

    /// <summary>The top-level tags of that PUT; fails when it carries none.</summary>
    private static Dictionary<string, string> PutTags(SessionRecord record, string uriFragment)
    {
        var json = PutTagsJson(record, uriFragment);
        Assert.False(json is null or "null", $"The PUT to {uriFragment} carried no tags.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json!)!;
    }
```

`PutTags` compares as dictionaries, so the PowerShell test in Task 3 can reuse it: a `Hashtable` enumerates in hash order.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/ --filter "FullyQualifiedName~VmCreateOrchestrationTests.Tags_"`
Expected: the build fails with `CS1061: 'VirtualMachine' does not contain a definition for 'Tags'`.

- [ ] **Step 4: Add `Tags` to the four models**

(a) `src/Extensions/Compute/Models/VirtualMachine.cs`: directly after `public string Location { get; set; } = null!;` in `VirtualMachine`, add:

```csharp

    // Omitted from a create request without tags, and absent from ARM's response for an untagged VM.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }
```

(b) `src/Extensions/Compute/Models/NetworkInterface.cs`: directly after `public string Location { get; set; } = null!;` in `NetworkInterface`, add:

```csharp

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }
```

(c) `src/Extensions/Compute/Models/VirtualNetwork.cs`: add `using System.Text.Json.Serialization;` directly before `using Microsoft.Azure.Azclips.ExtensionRuntime.Json;`. Then, directly after `public string Location { get; set; } = null!;` in `VirtualNetwork`, add:

```csharp

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }
```

(d) `src/Extensions/Compute/Models/PublicIPAddress.cs`: directly after the `Location` property and its attribute in `PublicIPAddress`, add:

```csharp

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }
```

Do not touch `NetworkSecurityGroup.cs`. It already has `Tags`, without `[JsonIgnore]`, and today's NSG and NIC bodies send `"tags":null` from it.

- [ ] **Step 5: Run the tests again**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/ --filter "FullyQualifiedName~VmCreateOrchestrationTests.Tags_"`
Expected: `Tags_Omitted_LeaveTheRequestBodiesAsTheyWere` passes; it pins today's bodies. The other six test cases fail because `vm create` does not accept `--tags` yet.

- [ ] **Step 6: Copy `Tags` onto each processor's request body**

In each of the five processors under `src/Extensions/Compute/Processors/`, add the property after the one named here, and add `Tags = Tags,` directly after `Location = Location,` in the top-level object initializer of `CreateRequestBody()`:

| File | Add the property after |
|---|---|
| `NewVMProcessor.cs` | `public bool BootDiagnosticsEnabled { get; set; } = false;` |
| `NewNetworkInterfaceProcessor.cs` | `public string PublicIpAddressId { get; set; } = string.Empty;` |
| `NewVirtualNetworkProcessor.cs` | `public List<string> AddressPrefixes { get; set; } = [];` |
| `NewNetworkSecurityGroupProcessor.cs` | `public List<string> RuleAccesses { get; set; } = [];` |
| `NewPublicIPAddressProcessor.cs` | `public string AllocationMethod { get; set; } = "Static";` |

The property, identical in all five:

```csharp
    public Dictionary<string, string>? Tags { get; set; }
```

`NewVMProcessor.cs` declares a block-scoped namespace, so its members are indented eight spaces; the other four use file-scoped namespaces and four spaces.

For example, `NewPublicIPAddressProcessor.CreateRequestBody()` becomes:

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

In `NewNetworkInterfaceProcessor.cs`, only the top-level `NetworkInterface` initializer gets `Tags = Tags,`. The embedded `NetworkSecurityGroup` reference stays as it is.

- [ ] **Step 7: Pass the tags through the NIC orchestrator**

In `src/Extensions/Compute/Customization/NewNetworkInterfaceProcessor_Orchestrator.cs`:

(a) Directly after `public string DefaultPublicIpAddressName { get; set; } = string.Empty;`, add:

```csharp

    /// <summary>Tags for every resource this orchestrator PUTs. Null sends none.</summary>
    public Dictionary<string, string>? Tags { get; set; }
```

(b) Add `p.Tags = Tags;` as the last line of each of these four configuration lambdas:
- the `NewVirtualNetworkProcessor` lambda in `SendAsync`, after `p.AddressPrefixes = AddressPrefixes;`;
- the `NewNetworkInterfaceProcessor` lambda in `SendAsync`, after `p.PublicIpAddressId = publicIpAddressId ?? string.Empty;`;
- the `NewNetworkSecurityGroupProcessor` lambda in `CreateNetworkSecurityGroupAsync`, after `p.RuleAccesses = [defaultRule.Properties.Access];`;
- the `NewPublicIPAddressProcessor` lambda in `CreatePublicIpAddressAsync`, after `p.ResourceGroupName = ResourceGroupName;`.

An existing NSG or public IP is never `PUT` — `NewOrExistingResourceResolver` sends it down the attach path — so it never sees the tags.

- [ ] **Step 8: Bind and normalize the tags in the VM orchestrator**

In `src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs`:

(a) Directly after the `PublicIpAddress` property (`[ParameterGroup("Network Arguments")] public string? PublicIpAddress { get; set; }`), add:

```csharp

        /// <summary>
        /// <c>--tags</c> KEY[=VALUE] tokens or the <c>-Tag</c> hashtable, as bound;
        /// <see cref="ResourceTags.Normalize"/> turns either into the tags every PUT carries.
        /// </summary>
        public object? Tags { get; set; }
```

It has no `[ParameterGroup]`: legacy CLI lists `--tags` under the general arguments.

(b) Make this the first statement of `SendAsync()`, before the `if (string.IsNullOrWhiteSpace(AdminPassword) && SshKeyValues.Count == 0)` check. That keeps it ahead of every request, including the marketplace image lookups that `ResolveImageMetadataAsync` sends:

```csharp
            Dictionary<string, string>? tags = ResourceTags.Normalize(Tags);

```

(c) Add `p.Tags = tags;` after `p.OsType = operatingSystemType;` in the NIC orchestrator lambda, and after `p.NetworkInterfaceId = nic.Id;` in the `NewVMProcessor` lambda.

- [ ] **Step 9: Register `--tags` on `vm create`**

In `src/Extensions/Compute/ExtensionIndex.cs`, inside `s_newVMOrchestrator_CLI`'s `Parameters`, directly after the `["public-ip-address"]` entry, add:

```csharp
                ["tags"]                             = new("Tags", "tags", typeof(List<string>), "Space-separated tags: key[=value] [key[=value] ...]. Applied to the VM and to each network resource the command creates or updates. Use \"\" to clear existing tags.", IsRequired: false, Aliases: [], AllowedValues: []),
```

The CLI parser converts by this metadata type, so `--tags a=1 b` binds a `List<string>` to the `object?` property. Repeated `--tags` flags accumulate, as for every list parameter.

- [ ] **Step 10: Run the tests to verify they pass**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/ --filter "FullyQualifiedName~VmCreateOrchestrationTests.Tags_"`
Expected: 7 passed, 0 failed.

- [ ] **Step 11: Run the vm create suites**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
Expected: all passed. The recorded `VmCreateScenarioTests`, `VmLifecycleScenarioTests` and `VmCreateScriptScenarioTests` replay unchanged.

Run: `dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
Expected: all passed.

- [ ] **Step 12: Document `--tags` in `src/CLI/Help/Docs/vm/create.md`**

(a) The `Description:` paragraph is one line, and must stay one line. Insert this sentence, followed by a space, directly before `The command returns the new virtual machine with the power state`:

```text
Pass `--tags` to tag the virtual machine and each network resource the command creates or updates, including an existing virtual network named by `--virtual-network-name`, whose tags are replaced; an existing network security group or public IP address that the command attaches keeps its own tags.
```

(b) Append this example at the end of the file, after the "Create a VM without a public IP address" example:

````markdown

### Create a VM with tags

```bash
az vm create --name myvm --resource-group myrg --location eastus --size Standard_D2s_v5 --admin-username azureuser --admin-password 'P@ssw0rd123!' --network-interface-name myvm-nic --virtual-network-name myvm-vnet --tags env=prod owner=team-a
```
````

- [ ] **Step 13: Build the solution and check the CLI help**

Run: `dotnet build azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
Expected: `Build succeeded`. The build copies the help markdown next to the CLI.

Run: `dotnet artifacts/Debug/Core.Cli/az.dll vm create --help`
Expected:
- `--tags` is listed among the general arguments, not the Network Arguments, with the help text from Global Constraints.
- The description includes the new sentence.
- All eight examples render, ending with "Create a VM with tags".

- [ ] **Step 14: Regenerate the CLI export fixture**

Run: `pwsh -File build/Update-CliExportFixtures.ps1`
Then run: `git --no-pager diff tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json`
Expected: only the `vm create` entry changes:
- `long_summary` gains the sentence from Step 12.
- `examples` gains "Create a VM with tags".
- `parameters` gains this entry between `--subscription-id` and `--virtual-network-name`:

```json
      {
        "name": "--tags",
        "required": false,
        "short_summary": "Space-separated tags: key[=value] [key[=value] ...]. Applied to the VM and to each network resource the command creates or updates. Use \u0022\u0022 to clear existing tags.",
        "long_summary": ""
      },
```

- [ ] **Step 15: Run the help tool tests**

Run: `dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
Expected: all passed, including `CliExportFixturesTests`.

- [ ] **Step 16: Commit**

```bash
git add src/Extensions/Compute tests/Scenario.Tests/FakeVmArmBackend.cs tests/Scenario.Tests/VmCreateOrchestrationTests.cs src/CLI/Help/Docs/vm/create.md tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json
git commit -m "[Compute] Add az vm create --tags: tag the VM and every network resource it creates or updates" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

Before committing, check `git --no-pager status --short` lists nothing else under `src/Extensions/Compute` beyond the files this task names.

### Task 3: `New-AzVM -Tag`

**Files:**
- Modify: `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` (one using, one test)
- Modify: `src/Extensions/Compute/ExtensionIndex.cs` (`s_newVMOrchestrator_PS`)
- Modify: `src/PS/Compute/Cmdlets/NewVMCmdlet.cs`
- Modify: `src/PS/Compute/help/New-AzVM.md` (SYNTAX, DESCRIPTION, EXAMPLES, PARAMETERS, OUTPUTS)

**Interfaces:**
- Consumes: `NewVMProcessor_Orchestrator.Tags` (`object?`); the test helpers `s_expectedTags`, `s_taggedPuts`, `PutTags`, `PsParameters()` and `RecordPowerShell(...)` from Task 2 and earlier.
- Produces: PowerShell metadata `["Tag"]` mapping to `Tags`, and `NewVMCmdlet.Tag` (`Hashtable?`).

- [ ] **Step 1: Write the failing PowerShell test**

In `tests/Scenario.Tests/VmCreateOrchestrationTests.cs`:

(a) Add `using System.Collections;` directly before `using System.Diagnostics;`.

(b) Add this test directly after `Tags_AreInTheCreateOutput`:

```csharp
    [Fact]
    public void PowerShell_Tag_HashtableTagsTheSameResources()
    {
        var parameters = PsParameters();
        parameters.Remove("SecurityGroupName");

        // What NewVMCmdlet forwards for -Tag @{ Env = 'prod'; owner = $null }: the hashtable itself.
        parameters["Tag"] = new Hashtable { ["Env"] = "prod", ["owner"] = null };

        var record = RecordPowerShell(new FakeVmArmBackend(SubscriptionId), parameters);

        foreach (var put in s_taggedPuts)
        {
            Assert.Equal(s_expectedTags, PutTags(record, put));
        }
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/ --filter "FullyQualifiedName~VmCreateOrchestrationTests.PowerShell_Tag_"`
Expected: FAIL. With no `Tag` metadata, the engine tries to bind a `Tag` property, which `NewVMProcessor_Orchestrator` does not have (`The property 'Tag' does not exist on processor ...`).

- [ ] **Step 3: Register `Tag` on `New-AzVM`**

In `src/Extensions/Compute/ExtensionIndex.cs`, inside `s_newVMOrchestrator_PS`'s `Parameters`, directly after the `["PublicIpAddressName"]` entry, add:

```csharp
                ["Tag"]                            = new("Tags", "Tag", typeof(System.Collections.Hashtable), "Key-value pairs in the form of a hash table, applied to the VM and to each network resource the command creates or updates. For example: @{key0=\"value0\";key1=$null;key2=\"value2\"}.", IsRequired: false, Aliases: [], AllowedValues: []),
```

`PSArgumentParsingProvider` converts only arrays and generic collections, so a `Hashtable` passes through to the `object?` property unchanged.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/ --filter "FullyQualifiedName~VmCreateOrchestrationTests.PowerShell_Tag_"`
Expected: 1 passed.

- [ ] **Step 5: Add `-Tag` to `NewVMCmdlet`**

In `src/PS/Compute/Cmdlets/NewVMCmdlet.cs`:

(a) Add `using System.Collections;` directly before `using System.Management.Automation;`.

(b) Directly after the `PublicIpAddressName` property, and before `Profile`, add:

```csharp

    [Parameter(Mandatory = false)]
    public Hashtable? Tag { get; set; }
```

(c) In `ProcessRecord()`, directly after the line that forwards `PublicIpAddressName`, add:

```csharp
        // Omitted or $null sends no tags; vm create converts the hashtable as legacy New-AzVM does.
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Tag)) && Tag is not null) { parameters.Add("Tag", Tag); }
```

- [ ] **Step 6: Build the solution**

Run: `dotnet build azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
Expected: `Build succeeded`, with no new warnings in `Cmdlets.Compute`.

- [ ] **Step 7: Document `-Tag` in `src/PS/Compute/help/New-AzVM.md`**

Make five edits.

(a) SYNTAX — replace the last two lines of the syntax block:

```text
 [-AcceleratedNetworking] [-PublicIpAddressName <string>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

with:

```text
 [-AcceleratedNetworking] [-PublicIpAddressName <string>] [-Tag <hashtable>] [-Profile <string>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

PlatyPS lists mandatory parameters first, then optional ones in declaration order. `-Tag` is declared between `-PublicIpAddressName` and `-Profile`. Leave the earlier lines, including `[-Image <string>]`, as they are.

(b) DESCRIPTION — directly after the line `Use `-PublicIpAddressName` to choose the public IP address by name or resource ID, or pass an empty string to create none.`, insert:

```markdown
Use `-Tag` to tag the virtual machine and each network resource this command creates or updates, including an existing virtual network named by `-VirtualNetworkName`, whose tags are replaced; an existing network security group or public IP address that this command attaches keeps its own tags.
```

(c) EXAMPLES — directly after the closing fence of the last example, "Create a VM without a public IP address", insert:

````markdown

### Create a VM with tags

Provision the same stack and tag the virtual machine, its network interface, virtual network, network security group, and public IP address with `env` and `owner`.

```powershell
$adminPassword = 'P@ssw0rd-replace-me!'
New-AzVM -Name myvm -Location eastus -ResourceGroupName myrg -VmSize Standard_D2s_v3 -AdminUsername azureuser -AdminPassword $adminPassword -NetworkInterfaceName mynic -VirtualNetworkName myvnet -Tag @{ env = 'prod'; owner = 'team-a' }
```
````

The example binds every mandatory parameter (PSL3) and assigns `$adminPassword` in its own fence (PSL5).

(d) PARAMETERS — insert this block directly before `### -VirtualNetworkName`, which keeps the section alphabetical:

````markdown
### -Tag

Key-value pairs in the form of a hash table, applied to the VM and to each network resource the command creates or updates.
For example: @{key0="value0";key1=$null;key2="value2"}.

Keys and values are stored as text, and a `$null` value as an empty string.
The tags go on the virtual machine, its network interface and virtual network, and on the network security group and public IP address when this command creates or updates them.
An existing virtual network named by `-VirtualNetworkName` is updated, so its tags are replaced.
An existing network security group or public IP address that this command attaches keeps its own tags.

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

(e) OUTPUTS — directly after the line `` `PublicIps` is empty when `-PublicIpAddressName` is an empty string. ``, add:

```markdown
`Tags` holds the virtual machine's tags as ARM returns them, and is `$null` when it has none.
```

- [ ] **Step 8: Validate the PowerShell help against the built cmdlet**

Run: `pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Compute`
Expected: no PSL13 or PSL14 records. If PlatyPS is missing, run `pwsh -File build/Install-BuildDependencies.ps1` first.

If PSL14 reports drift for `New-AzVM.md` because PlatyPS lays out the SYNTAX or the `-Tag` block differently:
1. Run `pwsh -File build/New-PSHelpMarkdown.ps1 -Module Compute -Refresh`.
2. Keep only the `New-AzVM.md` change: `git checkout -- src/PS/Compute/help/Get-AzVM.md src/PS/Compute/help/Remove-AzVM.md src/PS/Compute/help/Start-AzVM.md src/PS/Compute/help/Stop-AzVM.md`.
3. Put back any prose that the refresh replaced with a `TODO -` marker, including the edits from Step 7.
4. Re-run `pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Compute` and expect no records.

- [ ] **Step 9: Run the orchestration tests**

Run: `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/ --filter "FullyQualifiedName~VmCreateOrchestrationTests"`
Expected: all passed.

- [ ] **Step 10: Commit**

```bash
git add src/Extensions/Compute/ExtensionIndex.cs src/PS/Compute/Cmdlets/NewVMCmdlet.cs src/PS/Compute/help/New-AzVM.md tests/Scenario.Tests/VmCreateOrchestrationTests.cs
git commit -m "[Compute] Add New-AzVM -Tag: the same tags as az vm create --tags, from a hashtable" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 4: Smoke-check the tags live

**Files:**
- Modify: `tests/Scripts/CLI/VMCreate.sh` (header, one constant, three functions, cases 1 and 3)
- Modify: `tests/Scripts/PS/VMCreate.ps1` (header, one variable, two functions, cases 1 and 3)

**Interfaces:**
- Consumes: `az vm create --tags` (Task 2) and `New-AzVM -Tag` (Task 3), through the built CLI and module.
- Produces: smoke checks the user runs live. Cases 1 and 3 carry the tags, so quota stays at four Standard_D2s_v3 VMs.

Both scripts tag cases 1 and 3 with a pair and a key without a value: `azclips-tags=vm-create` and `azclips-empty`, which both surfaces store as `""`. Case 1 checks that the VM, its NIC and VNet, `{vm}NSG` and `{vm}PublicIP` carry both tags. Case 3 checks that the VM, its NIC and VNet carry them, and that the pre-created public IP and NSG do not. The VM's tags are read from the create output, which also proves the output carries them.

- [ ] **Step 1: Update the bash script's header**

In `tests/Scripts/CLI/VMCreate.sh`, replace:

```bash
#   1. vm create                                  -> creates <vm>PublicIP; publicIps is set;
#                                                    creates <vm>NSG with only the rdp rule
```

with:

```bash
#   1. vm create --tags <tags>                    -> creates <vm>PublicIP; publicIps is set;
#                                                    creates <vm>NSG with only the rdp rule;
#                                                    tags the VM, NIC, VNet, NSG and public IP
```

Then replace:

```bash
#   3. vm create --public-ip-address <existing>   -> reuses that public IP; publicIps is set;
#                --nsg <existing name>               attaches the pre-created NSG untouched
```

with:

```bash
#   3. vm create --public-ip-address <existing>   -> reuses that public IP; publicIps is set;
#                --nsg <existing name>               attaches the pre-created NSG untouched;
#                --tags <tags>                       tags the VM, NIC and VNet, but neither
#                                                    the pre-created public IP nor the NSG
```

Then replace:

```bash
# and smoke-allow-https, with the tag intact, for the NSG that cases 2 and 3 attach.
```

with:

```bash
# and smoke-allow-https, with the tag intact, for the NSG that cases 2 and 3 attach.
#
# Cases 1 and 3 pass --tags azclips-tags=vm-create azclips-empty: a pair, and a key
# without a value, which vm create stores with an empty value. The tag checks read the
# VM's tags from vm create's output, and each network resource's tags through az rest.
```

- [ ] **Step 2: Add the tag arguments**

Directly after the line `KEY_FILE="${TMPDIR:-/tmp}/azclips-vmcreate-key-$SUFFIX"`, add:

```bash

# vm create's tags for cases 1 and 3: a pair, and a key without a value, which becomes "".
TAG_ARGS=(--tags azclips-tags=vm-create azclips-empty)
```

- [ ] **Step 3: Add the tag checks**

Directly after the closing brace of `check_nsg()`, and before `"$AZ" group create \`, add:

```bash

# True when the JSON text carries both of TAG_ARGS' tags.
has_smoke_tags()
{
    [[ "$(json_field azclips-tags "$1")" == "vm-create" ]] && grep -qE '"azclips-empty": *""' <<< "$1"
}

# vm create's output is read back from the VM, so it must carry TAG_ARGS' tags.
check_output_tags()
{
    local label="$1" output="$2"
    if has_smoke_tags "$output"
    then
        echo "OK: $label: the VM carries azclips-tags and azclips-empty"
    else
        fail "$label: vm create's output lacks azclips-tags=vm-create or an empty azclips-empty"
    fi
}

# The network resource must carry TAG_ARGS' tags ("tagged") or neither of them ("untouched").
check_resource_tags()
{
    local label="$1" type="$2" name="$3" expect="$4"
    local tags
    if ! tags=$("$AZ" rest --method GET --uri "$(network_url "$type" "$name")" --query json.tags --output json 2>&1)
    then
        fail "$label: could not read the tags of $type '$name': $tags"
    elif [[ "$expect" == "tagged" ]] && ! has_smoke_tags "$tags"
    then
        fail "$label: $type '$name' lacks azclips-tags=vm-create or an empty azclips-empty: $tags"
    elif [[ "$expect" == "untouched" ]] && grep -qE '"azclips-(tags|empty)"' <<< "$tags"
    then
        fail "$label: vm create tagged $type '$name': $tags"
    else
        echo "OK: $label: $type '$name' is $expect"
    fi
}
```

- [ ] **Step 4: Tag case 1**

Replace:

```bash
echo "== 1. vm create (default public IP ${DEFAULT_VM}PublicIP, default NSG ${DEFAULT_VM}NSG) =="
if output=$(create_vm "$DEFAULT_VM")
then
    check_public_ip "default" "$output" yes "${DEFAULT_VM}PublicIP"
    check_nsg "default" "$DEFAULT_VM" "$DEFAULT_NSG_ID" "$RDP_NSG_RULE"
else
```

with:

```bash
echo "== 1. vm create --tags (default public IP ${DEFAULT_VM}PublicIP, default NSG ${DEFAULT_VM}NSG) =="
if output=$(create_vm "$DEFAULT_VM" "${TAG_ARGS[@]}")
then
    check_public_ip "default" "$output" yes "${DEFAULT_VM}PublicIP"
    check_nsg "default" "$DEFAULT_VM" "$DEFAULT_NSG_ID" "$RDP_NSG_RULE"
    check_output_tags "default" "$output"
    check_resource_tags "default" networkInterfaces "$DEFAULT_VM-nic" tagged
    check_resource_tags "default" virtualNetworks "$DEFAULT_VM-vnet" tagged
    check_resource_tags "default" networkSecurityGroups "${DEFAULT_VM}NSG" tagged
    check_resource_tags "default" publicIPAddresses "${DEFAULT_VM}PublicIP" tagged
else
```

- [ ] **Step 5: Tag case 3**

Replace:

```bash
echo "== 3. vm create --public-ip-address $REUSE_PUBLIC_IP --nsg $EXISTING_NSG (existing names) =="
```

with:

```bash
echo "== 3. vm create --public-ip-address $REUSE_PUBLIC_IP --nsg $EXISTING_NSG --tags (existing names) =="
```

Then replace:

```bash
        if output=$(create_vm "$REUSE_VM" --public-ip-address "$REUSE_PUBLIC_IP" ${reuse_nsg_args[@]+"${reuse_nsg_args[@]}"})
        then
            check_public_ip "reuse" "$output" yes "$REUSE_PUBLIC_IP" reuse
            if [[ "$EXISTING_NSG_READY" == "1" ]]
            then
                check_nsg "reuse" "$REUSE_VM" "$EXISTING_NSG_ID" "$EXISTING_NSG_RULE" reuse-nsg
            fi
        else
```

with:

```bash
        if output=$(create_vm "$REUSE_VM" --public-ip-address "$REUSE_PUBLIC_IP" ${reuse_nsg_args[@]+"${reuse_nsg_args[@]}"} "${TAG_ARGS[@]}")
        then
            check_public_ip "reuse" "$output" yes "$REUSE_PUBLIC_IP" reuse
            if [[ "$EXISTING_NSG_READY" == "1" ]]
            then
                check_nsg "reuse" "$REUSE_VM" "$EXISTING_NSG_ID" "$EXISTING_NSG_RULE" reuse-nsg
                check_resource_tags "reuse" networkSecurityGroups "$EXISTING_NSG" untouched
            fi
            check_output_tags "reuse" "$output"
            check_resource_tags "reuse" networkInterfaces "$REUSE_VM-nic" tagged
            check_resource_tags "reuse" virtualNetworks "$REUSE_VM-vnet" tagged
            check_resource_tags "reuse" publicIPAddresses "$REUSE_PUBLIC_IP" untouched
        else
```

`--tags` goes last, because it collects every value up to the next flag.

- [ ] **Step 6: Check the bash script's syntax**

Run, in Git Bash or WSL: `bash -n tests/Scripts/CLI/VMCreate.sh`
Expected: no output, exit code 0.

- [ ] **Step 7: Update the PowerShell script's header**

In `tests/Scripts/PS/VMCreate.ps1`, replace:

```powershell
#   1. New-AzVM                                    -> creates <vm>PublicIP; PublicIps is set;
#                                                     creates <vm>NSG with only the rdp rule
#   2. New-AzVM -PublicIpAddressName <new name>    -> creates that public IP; PublicIps is set
#   3. New-AzVM -PublicIpAddressName <existing>    -> reuses that public IP; PublicIps is set;
#               -SecurityGroupName <existing name>    attaches the pre-created NSG untouched
```

with:

```powershell
#   1. New-AzVM -Tag <tags>                        -> creates <vm>PublicIP; PublicIps is set;
#                                                     creates <vm>NSG with only the rdp rule;
#                                                     tags the VM, NIC, VNet, NSG and public IP
#   2. New-AzVM -PublicIpAddressName <new name>    -> creates that public IP; PublicIps is set
#   3. New-AzVM -PublicIpAddressName <existing>    -> reuses that public IP; PublicIps is set;
#               -SecurityGroupName <existing name>    attaches the pre-created NSG untouched;
#               -Tag <tags>                           tags the VM, NIC and VNet, but neither
#                                                     the pre-created public IP nor the NSG
```

Then replace:

```powershell
# smoke-allow-https, with the tag intact, for the NSG that cases 3 and 4 attach.
# Every other VM gets its own default <vm>NSG.
```

with:

```powershell
# smoke-allow-https, with the tag intact, for the NSG that cases 3 and 4 attach.
# Every other VM gets its own default <vm>NSG.
#
# Cases 1 and 3 pass -Tag @{ 'azclips-tags' = 'vm-create'; 'azclips-empty' = $null }:
# a pair, and a key whose $null value New-AzVM stores as an empty value. The tag checks
# read the VM's tags from New-AzVM's output, and each network resource's tags through
# Invoke-AzRestMethod.
```

- [ ] **Step 8: Add the tags**

Directly after the line `$existingNsgRule   = 'smoke-allow-https/Tcp/*/*/*/443/Allow/Inbound/1100'`, add:

```powershell

# New-AzVM's tags for cases 1 and 3: a pair, and a key whose $null value becomes ''.
$smokeTags = @{ 'azclips-tags' = 'vm-create'; 'azclips-empty' = $null }
```

- [ ] **Step 9: Add the tag checks**

Directly after the closing brace of `function Test-Nsg`, and before `# Guard the cleanup block`, add:

```powershell
# $Tags, a dictionary or a ConvertFrom-Json object, must carry $smokeTags ('tagged') or neither
# of its keys ('untouched'). Keys compare ordinally, as tag keys and the bash checks do.
function Test-SmokeTags([string]$Label, [string]$What, $Tags, [string]$Expect)
{
    $actual = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
    if ($Tags -is [System.Collections.IDictionary])
    {
        foreach ($key in $Tags.Keys) { $actual[$key] = $Tags[$key] }
    }
    elseif ($null -ne $Tags)
    {
        foreach ($property in $Tags.PSObject.Properties) { $actual[$property.Name] = $property.Value }
    }

    $tagged  = $actual['azclips-tags'] -eq 'vm-create' -and $actual.ContainsKey('azclips-empty') -and $actual['azclips-empty'] -eq ''
    $touched = $actual.ContainsKey('azclips-tags') -or $actual.ContainsKey('azclips-empty')
    $shown   = $actual | ConvertTo-Json -Compress

    if ($Expect -eq 'tagged' -and -not $tagged)
    {
        Add-Failure "${Label}: $What lacks azclips-tags=vm-create or an empty azclips-empty: $shown"
    }
    elseif ($Expect -eq 'untouched' -and $touched)
    {
        Add-Failure "${Label}: New-AzVM tagged ${What}: $shown"
    }
    else
    {
        Write-Host "OK: ${Label}: $What is $Expect"
    }
}

function Test-ResourceTags([string]$Label, [string]$Type, [string]$Name, [string]$Expect)
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
    Test-SmokeTags -Label $Label -What "$Type '$Name'" -Tags $resource.tags -Expect $Expect
}

```

- [ ] **Step 10: Tag case 1**

Replace:

```powershell
    Write-Host "== 1. New-AzVM (default public IP ${defaultVm}PublicIP, default NSG ${defaultVm}NSG) =="
    $createdVms.Add($defaultVm)
    $defaultResult = New-SmokeVM -Name $defaultVm
    Test-PublicIp -Label 'default' -Vm $defaultResult -ExpectPublicIp $true -ExpectedPublicIpName "${defaultVm}PublicIP"
    Test-Nsg -Label 'default' -VmName $defaultVm -ExpectedId $defaultNsgId -ExpectedRule $defaultNsgRule
```

with:

```powershell
    Write-Host "== 1. New-AzVM -Tag (default public IP ${defaultVm}PublicIP, default NSG ${defaultVm}NSG) =="
    $createdVms.Add($defaultVm)
    $defaultResult = New-SmokeVM -Name $defaultVm -Extra @{ Tag = $smokeTags }
    Test-PublicIp -Label 'default' -Vm $defaultResult -ExpectPublicIp $true -ExpectedPublicIpName "${defaultVm}PublicIP"
    Test-Nsg -Label 'default' -VmName $defaultVm -ExpectedId $defaultNsgId -ExpectedRule $defaultNsgRule
    Test-SmokeTags -Label 'default' -What "VM '$defaultVm'" -Tags $defaultResult.Tags -Expect 'tagged'
    Test-ResourceTags -Label 'default' -Type 'networkInterfaces' -Name "$defaultVm-nic" -Expect 'tagged'
    Test-ResourceTags -Label 'default' -Type 'virtualNetworks' -Name "$defaultVm-vnet" -Expect 'tagged'
    Test-ResourceTags -Label 'default' -Type 'networkSecurityGroups' -Name "${defaultVm}NSG" -Expect 'tagged'
    Test-ResourceTags -Label 'default' -Type 'publicIPAddresses' -Name "${defaultVm}PublicIP" -Expect 'tagged'
```

- [ ] **Step 11: Tag case 3**

Replace:

```powershell
    Write-Host "== 3. New-AzVM -PublicIpAddressName $reusePublicIp -SecurityGroupName $existingNsg (existing names) =="
```

with:

```powershell
    Write-Host "== 3. New-AzVM -PublicIpAddressName $reusePublicIp -SecurityGroupName $existingNsg -Tag (existing names) =="
```

Then replace:

```powershell
            $reuseExtra = @{ PublicIpAddressName = $reusePublicIp }
            if ($existingNsgReady) { $reuseExtra.SecurityGroupName = $existingNsg }
            $reuseResult = New-SmokeVM -Name $reuseVm -Extra $reuseExtra
            Test-PublicIp -Label 'reuse' -Vm $reuseResult -ExpectPublicIp $true -ExpectedPublicIpName $reusePublicIp -ExpectedSmokeTag 'reuse'
            if ($existingNsgReady)
            {
                Test-Nsg -Label 'reuse' -VmName $reuseVm -ExpectedId $existingNsgId -ExpectedRule $existingNsgRule -ExpectedSmokeTag 'reuse-nsg'
            }
```

with:

```powershell
            $reuseExtra = @{ PublicIpAddressName = $reusePublicIp; Tag = $smokeTags }
            if ($existingNsgReady) { $reuseExtra.SecurityGroupName = $existingNsg }
            $reuseResult = New-SmokeVM -Name $reuseVm -Extra $reuseExtra
            Test-PublicIp -Label 'reuse' -Vm $reuseResult -ExpectPublicIp $true -ExpectedPublicIpName $reusePublicIp -ExpectedSmokeTag 'reuse'
            if ($existingNsgReady)
            {
                Test-Nsg -Label 'reuse' -VmName $reuseVm -ExpectedId $existingNsgId -ExpectedRule $existingNsgRule -ExpectedSmokeTag 'reuse-nsg'
                Test-ResourceTags -Label 'reuse' -Type 'networkSecurityGroups' -Name $existingNsg -Expect 'untouched'
            }
            Test-SmokeTags -Label 'reuse' -What "VM '$reuseVm'" -Tags $reuseResult.Tags -Expect 'tagged'
            Test-ResourceTags -Label 'reuse' -Type 'networkInterfaces' -Name "$reuseVm-nic" -Expect 'tagged'
            Test-ResourceTags -Label 'reuse' -Type 'virtualNetworks' -Name "$reuseVm-vnet" -Expect 'tagged'
            Test-ResourceTags -Label 'reuse' -Type 'publicIPAddresses' -Name $reusePublicIp -Expect 'untouched'
```

- [ ] **Step 12: Check the PowerShell script's syntax**

Run: `pwsh -NoProfile -Command '$errors = $null; $null = [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path "tests/Scripts/PS/VMCreate.ps1").Path, [ref]$null, [ref]$errors); if ($errors) { $errors; exit 1 }'`
Expected: no output, exit code 0.

- [ ] **Step 13: Commit**

```bash
git add tests/Scripts/CLI/VMCreate.sh tests/Scripts/PS/VMCreate.ps1
git commit -m "{Compute} VMCreate smoke scripts: check the tags vm create puts on the VM and its network resources" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 5: Final verification

**Files:** none, unless a check fails. Fix a failure in the task that introduced it, then commit with that task's prefix.

**Interfaces:**
- Consumes: Tasks 1-4.
- Produces: a branch whose build, suites and help pipeline are green, ready for the user's live smoke runs.

- [ ] **Step 1: Build the solution**

Run: `dotnet build azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
Expected: `Build succeeded`.

- [ ] **Step 2: Run the affected suites**

Run each, and expect all passed:
- `dotnet test tests/Extensions.Compute.Tests/Extensions.Compute.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
- `dotnet test tests/Scenario.Tests/Scenario.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
- `dotnet test tests/Cli.Tests/Cli.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`
- `dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`

- [ ] **Step 3: Run the full help pipeline**

Run: `pwsh -File build/build.ps1 -GenerateHelpArtifacts`
Expected: exit code 0. It rebuilds the solution and validates every CLI and PowerShell example against the live metadata and cmdlets, including the two new tag examples.

- [ ] **Step 4: Confirm the working tree is clean**

Run: `git --no-pager status --short`
Expected: no output. If the help pipeline rewrote a tracked file, inspect the diff. Commit it under `{Compute}` only if it belongs to this change; otherwise restore it.

- [ ] **Step 5: Hand the live smoke runs to the user**

They need credentials and quota for four Standard_D2s_v3 VMs and three Standard public IPs, so the user runs them:
- CLI: put `ssh-keygen` on `PATH` (case 4 generates a temporary key pair), set `AZCLIPS_SUBSCRIPTION_ID` and `AZCLIPS_VM_ADMIN_PASSWORD`, sign in with the built CLI, then run `./tests/Scripts/CLI/VMCreate.sh`.
- PowerShell: import `src/PS/Accounts/Accounts.psd1`, `src/PS/Resources/Resources.psd1` and `src/PS/Compute/Compute.psd1`, run `Connect-AzAccount`, set the same variables, then run `./tests/Scripts/PS/VMCreate.ps1`.

Expected: both finish with "smoke test finished" and no `FAIL:` lines. The new `OK:` lines report the VM, NIC, VNet, NSG and public IP as tagged in case 1, and the pre-created public IP and NSG as untouched in case 3.

