# Compute Extension Benchmarks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add benchmarks for the Compute extension (VM List/Create/Get, CLI + PS), publish them to the existing gh-pages dashboards as their own charts, and make the benchmark runner + CI extensible so future extensions plug in with minimal, localized changes.

**Architecture:** Introduce an *extension* dimension alongside the existing *product* (CLI/PS) dimension. Benchmark classes are tagged with a new `[BenchmarkExtension]` attribute and discovered by reflection (no hardcoded type list). A resource-aware HTTP stub returns minimal, correctly-shaped JSON per Azure resource type (loaded from fixtures) so the orchestrated VM-create long-running operation completes instantly instead of polling. CI uses `dorny/paths-filter` to compute a matrix of only the changed extensions.

**Tech Stack:** C# / .NET 10, BenchmarkDotNet 0.13.12, xUnit, PowerShell SDK, GitHub Actions (`dorny/paths-filter`, `benchmark-action/github-action-benchmark`).

**Reference spec:** `docs/superpowers/specs/2026-07-06-compute-extension-benchmarks-design.md`

---

## File Structure

**Benchmark runner (`tests/Benchmark/`):**
- `BenchmarkExtensionAttribute.cs` (create) — the `[BenchmarkExtension("Name")]` marker.
- `Program.cs` (modify) — reflection discovery, `--extension` + `--list` args, per-extension CI artifacts path.
- `BenchmarkBase.cs` (modify) — add `VmCounter`.
- `ChartGenerator.cs` (modify) — derive operation labels from benchmark names (drop hardcoded RG list); generic subtitle. Local/CI-log artifacts only.
- `CLI/ResourceGroupBenchmarks.cs` (modify) — add `[BenchmarkExtension("Resources")]`.
- `PS/ResourceGroupBenchmarks.cs` (modify) — add `[BenchmarkExtension("Resources")]`.
- `CLI/VirtualMachineBenchmarks.cs` (create) — Compute CLI benchmarks.
- `PS/VirtualMachineBenchmarks.cs` (create) — Compute PS benchmarks.
- `Benchmark.csproj` (modify) — reference `Extensions.Compute.csproj`.

**Shared test stub (`tests/Common/`):**
- `Stubs/StubResponseMappings.cs` (create) — explicit path→fixture mapping + fixture loading.
- `Stubs/StubHttpTerminateHandler.cs` (modify) — resolve body via mappings + `Retry-After: 0`.
- `Stubs/Fixtures/virtualNetwork.json`, `virtualMachine.json`, `networkInterface.json`, `networkSecurityGroup.json` (create).
- `Tests.Common.csproj` (modify) — copy fixtures to output.

**Tests (`tests/Cli.Tests/`):**
- `StubHttpTerminateHandlerTests.cs` (create) — unit tests for the resource-aware stub.

**CI (`.github/workflows/`):**
- `benchmark.yml` (modify) — `detect-changes` job + extension matrix + per-extension charts.
- `benchmark-comment.yml` (modify) — download all per-extension result artifacts.

**Docs:**
- `tests/Benchmark/README.md`, `docs/spec/benchmark-workflows.md`, `wiki/02-deep-dive/testing-benchmarking.md` (modify).

---

## Task 1: `[BenchmarkExtension]` attribute

**Files:**
- Create: `tests/Benchmark/BenchmarkExtensionAttribute.cs`

- [ ] **Step 1: Create the attribute**

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Benchmark;

/// <summary>
/// Tags a benchmark class with the Azclips extension it exercises (e.g. "Resources",
/// "Compute"). Used by <see cref="Program"/> to discover and filter benchmarks by
/// extension. Every concrete benchmark class MUST carry this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class BenchmarkExtensionAttribute : Attribute
{
    public string Name { get; }

    public BenchmarkExtensionAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Extension name is required.", nameof(name));
        }
        Name = name;
    }
}
```

- [ ] **Step 2: Build the Benchmark project**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add tests/Benchmark/BenchmarkExtensionAttribute.cs
git commit -m "feat(benchmark): add BenchmarkExtension attribute"
```

---

## Task 2: Tag the Resources benchmark classes

Tag the existing classes now (before the runner switches to reflection in Task 3), so every commit stays green.

**Files:**
- Modify: `tests/Benchmark/CLI/ResourceGroupBenchmarks.cs`
- Modify: `tests/Benchmark/PS/ResourceGroupBenchmarks.cs`

- [ ] **Step 1: Tag the CLI class**

In `tests/Benchmark/CLI/ResourceGroupBenchmarks.cs`, add the attribute above the class declaration:

```csharp
namespace Benchmark.CLI;

[BenchmarkExtension("Resources")]
public class ResourceGroupBenchmarks : CLIBenchmarkBase
{
```

- [ ] **Step 2: Tag the PS class**

In `tests/Benchmark/PS/ResourceGroupBenchmarks.cs`, add the attribute above the class declaration (keep the existing `<summary>` doc comment above it):

```csharp
[BenchmarkExtension("Resources")]
public class ResourceGroupBenchmarks : PSBenchmarkBase
{
```

- [ ] **Step 3: Build**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add tests/Benchmark/CLI/ResourceGroupBenchmarks.cs tests/Benchmark/PS/ResourceGroupBenchmarks.cs
git commit -m "feat(benchmark): tag Resources benchmarks with BenchmarkExtension"
```

---

## Task 3: Reflection-based discovery + `--extension` / `--list` args + per-extension CI path

Replace the hardcoded `GetBenchmarkTypes` switch with reflection, add the `--extension` filter and a `--list` diagnostic, and nest the CI artifacts path under the extension.

**Files:**
- Modify: `tests/Benchmark/Program.cs`

- [ ] **Step 1: Add `--extension` and `--list` parsing helpers**

In `tests/Benchmark/Program.cs`, add these methods next to `ParseProductFilter` / `ParseCiMode`:

```csharp
    private static string? ParseExtensionFilter(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--extension" && i + 1 < args.Length)
            {
                return args[i + 1].ToUpperInvariant();
            }
            if (args[i].StartsWith("--extension="))
            {
                return args[i].Substring("--extension=".Length).ToUpperInvariant();
            }
        }
        return null;
    }

    private static bool ParseListMode(string[] args)
    {
        return args.Any(arg => arg == "--list");
    }
```

- [ ] **Step 2: Replace `GetBenchmarkTypes` with reflection-based discovery**

Replace the entire existing `GetBenchmarkTypes(string? product)` method with the following two methods (note the added `System.Reflection` using at the top of the file if not present):

```csharp
    /// <summary>
    /// Discovers benchmark classes by reflection and filters them by product (inferred
    /// from the base type) and extension (from <see cref="BenchmarkExtensionAttribute"/>).
    /// A class missing the attribute is a hard error — this keeps the taxonomy honest.
    /// </summary>
    private static Type[] GetBenchmarkTypes(string? product, string? extension)
    {
        var allTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(CLI.CLIBenchmarkBase).IsAssignableFrom(t)
                     || typeof(PS.PSBenchmarkBase).IsAssignableFrom(t))
            .ToArray();

        var untagged = allTypes
            .Where(t => t.GetCustomAttribute<BenchmarkExtensionAttribute>() is null)
            .Select(t => t.Name)
            .ToArray();
        if (untagged.Length > 0)
        {
            throw new InvalidOperationException(
                $"Benchmark classes missing [BenchmarkExtension]: {string.Join(", ", untagged)}");
        }

        IEnumerable<Type> filtered = allTypes;

        if (!string.IsNullOrEmpty(product) && product != "ALL")
        {
            filtered = product switch
            {
                "CLI" => filtered.Where(t => typeof(CLI.CLIBenchmarkBase).IsAssignableFrom(t)),
                "PS" => filtered.Where(t => typeof(PS.PSBenchmarkBase).IsAssignableFrom(t)),
                _ => throw new ArgumentException($"Unknown product: {product}. Use: CLI, PS, or ALL")
            };
        }

        if (!string.IsNullOrEmpty(extension) && extension != "ALL")
        {
            filtered = filtered.Where(t => string.Equals(
                t.GetCustomAttribute<BenchmarkExtensionAttribute>()!.Name,
                extension,
                StringComparison.OrdinalIgnoreCase));
        }

        return filtered
            .OrderBy(t => t.GetCustomAttribute<BenchmarkExtensionAttribute>()!.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => ProductOf(t), StringComparer.Ordinal)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ProductOf(Type t) =>
        typeof(CLI.CLIBenchmarkBase).IsAssignableFrom(t) ? "CLI" : "PS";
```

- [ ] **Step 3: Wire the new args into `Main` (parse + list + path + discovery call)**

In `Main`, immediately after the line `var ciMode = ParseCiMode(args);` add:

```csharp
        var extensionFilter = ParseExtensionFilter(args);
        var listMode = ParseListMode(args);
```

Then, immediately before the line `var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");`, add the `--list` short-circuit:

```csharp
        if (listMode)
        {
            var discovered = GetBenchmarkTypes(productFilter, extensionFilter);
            Console.WriteLine($"[Benchmark] Discovered {discovered.Length} class(es):");
            foreach (var t in discovered)
            {
                var ext = t.GetCustomAttribute<BenchmarkExtensionAttribute>()!.Name;
                Console.WriteLine($"  - {ext} / {ProductOf(t)} / {t.Name}");
            }
            return 0;
        }

```

Change the CI artifacts path (the `var artifactsPath = ciMode ? ... : ...;` assignment) to nest under the extension:

```csharp
        var artifactsPath = ciMode
            ? Path.Combine(RepoLocator.GetRepoRoot(), "artifacts", "Benchmark", "ci", extensionFilter ?? "ALL", productFilter ?? "ALL")
            : Path.Combine(RepoLocator.GetRepoRoot(), "artifacts", "Benchmark", timestamp);
```

Change the discovery call from `var benchmarkTypes = GetBenchmarkTypes(productFilter);` to:

```csharp
        var benchmarkTypes = GetBenchmarkTypes(productFilter, extensionFilter);
```

- [ ] **Step 4: Ensure `System.Reflection` is imported**

At the top of `Program.cs`, confirm/add:

```csharp
using System.Reflection;
```

- [ ] **Step 5: Build**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: Build succeeds.

- [ ] **Step 6: Verify discovery with `--list`**

Run: `dotnet run --project tests/Benchmark/Benchmark.csproj -- --list`
Expected output includes exactly the two Resources classes:
```
[Benchmark] Discovered 2 class(es):
  - Resources / CLI / ResourceGroupBenchmarks
  - Resources / PS / ResourceGroupBenchmarks
```

Run: `dotnet run --project tests/Benchmark/Benchmark.csproj -- --list --product CLI`
Expected: only `Resources / CLI / ResourceGroupBenchmarks`.

Run: `dotnet run --project tests/Benchmark/Benchmark.csproj -- --list --extension Resources`
Expected: both Resources classes.

Run: `dotnet run --project tests/Benchmark/Benchmark.csproj -- --list --extension Nope`
Expected: `Discovered 0 class(es):`.

- [ ] **Step 7: Commit**

```bash
git add tests/Benchmark/Program.cs
git commit -m "feat(benchmark): discover benchmarks by reflection + --extension/--list filters"
```

---

## Task 4: Resource-aware HTTP stub with JSON fixtures

Make the shared stub return minimal shaped bodies per resource type (from fixtures) plus `Retry-After: 0`, and unit-test it.

**Files:**
- Create: `tests/Common/Stubs/StubResponseMappings.cs`
- Modify: `tests/Common/Stubs/StubHttpTerminateHandler.cs`
- Create: `tests/Common/Stubs/Fixtures/virtualNetwork.json`
- Create: `tests/Common/Stubs/Fixtures/virtualMachine.json`
- Create: `tests/Common/Stubs/Fixtures/networkInterface.json`
- Create: `tests/Common/Stubs/Fixtures/networkSecurityGroup.json`
- Modify: `tests/Common/Tests.Common.csproj`
- Test: `tests/Cli.Tests/StubHttpTerminateHandlerTests.cs`

- [ ] **Step 1: Create the fixture files**

`tests/Common/Stubs/Fixtures/virtualNetwork.json` (the only shape actually read by the orchestrator — `properties.subnets[].id`):

```json
{
  "id": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/bench-rg/providers/Microsoft.Network/virtualNetworks/bench-vnet",
  "location": "eastus",
  "properties": {
    "subnets": [
      {
        "name": "default",
        "id": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/bench-rg/providers/Microsoft.Network/virtualNetworks/bench-vnet/subnets/default"
      }
    ]
  }
}
```

`tests/Common/Stubs/Fixtures/virtualMachine.json` (terminal `provisioningState` ends LRO polling):

```json
{
  "id": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/bench-rg/providers/Microsoft.Compute/virtualMachines/bench-vm",
  "location": "eastus",
  "properties": { "provisioningState": "Succeeded" }
}
```

`tests/Common/Stubs/Fixtures/networkInterface.json` (orchestrator reads `.Id`):

```json
{
  "id": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/bench-rg/providers/Microsoft.Network/networkInterfaces/bench-nic"
}
```

`tests/Common/Stubs/Fixtures/networkSecurityGroup.json` (orchestrator reads `.Id`):

```json
{
  "id": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/bench-rg/providers/Microsoft.Network/networkSecurityGroups/bench-nsg"
}
```

- [ ] **Step 2: Create `StubResponseMappings`**

`tests/Common/Stubs/StubResponseMappings.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Tests.Common.Stubs;

/// <summary>
/// Explicit mapping from an ARM resource-type path segment to the minimal JSON
/// fixture returned for it by <see cref="StubHttpTerminateHandler"/>. Bodies are
/// loaded from the fixture files once and cached; per-request cost is an in-memory
/// lookup. Add a resource by adding one entry here plus one fixture file under
/// <c>Stubs/Fixtures</c>.
/// </summary>
internal static class StubResponseMappings
{
    /// <summary>Body returned for any request that matches no mapping.</summary>
    public const string DefaultBody = "{}";

    // Resource-type path segment -> fixture file name. The request path carries a
    // varying resource name, so the stable type segment is matched as a substring.
    private static readonly IReadOnlyList<(string Segment, string File)> s_fixtureFiles = new[]
    {
        ("/virtualNetworks/", "virtualNetwork.json"),
        ("/virtualMachines/", "virtualMachine.json"),
        ("/networkInterfaces/", "networkInterface.json"),
        ("/networkSecurityGroups/", "networkSecurityGroup.json"),
    };

    private static readonly IReadOnlyList<(string Segment, string Body)> s_bodies = LoadFixtures();

    /// <summary>
    /// Returns the minimal JSON body for the request path, or <see cref="DefaultBody"/>
    /// when the path matches no mapped resource type.
    /// </summary>
    public static string ResolveBody(string path)
    {
        foreach (var (segment, body) in s_bodies)
        {
            if (path.Contains(segment, StringComparison.OrdinalIgnoreCase))
            {
                return body;
            }
        }
        return DefaultBody;
    }

    private static List<(string, string)> LoadFixtures()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Stubs", "Fixtures");
        var list = new List<(string, string)>(s_fixtureFiles.Count);
        foreach (var (segment, file) in s_fixtureFiles)
        {
            var full = Path.Combine(dir, file);
            if (!File.Exists(full))
            {
                throw new FileNotFoundException(
                    $"Stub fixture not found: '{full}'. Ensure Stubs/Fixtures/*.json are copied to output.",
                    full);
            }
            list.Add((segment, File.ReadAllText(full)));
        }
        return list;
    }
}
```

- [ ] **Step 3: Update the handler to use the mappings**

Replace the whole body of `tests/Common/Stubs/StubHttpTerminateHandler.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Tests.Common.Stubs;

/// <summary>
/// Benchmark/test HTTP terminator: short-circuits every outgoing request with a
/// minimal, structurally-valid ARM response (resolved from <see cref="StubResponseMappings"/>)
/// so the CLI/PS pipeline never calls Azure. A <c>Retry-After: 0</c> header neutralises
/// long-running-operation polling delays. Used only by the Benchmark project.
/// </summary>
public class StubHttpTerminateHandler : IHttpTerminateHandler
{
    public SendAsyncStep SendAsync { get; } = (request, callback, next) =>
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(StubResponseMappings.ResolveBody(path), Encoding.UTF8, "application/json"),
        };
        response.Headers.TryAddWithoutValidation("Retry-After", "0");
        return Task.FromResult(response);
    };
}
```

- [ ] **Step 4: Copy fixtures to output in `Tests.Common.csproj`**

In `tests/Common/Tests.Common.csproj`, add an `ItemGroup` inside `<Project>`:

```xml
  <ItemGroup>
    <Content Include="Stubs\Fixtures\*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Link>Stubs\Fixtures\%(Filename)%(Extension)</Link>
    </Content>
  </ItemGroup>
```

- [ ] **Step 5: Write the failing tests**

`tests/Cli.Tests/StubHttpTerminateHandlerTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Tests.Common.Stubs;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Pins the resource-aware benchmark stub: (1) VNet path returns a body whose
/// properties.subnets[0].id is populated (the field the VM-create orchestrator reads),
/// (2) NSG/NIC paths echo an id, (3) VM path returns a terminal provisioningState,
/// (4) unmapped paths and the list path fall back to "{}", (5) every response carries
/// Retry-After: 0.
/// </summary>
public class StubHttpTerminateHandlerTests
{
    private const string Sub = "00000000-0000-0000-0000-000000000000";

    private static async Task<HttpResponseMessage> Send(string url)
    {
        var handler = new StubHttpTerminateHandler();
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        return await handler.SendAsync(request, null!, null!);
    }

    [Fact]
    public async Task VirtualNetworkPath_ReturnsSubnetWithId()
    {
        var response = await Send($"https://management.azure.com/subscriptions/{Sub}/resourceGroups/bench-rg/providers/Microsoft.Network/virtualNetworks/vnet?api-version=2025-05-01");
        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var subnets = doc.RootElement.GetProperty("properties").GetProperty("subnets");
        Assert.True(subnets.GetArrayLength() >= 1);
        Assert.False(string.IsNullOrEmpty(subnets[0].GetProperty("id").GetString()));
    }

    [Fact]
    public async Task VirtualMachinePath_ReturnsTerminalProvisioningState()
    {
        var response = await Send($"https://management.azure.com/subscriptions/{Sub}/resourceGroups/bench-rg/providers/Microsoft.Compute/virtualMachines/vm?api-version=2025-11-01");
        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var state = doc.RootElement.GetProperty("properties").GetProperty("provisioningState").GetString();
        Assert.Equal("Succeeded", state);
    }

    [Theory]
    [InlineData("Microsoft.Network/networkInterfaces/nic")]
    [InlineData("Microsoft.Network/networkSecurityGroups/nsg")]
    public async Task NicAndNsgPaths_EchoAnId(string resource)
    {
        var response = await Send($"https://management.azure.com/subscriptions/{Sub}/resourceGroups/bench-rg/providers/{resource}?api-version=2025-05-01");
        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task ResourceGroupPath_ReturnsEmptyObject()
    {
        var response = await Send($"https://management.azure.com/subscriptions/{Sub}/resourcegroups/bench-rg?api-version=2025-04-01");
        Assert.Equal("{}", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task VirtualMachineListPath_ReturnsEmptyObject()
    {
        // No trailing slash after "virtualMachines" => a list, not a single VM.
        var response = await Send($"https://management.azure.com/subscriptions/{Sub}/resourceGroups/bench-rg/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01");
        Assert.Equal("{}", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task EveryResponse_SetsRetryAfterZero()
    {
        var response = await Send("https://management.azure.com/anything");
        Assert.Equal("0", Assert.Single(response.Headers.GetValues("Retry-After")));
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~StubHttpTerminateHandlerTests"`
Expected: all 7 test cases PASS (this also proves the fixtures are copied to the test output — a `FileNotFoundException` here means Step 4 needs fixing).

- [ ] **Step 7: Commit**

```bash
git add tests/Common/Stubs/StubResponseMappings.cs tests/Common/Stubs/StubHttpTerminateHandler.cs tests/Common/Stubs/Fixtures tests/Common/Tests.Common.csproj tests/Cli.Tests/StubHttpTerminateHandlerTests.cs
git commit -m "feat(benchmark): resource-aware HTTP stub with JSON fixtures for LRO/orchestrator support"
```

---

## Task 5: Reference the Compute extension from the Benchmark project

Ensures the Compute extension is built into `artifacts/Release` whenever the Benchmark project builds (mirrors the existing Resources reference).

**Files:**
- Modify: `tests/Benchmark/Benchmark.csproj`

- [ ] **Step 1: Add the project reference**

In `tests/Benchmark/Benchmark.csproj`, in the `ItemGroup` that lists `ProjectReference`s, add the Compute reference directly after the Resources one:

```xml
    <ProjectReference Include="..\..\src\Extensions\Resources\Extensions.Resources.csproj" />
    <ProjectReference Include="..\..\src\Extensions\Compute\Extensions.Compute.csproj" />
```

- [ ] **Step 2: Build**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add tests/Benchmark/Benchmark.csproj
git commit -m "build(benchmark): reference Compute extension project"
```

---

## Task 6: Compute CLI benchmarks (`VirtualMachineBenchmarks`)

**Files:**
- Modify: `tests/Benchmark/BenchmarkBase.cs` (add `VmCounter`)
- Create: `tests/Benchmark/CLI/VirtualMachineBenchmarks.cs`

- [ ] **Step 1: Add `VmCounter` to `BenchmarkBase`**

In `tests/Benchmark/BenchmarkBase.cs`, next to `protected int RgCounter = 0;` add:

```csharp
    protected int RgCounter = 0;
    protected int VmCounter = 0;
```

- [ ] **Step 2: Create the CLI benchmark class**

`tests/Benchmark/CLI/VirtualMachineBenchmarks.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.CLI;

/// <summary>
/// CLI benchmarks for the Compute extension — Virtual Machine List / Create / Get,
/// run against the resource-aware stub (no Azure calls). Create exercises the
/// orchestrated `vm create` path (VNet -> NSG -> NIC -> VM). Cold start per call.
/// </summary>
[BenchmarkExtension("Compute")]
public class VirtualMachineBenchmarks : CLIBenchmarkBase
{
    protected override IReadOnlyList<string> RequiredExtensions => new[] { "Compute" };

    private const string ResourceGroup = "bench-rg";

    [Benchmark(Description = "CLI: List VM")]
    public int ListVirtualMachines()
    {
        var exitCode = Core.Cli.Program.Main(
            ["vm", "list", "--subscription-id", SubscriptionId, "--resource-group", ResourceGroup])
            .GetAwaiter().GetResult();
        if (exitCode != 0) throw new InvalidOperationException($"CLI exited with code {exitCode}");
        return exitCode;
    }

    [Benchmark(Description = "CLI: Create VM")]
    public int CreateVirtualMachine()
    {
        var n = Interlocked.Increment(ref VmCounter);
        var exitCode = Core.Cli.Program.Main(
        [
            "vm", "create",
            "--subscription-id", SubscriptionId,
            "--name", $"bench-vm-{n}",
            "--location", "eastus",
            "--resource-group", ResourceGroup,
            "--vm-size", "Standard_D2s_v5",
            "--admin-username", "benchadmin",
            "--admin-password", "Benchmark!Pass123",
            "--network-interface-name", $"bench-nic-{n}",
            "--virtual-network-name", $"bench-vnet-{n}",
            "--network-security-group-name", $"bench-nsg-{n}"
        ]).GetAwaiter().GetResult();
        if (exitCode != 0) throw new InvalidOperationException($"CLI exited with code {exitCode}");
        return exitCode;
    }

    [Benchmark(Description = "CLI: Get VM")]
    public int GetVirtualMachine()
    {
        var exitCode = Core.Cli.Program.Main(
            ["vm", "show", "--subscription-id", SubscriptionId, "--name", "bench-vm-get", "--resource-group", ResourceGroup])
            .GetAwaiter().GetResult();
        if (exitCode != 0) throw new InvalidOperationException($"CLI exited with code {exitCode}");
        return exitCode;
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Verify discovery**

Run: `dotnet run --project tests/Benchmark/Benchmark.csproj -- --list --extension Compute`
Expected: `Compute / CLI / VirtualMachineBenchmarks` is listed (PS class comes in Task 7).

- [ ] **Step 5: Commit**

```bash
git add tests/Benchmark/BenchmarkBase.cs tests/Benchmark/CLI/VirtualMachineBenchmarks.cs
git commit -m "feat(benchmark): add Compute CLI VirtualMachine benchmarks"
```

---

## Task 7: Compute PS benchmarks (`VirtualMachineBenchmarks`)

**Files:**
- Create: `tests/Benchmark/PS/VirtualMachineBenchmarks.cs`

- [ ] **Step 1: Create the PS benchmark class**

`tests/Benchmark/PS/VirtualMachineBenchmarks.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;

namespace Benchmark.PS;

/// <summary>
/// PS benchmarks for the Compute extension — Virtual Machine List / Create / Get,
/// run against the resource-aware stub (no Azure calls). `Get-VM` without -Name lists;
/// with -Name gets. `New-VM` runs the orchestrated create (VNet -> NSG -> NIC -> VM).
/// Cold start: fresh runspace + module import + DI init per call.
/// </summary>
[BenchmarkExtension("Compute")]
public class VirtualMachineBenchmarks : PSBenchmarkBase
{
    protected override string ModuleName => "Compute";

    private const string ResourceGroup = "bench-rg";

    [Benchmark(Description = "PS: List VM")]
    public object? ListVirtualMachines()
    {
        return RunPowerShellCommand("Get-VM",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup));
    }

    [Benchmark(Description = "PS: Create VM")]
    public object? CreateVirtualMachine()
    {
        var n = Interlocked.Increment(ref VmCounter);
        return RunPowerShellCommand("New-VM",
            ("SubscriptionId", SubscriptionId),
            ("Name", $"bench-vm-{n}"),
            ("Location", "eastus"),
            ("ResourceGroupName", ResourceGroup),
            ("VmSize", "Standard_D2s_v5"),
            ("AdminUsername", "benchadmin"),
            ("AdminPassword", "Benchmark!Pass123"),
            ("NetworkInterfaceName", $"bench-nic-{n}"),
            ("VirtualNetworkName", $"bench-vnet-{n}"),
            ("NetworkSecurityGroupName", $"bench-nsg-{n}"));
    }

    [Benchmark(Description = "PS: Get VM")]
    public object? GetVirtualMachine()
    {
        return RunPowerShellCommand("Get-VM",
            ("SubscriptionId", SubscriptionId),
            ("ResourceGroupName", ResourceGroup),
            ("Name", "bench-vm-get"));
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Verify discovery**

Run: `dotnet run --project tests/Benchmark/Benchmark.csproj -- --list --extension Compute`
Expected: both `Compute / CLI / VirtualMachineBenchmarks` and `Compute / PS / VirtualMachineBenchmarks`.

Run: `dotnet run --project tests/Benchmark/Benchmark.csproj -- --list`
Expected: all four classes (Compute CLI/PS + Resources CLI/PS).

- [ ] **Step 4: Commit**

```bash
git add tests/Benchmark/PS/VirtualMachineBenchmarks.cs
git commit -m "feat(benchmark): add Compute PS VirtualMachine benchmarks"
```

---

## Task 8: Generalize `ChartGenerator` for any extension

`ChartGenerator` produces the local/CI-log HTML + JSON artifacts (it does **not** feed the gh-pages dashboard, the regression gate, or PR comments — those come from BenchmarkDotNet's own report JSON). Today it hardcodes the three Resource-Group operations, so a Compute run would show empty/misleading rows. Derive operation labels from the benchmark display names instead, so the local charts work for any extension automatically.

**Files:**
- Modify: `tests/Benchmark/ChartGenerator.cs`

- [ ] **Step 1: Replace `CalculateOverhead` with a dynamic version**

In `tests/Benchmark/ChartGenerator.cs`, replace the entire `CalculateOverhead` method (the one beginning `private static List<OverheadData> CalculateOverhead`) with:

```csharp
    private static List<OverheadData> CalculateOverhead(List<ChartDataPoint> data)
    {
        // Derive the operation label from each benchmark's display name by stripping the
        // leading product prefix ("CLI: " / "PS: "). This keeps the comparison view
        // extension-agnostic — whatever operations ran show up automatically, in
        // first-seen order — instead of a hardcoded Resource-Group list.
        return data
            .Select(x => OperationLabel(x.Method))
            .Distinct()
            .Select(op =>
            {
                var cliResult = data.FirstOrDefault(x => OperationLabel(x.Method) == op && x.Product == "CLI");
                var psResult = data.FirstOrDefault(x => OperationLabel(x.Method) == op && x.Product == "PS");

                return new OverheadData
                {
                    Operation = op,
                    CliTime = Math.Round(cliResult?.MeanSeconds ?? 0, 4),
                    PsTime = Math.Round(psResult?.MeanSeconds ?? 0, 4)
                };
            }).ToList();
    }

    // "CLI: List VM" -> "List VM"; "PS: Create RG" -> "Create RG"; falls back to the
    // whole trimmed name when there is no "<product>: " prefix.
    private static string OperationLabel(string method)
    {
        var colon = method.IndexOf(':');
        return colon >= 0 ? method[(colon + 1)..].Trim() : method.Trim();
    }
```

- [ ] **Step 2: Generalize the comparison subtitle**

In the same file, inside `GenerateGroupedBarChart`, replace the hardcoded subtitle line (note the doubled quotes — it is inside a C# verbatim string):

```html
        <p class=""subtitle"">Execution time comparison across CLI and PowerShell for Resource Group operations</p>
```

with:

```html
        <p class=""subtitle"">Execution time comparison across CLI and PowerShell</p>
```

- [ ] **Step 3: Build**

Run: `dotnet build tests/Benchmark/Benchmark.csproj`
Expected: Build succeeds.

(Functional verification happens in Task 9's smoke run, which inspects the generated summary JSON.)

- [ ] **Step 4: Commit**

```bash
git add tests/Benchmark/ChartGenerator.cs
git commit -m "refactor(benchmark): derive chart operations from benchmark names (extension-agnostic)"
```

---

## Task 9: Smoke-validate the benchmarks end-to-end

Confirm both extensions build and run to completion (including the orchestrated create) at a low iteration count. This uses the `validate-benchmarks` skill's technique.

**Files:**
- Temporarily modify (then revert): `tests/Benchmark/CLI/CLIBenchmarkBase.cs`, `tests/Benchmark/PS/PSBenchmarkBase.cs`

- [ ] **Step 1: Build the whole solution in Release**

Run: `dotnet build azclips.slnx -c Release`
Expected: Build succeeds (populates `artifacts/Release/` with Compute + Resources + Cmdlets DLLs).

- [ ] **Step 2: Stage PS module manifests**

Run: `./build/Set-ModuleConfiguration.ps1 -c Release`
Expected: Completes without error.

- [ ] **Step 3: Lower the iteration count to 5 (temporary)**

- In `tests/Benchmark/CLI/CLIBenchmarkBase.cs`: change `[IterationCount(10000)]` → `[IterationCount(5)]`.
- In `tests/Benchmark/PS/PSBenchmarkBase.cs`: change `[IterationCount(10000)]` → `[IterationCount(5)]`.

- [ ] **Step 4: Run the Compute benchmarks (local mode)**

Run: `dotnet run --project tests/Benchmark/Benchmark.csproj -c Release --no-build -- --local --extension Compute --product ALL`
Expected: ends with `[Benchmark] PASS — all benchmarks completed successfully.` and exit code 0. In particular the "Create VM" cases must complete quickly (proves the `Retry-After: 0` + shaped-VNet fixture defeat the LRO polling and the `Subnets` NRE).

- [ ] **Step 5: Run the full suite (regression check)**

Run: `dotnet run --project tests/Benchmark/Benchmark.csproj -c Release --no-build -- --local --extension ALL --product ALL`
Expected: ends with `[Benchmark] PASS`; both Compute and Resources classes run.

- [ ] **Step 6: Inspect the generated summary JSON (verifies Task 8's ChartGenerator change)**

The `--local` runs above wrote `artifacts/Benchmark/<timestamp>/benchmark-summary-*.json`. Confirm the latest summary lists the VM operations by their derived labels (not the old hardcoded RG labels):

Run: `Get-Content (Get-ChildItem artifacts/Benchmark -Recurse -Filter 'benchmark-summary-*.json' | Sort-Object LastWriteTime | Select-Object -Last 1).FullName | Select-String 'List VM','Create VM','Get VM'`
Expected: matches for `List VM`, `Create VM`, and `Get VM` (the full-suite run's summary also contains the RG operations). This proves the dynamic operation derivation replaced the hardcoded list.

- [ ] **Step 7: ALWAYS restore the iteration count to 10000**

- In `tests/Benchmark/CLI/CLIBenchmarkBase.cs`: change `[IterationCount(5)]` → `[IterationCount(10000)]`.
- In `tests/Benchmark/PS/PSBenchmarkBase.cs`: change `[IterationCount(5)]` → `[IterationCount(10000)]`.

- [ ] **Step 8: Verify restore**

Run: `git diff -- tests/Benchmark/CLI/CLIBenchmarkBase.cs tests/Benchmark/PS/PSBenchmarkBase.cs`
Expected: empty diff (both files back to `[IterationCount(10000)]`, no `[IterationCount(5)]` remaining).

No commit for this task (validation only; no intended file changes remain).

---

## Task 10: CI workflow — path-filter matrix + per-extension charts

Rework `benchmark.yml` into a `detect-changes` job (computes which extensions changed) plus a matrix `benchmark` job that runs and publishes one extension at a time. Standardize the Resources chart names.

**Files:**
- Modify: `.github/workflows/benchmark.yml`

- [ ] **Step 1: Replace the workflow with the matrix version**

Replace the entire contents of `.github/workflows/benchmark.yml` with:

```yaml
name: Performance Benchmarks

on:
  push:
    branches: [main]
    paths:
      - 'src/**'
      - 'tests/Benchmark/**'
      - 'tests/Common/**'
      - 'global.json'
      - 'azclips.slnx'
      - '.github/workflows/benchmark.yml'
  pull_request:
    branches: [main]
    paths:
      - 'src/**'
      - 'tests/Benchmark/**'
      - 'tests/Common/**'
      - 'global.json'
      - 'azclips.slnx'
      - '.github/workflows/benchmark.yml'
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: benchmark-${{ github.ref }}
  cancel-in-progress: false

jobs:
  # ─────────────── Detect which extensions changed ───────────────
  detect-changes:
    name: Detect changed extensions
    runs-on: ubuntu-latest
    outputs:
      matrix: ${{ steps.set-matrix.outputs.matrix }}
      has_work: ${{ steps.set-matrix.outputs.has_work }}
    steps:
      - name: Harden runner
        uses: step-security/harden-runner@v2
        with:
          egress-policy: audit

      - uses: actions/checkout@v4

      - uses: dorny/paths-filter@v3
        id: filter
        with:
          filters: |
            shared:
              - 'src/Core/**'
              - 'tests/Benchmark/*.cs'
              - 'tests/Common/**'
              - 'global.json'
              - 'azclips.slnx'
              - 'Shared.props'
              - '.github/workflows/benchmark.yml'
            compute:
              - 'src/Extensions/Compute/**'
              - 'src/PS/Compute/**'
              - 'tests/Benchmark/CLI/VirtualMachineBenchmarks.cs'
              - 'tests/Benchmark/PS/VirtualMachineBenchmarks.cs'
            resources:
              - 'src/Extensions/Resources/**'
              - 'src/PS/Resources/**'
              - 'tests/Benchmark/CLI/ResourceGroupBenchmarks.cs'
              - 'tests/Benchmark/PS/ResourceGroupBenchmarks.cs'

      - name: Build extension matrix
        id: set-matrix
        env:
          SHARED: ${{ steps.filter.outputs.shared }}
          COMPUTE: ${{ steps.filter.outputs.compute }}
          RESOURCES: ${{ steps.filter.outputs.resources }}
          EVENT: ${{ github.event_name }}
        run: |
          ALL='["compute","resources"]'
          if [ "$EVENT" = "workflow_dispatch" ] || [ "$SHARED" = "true" ]; then
            MATRIX="$ALL"
          else
            exts=()
            [ "$COMPUTE" = "true" ] && exts+=("compute")
            [ "$RESOURCES" = "true" ] && exts+=("resources")
            if [ ${#exts[@]} -eq 0 ]; then
              MATRIX='[]'
            else
              MATRIX=$(printf '"%s",' "${exts[@]}")
              MATRIX="[${MATRIX%,}]"
            fi
          fi
          echo "matrix={\"extension\":$MATRIX}" >> "$GITHUB_OUTPUT"
          if [ "$MATRIX" = "[]" ]; then
            echo "has_work=false" >> "$GITHUB_OUTPUT"
          else
            echo "has_work=true" >> "$GITHUB_OUTPUT"
          fi
          echo "Resolved matrix: {\"extension\":$MATRIX}"

  # ─────────────── Run + publish per extension ───────────────
  benchmark:
    name: Benchmark ${{ matrix.extension }}
    needs: detect-changes
    if: needs.detect-changes.outputs.has_work == 'true'
    runs-on: [self-hosted, Linux, X64, benchmark]
    permissions:
      contents: write          # gh-pages auto-push (main only)
      pull-requests: write     # PR comments on regression
    strategy:
      fail-fast: false
      max-parallel: 1          # serialize gh-pages pushes
      matrix: ${{ fromJSON(needs.detect-changes.outputs.matrix) }}
    steps:
      - name: Harden runner
        uses: step-security/harden-runner@v2
        with:
          egress-policy: audit

      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Compute display title
        id: names
        run: |
          ext="${{ matrix.extension }}"
          echo "title=$(tr '[:lower:]' '[:upper:]' <<< ${ext:0:1})${ext:1}" >> "$GITHUB_OUTPUT"

      - name: Restore + build
        run: dotnet build azclips.slnx --configuration Release

      - name: Clean CI artifacts
        run: rm -rf artifacts/Benchmark/ci

      # ─────────────── CLI ───────────────
      - name: Run CLI benchmarks (with retry)
        uses: nick-fields/retry@v3
        with:
          timeout_minutes: 20
          max_attempts: 2
          retry_wait_seconds: 30
          command: dotnet run --project tests/Benchmark -c Release --no-build -- --ci --extension ${{ matrix.extension }} --product CLI

      - name: Resolve CLI report path
        id: cli-report
        run: echo "path=$(ls artifacts/Benchmark/ci/*/CLI/results/*-report-full-compressed.json | head -n1)" >> "$GITHUB_OUTPUT"

      - name: Publish CLI dashboard / gate regression
        uses: benchmark-action/github-action-benchmark@v1
        with:
          name: 'Azclips ${{ steps.names.outputs.title }} CLI Benchmarks'
          tool: 'benchmarkdotnet'
          output-file-path: ${{ steps.cli-report.outputs.path }}
          github-token: ${{ secrets.GITHUB_TOKEN }}
          auto-push: ${{ github.event_name == 'push' }}
          alert-threshold: '115%'
          fail-on-alert: false
          comment-on-alert: ${{ github.event.pull_request.head.repo.fork != true }}
          summary-always: true
          gh-pages-branch: gh-pages
          benchmark-data-dir-path: dev/bench/cli
          comment-always: ${{ github.event.pull_request.head.repo.fork != true }}

      # ─────────────── PS ────────────────
      - name: Run PS benchmarks (with retry)
        uses: nick-fields/retry@v3
        with:
          timeout_minutes: 20
          max_attempts: 2
          retry_wait_seconds: 30
          command: dotnet run --project tests/Benchmark -c Release --no-build -- --ci --extension ${{ matrix.extension }} --product PS

      - name: Resolve PS report path
        id: ps-report
        run: echo "path=$(ls artifacts/Benchmark/ci/*/PS/results/*-report-full-compressed.json | head -n1)" >> "$GITHUB_OUTPUT"

      - name: Publish PS dashboard / gate regression
        uses: benchmark-action/github-action-benchmark@v1
        with:
          name: 'Azclips ${{ steps.names.outputs.title }} PS Benchmarks'
          tool: 'benchmarkdotnet'
          output-file-path: ${{ steps.ps-report.outputs.path }}
          github-token: ${{ secrets.GITHUB_TOKEN }}
          auto-push: ${{ github.event_name == 'push' }}
          alert-threshold: '115%'
          fail-on-alert: false
          comment-on-alert: ${{ github.event.pull_request.head.repo.fork != true }}
          summary-always: true
          gh-pages-branch: gh-pages
          benchmark-data-dir-path: dev/bench/ps
          comment-always: ${{ github.event.pull_request.head.repo.fork != true }}
          skip-fetch-gh-pages: true

      # Upload CLI + PS benchmark results for the fork-PR comment workflow
      - name: Upload benchmark results for fork PR
        if: always() && github.event_name == 'pull_request' && github.event.pull_request.head.repo.fork == true
        uses: actions/upload-artifact@v4
        with:
          name: benchmark-results-${{ matrix.extension }}
          path: artifacts/Benchmark/ci/**/results/*-report-full-compressed.json
          retention-days: 1

      # ─────────────── Always ────────────
      - name: Upload BDN logs (always)
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: benchmark-ci-${{ matrix.extension }}-${{ github.run_id }}
          path: artifacts/Benchmark/ci/**
          retention-days: 14
```

- [ ] **Step 2: Validate the workflow YAML syntax**

Run: `python -c "import yaml; yaml.safe_load(open('.github/workflows/benchmark.yml')); print('YAML OK')"`
Expected: `YAML OK`. (If `python`/`pyyaml` is unavailable, confirm indentation visually; GitHub validates on push.)

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/benchmark.yml
git commit -m "ci(benchmark): path-filter matrix over extensions + per-extension gh-pages charts"
```

---

## Task 11: Fork-PR comment workflow — consume all per-extension artifacts

The matrix now uploads one `benchmark-results-<ext>` artifact per extension. Update the comment workflow to download all of them.

**Files:**
- Modify: `.github/workflows/benchmark-comment.yml`

- [ ] **Step 1: Download all per-extension result artifacts**

In `.github/workflows/benchmark-comment.yml`, replace the "Download benchmark results artifact" step with a pattern download that merges all extensions:

```yaml
      - name: Download benchmark results artifacts
        id: download
        uses: actions/download-artifact@v4
        with:
          pattern: benchmark-results-*
          path: ${{ runner.temp }}/benchmark-results
          run-id: ${{ github.event.workflow_run.id }}
          github-token: ${{ secrets.GITHUB_TOKEN }}
          merge-multiple: true
        continue-on-error: true
```

(The existing Python step already walks every JSON under `RESULTS_DIR`, classifies each by the `Benchmark.CLI.` / `Benchmark.PS.` prefix, and matches by `FullName` against the two `dev/bench/<product>/data.js` files, so multiple extensions require no further changes.)

- [ ] **Step 2: Validate the workflow YAML syntax**

Run: `python -c "import yaml; yaml.safe_load(open('.github/workflows/benchmark-comment.yml')); print('YAML OK')"`
Expected: `YAML OK`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/benchmark-comment.yml
git commit -m "ci(benchmark): comment workflow downloads all per-extension result artifacts"
```

---

## Task 12: Documentation

**Files:**
- Modify: `tests/Benchmark/README.md`
- Modify: `docs/spec/benchmark-workflows.md`
- Modify: `wiki/02-deep-dive/testing-benchmarking.md`

- [ ] **Step 1: Update `tests/Benchmark/README.md`**

Make these edits:

- In the "Command Line Options" table, add a row for `--extension` and one for `--list`:

```markdown
| `--extension <name>` | Run a specific extension only (`Compute`, `Resources`, or `ALL`) | `-- --extension Compute` |
| `--list` | Print the discovered benchmark classes (extension / product / name) and exit | `-- --list` |
```

- In "Current Test Scenarios", add a Compute section after the Resource Group table:

```markdown
### Compute (Virtual Machine)

Azure Virtual Machine operations, run against the resource-aware stubbed HTTP (no real Azure calls):

| Operation | CLI command | PowerShell cmdlet | Notes |
|-----------|-------------|-------------------|-------|
| List | `vm list` | `Get-VM` (by resource group) | GET, non-LRO |
| Create | `vm create` | `New-VM` | Orchestrated: VNet -> NSG -> NIC -> VM (LRO) |
| Get | `vm show` | `Get-VM -Name` | GET, non-LRO |

Create is a long-running operation; the benchmark stub returns `Retry-After: 0` and a
minimally-shaped virtual-network body so the orchestrated create completes without
real polling. See "Project Structure" and `tests/Common/Stubs/`.
```

- In "Project Structure", add the new files under `CLI/` and `PS/` and note the extension attribute:

```markdown
├── BenchmarkExtensionAttribute.cs  # [BenchmarkExtension("Name")] discovery marker
├── CLI/
│   ├── ...
│   ├── ResourceGroupBenchmarks.cs                # CLI: List / Create / Get RG  [Resources]
│   └── VirtualMachineBenchmarks.cs               # CLI: List / Create / Get VM  [Compute]
└── PS/
    ├── ...
    ├── ResourceGroupBenchmarks.cs                # PS: List / Create / Get RG   [Resources]
    └── VirtualMachineBenchmarks.cs               # PS: List / Create / Get VM   [Compute]
```

- Add a short "Adding a new extension" subsection near the end:

```markdown
## Adding a New Extension

1. Add `CLI/<Resource>Benchmarks.cs` and `PS/<Resource>Benchmarks.cs`, each tagged
   `[BenchmarkExtension("<Name>")]` and overriding `RequiredExtensions` / `ModuleName`.
2. If the extension makes orchestrated/LRO calls that read response fields, add the
   minimal shape to `tests/Common/Stubs/StubResponseMappings.cs` + a fixture file.
3. Add a `<name>:` filter block (and its files) in `.github/workflows/benchmark.yml`.

Discovery (reflection), the CI matrix, the report glob, and the comment workflow pick
it up automatically — no `Program.cs` or matrix-list edits required.
```

- [ ] **Step 2: Update `docs/spec/benchmark-workflows.md`**

- Update the "Dashboard" bullets to note per-extension charts on the same pages:

```markdown
**Dashboards** (one page per product; one chart per extension):
- CLI — https://azure.github.io/azclips/dev/bench/cli/ (charts: `Azclips <Extension> CLI Benchmarks`)
- PS — https://azure.github.io/azclips/dev/bench/ps/ (charts: `Azclips <Extension> PS Benchmarks`)
```

- Under `benchmark.yml`, add a paragraph:

```markdown
A `detect-changes` job uses `dorny/paths-filter` to determine which extensions changed
and emits a matrix. Changes to shared code (`src/Core/**`, the benchmark runner/stubs,
`global.json`, `azclips.slnx`) run **all** extensions; changes scoped to a single
extension (`src/Extensions/<Ext>/**`, `src/PS/<Ext>/**`, or its benchmark classes) run
only that extension. `workflow_dispatch` runs all. The matrix `benchmark` job builds,
runs `--ci --extension <ext> --product CLI|PS`, and publishes each report to
`dev/bench/cli` / `dev/bench/ps` under its own chart name.
```

- [ ] **Step 3: Update `wiki/02-deep-dive/testing-benchmarking.md`**

Find the section describing `StubHttpTerminateHandler` and update it to describe the
resource-aware behavior:

```markdown
`StubHttpTerminateHandler` (in `tests/Common/Stubs/`) short-circuits every outgoing
request so benchmarks never call Azure. It resolves a minimal, correctly-shaped ARM
response per resource type via `StubResponseMappings` (backed by JSON fixtures under
`Stubs/Fixtures/`), and sets `Retry-After: 0` so long-running-operation polling does
not wait. Unmapped paths return `{}`. This lets orchestrated creates (e.g. Compute
`vm create`: VNet -> NSG -> NIC -> VM) run to completion under benchmark conditions.
```

- [ ] **Step 4: Commit**

```bash
git add tests/Benchmark/README.md docs/spec/benchmark-workflows.md wiki/02-deep-dive/testing-benchmarking.md
git commit -m "docs(benchmark): document extension dimension, Compute scenarios, and resource-aware stub"
```

---

## Task 13: Final full-suite verification

- [ ] **Step 1: Build the solution in Release**

Run: `dotnet build azclips.slnx -c Release`
Expected: Build succeeds.

- [ ] **Step 2: Run the full unit-test regression for the touched test project**

Run: `dotnet test tests/Cli.Tests/Cli.Tests.csproj`
Expected: all tests pass (including `StubHttpTerminateHandlerTests`).

- [ ] **Step 3: Confirm CI report paths resolve locally (simulates the workflow glob)**

Run:
```bash
dotnet run --project tests/Benchmark -c Release --no-build -- --ci --extension Compute --product CLI
ls artifacts/Benchmark/ci/*/CLI/results/*-report-full-compressed.json
```
Expected: the `--ci` run finishes (`[Benchmark] PASS`) and the `ls` prints exactly one JSON path (this is the file the workflow's "Resolve CLI report path" step globs). Note: this is a full 10,000-iteration run and may take a while; if you only need to confirm the path shape, you may keep the temporary `[IterationCount(5)]` from Task 9's technique and restore afterward.

- [ ] **Step 4: Confirm no stray edits remain**

Run: `git status --short`
Expected: clean (all work committed; no `[IterationCount(5)]` left behind).

---

## Notes for the implementer

- **Do not** commit anything under `artifacts/` (it is gitignored and regenerated).
- The two `[IterationCount(10000)]` attributes must never be left at `5` — Task 9 and any local smoke runs must restore them (see the `validate-benchmarks` skill).
- The stub change lives in `tests/Common` and is consumed only by the Benchmark project; the added unit test in `Cli.Tests` is what guards it. If `StubHttpTerminateHandlerTests` throws `FileNotFoundException`, the fixture copy-to-output (Task 4, Step 4) is misconfigured — fix that before proceeding.
- Chart-name casing: the CI `names` step title-cases the lowercase matrix id (`compute` -> `Compute`). The benchmark run receives the lowercase id via `--extension`; the runner upper-cases it internally only for the artifacts directory, and the workflow globs that directory with `*`, so casing never needs to line up by hand.
