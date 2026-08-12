# @azure-client-engine/generator

Roslyn-based **C# client generator** — stage 3 of the Azure Client Engine
pipeline (`emit → transform → generate`). It consumes a validated IR document and
returns generated C# source files.

This package is a **plain .NET class library**: it exposes the generation API and
the IR POCOs. It is **not** an npm-workspace member — the `ace` CLI integrates
with it by spawning a `dotnet` process. Hosting/integration with the CLI is
intentionally **out of scope** here and is wired up separately.

## Layout

```
packages/generator/
  Directory.Build.props               Shared MSBuild settings (net10.0, nullable, ...)
  Ace.Generator.csproj                The generator library (project at the package root)
  src/
    Generation/                       Generation API + orchestrator + shared helpers
      IClientGenerator.cs               Generator contract (mirrors the TS `Generator` seam)
      CSharpClientGenerator.cs          Orchestrator: runs the artifact generators, merges output
      IArtifactGenerator.cs             Contract for one artifact-producing stage
      GenerationContext.cs              Shared run state (file + diagnostic sinks)
      GenerationResult.cs               Returned files + diagnostics
      GeneratorOptions.cs               Host-resolved options
      GeneratorDiagnostic.cs            Structured diagnostic
      Naming.cs / CSharpStub.cs         Identifier + Roslyn stub helpers
      Generators/                       The artifact generators (stubs for now)
        ModelGenerator.cs                 IR models  -> model classes
        ProcessorGenerator.cs             IR operations -> processor classes (IProcessor)
        ProcessorBaseGenerator.cs         Template-like processor base (runtime wiring)
        ProjectStubGenerator.cs           .csproj / project metadata / resources
    Model/
      Ir.Generated.cs                   IR POCOs, generated from ir.schema.json
      IrJson.cs                         Canonical System.Text.Json options for IR (de)serialization
  test/Ace.Generator.Tests/           xUnit tests (incl. polymorphic round-trip)
  tools/AceIrModelGen/                Codegen tool (JSON Schema -> C# POCOs, via Roslyn)
    Program.cs                          Entry point (arg parsing, file I/O)
    IrModelGenerator.cs                 JSON Schema -> C# mapping + Roslyn emission
    SchemaModel.cs                      Internal model (enum/class/property build records)
```

There is no solution file: the library is the single project at the package root,
with the test and codegen-tool projects in subfolders (built individually). The
library csproj excludes `test/**` and `tools/**` from its source glob so those
nested projects are not compiled into it.

## Build / test

Uses the .NET SDK pinned by the repo `global.json`. Standalone .NET, no npm —
target each project by path:

```bash
# build the library (run from this package directory)
dotnet build Ace.Generator.csproj

# run the tests (also builds the library via its project reference)
dotnet test test/Ace.Generator.Tests/Ace.Generator.Tests.csproj
```

## IR POCOs (single source of truth)

The IR is defined once, in Zod, in `@azure-client-engine/ir`. To avoid
hand-duplicating it in C#, the POCOs in `src/Model/Ir.Generated.cs` are
**code-generated** from the IR JSON Schema artifact, which is itself derived from
the Zod schema. The `AceIrModelGen` tool maps JSON Schema → C# itself (using
**Roslyn** to emit the code — no third-party codegen library). Regenerating is a
two-step flow across the TS/.NET boundary:

```bash
# 1) Emit the IR JSON Schema (owned by the `ir` package; writes packages/ir/dist/ir.schema.json).
#    Run from the toolkit root, or anywhere with `-w`:
npm run schema -w @azure-client-engine/ir

# 2) Generate the C# POCOs (run from this package directory).
#    With no arguments the tool reads ../ir/dist/ir.schema.json and writes
#    src/Model/Ir.Generated.cs:
dotnet run --project tools/AceIrModelGen
```

The tool accepts `--schema <path>`, `--out <path>`, and `--namespace <ns>` to
override the defaults, and errors with a hint if the schema artifact is missing.
Re-run both steps whenever the IR schema changes. `src/Model/Ir.Generated.cs` is
committed so the library builds out-of-the-box; treat it as generated output (do
not hand-edit).

### Discriminated unions

The IR's discriminated unions — `IRTypeRef`, `IRProperty`, `IRStatusCode`,
`IRLiteralValue`, `IRServer`, `IROperationSemantics`, `IRAuthScheme`,
`IROAuth2Flow` — are emitted by `zod-to-json-schema` as `anyOf` + `const`
discriminators. `AceIrModelGen` maps each to an **`abstract` base + derived
types** wired for `System.Text.Json` polymorphism (`[JsonPolymorphic]` /
`[JsonDerivedType]`). Enums map to C# enums with `[JsonStringEnumMemberName]` for
wire fidelity (e.g. `date-time`).

Because the emitted IR does not place the discriminator first in each JSON
object, **deserialize IR via `IrJson.Options` / `IrJson.Deserialize`** (which sets
`AllowOutOfOrderMetadataProperties = true`); deserializing with default options
throws on the out-of-order discriminator.

## Generation API

`IClientGenerator.Generate(IRDocument ir, GeneratorOptions options, CancellationToken)`
returns a `GenerationResult` (`Files` map + `Diagnostics`). Implementations are
**pure**: no disk writes, no console output, no process side effects — matching
the backbone's host-agnostic contract.

`CSharpClientGenerator` is the orchestrator: it runs a fixed set of
**artifact generators** (`IArtifactGenerator`), each contributing files and
diagnostics through a shared `GenerationContext`:

| Stage | Type | Responsibility |
|-------|------|----------------|
| model | `ModelGenerator` | IR models (and enums) → C# model types |
| processor | `ProcessorGenerator` | IR operations → one processor class each (IProcessor pattern) |
| base | `ProcessorBaseGenerator` | template-like processor base that wires runtime dependencies |
| stub | `ProjectStubGenerator` | `.csproj`, assembly metadata, resources |

The stages are **scaffolds** today: model/processor/base emit empty
`partial class`es showing the intended file/namespace/type shape, and the stub
emits a minimal `.csproj`. Each carries a `TODO` describing the real emission
that replaces it. This split lets the model, operation, runtime-wiring, and
project concerns evolve independently.
