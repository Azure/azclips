# @azure-client-engine/generator

Roslyn-based **C# client generator** — stage 3 of the Azure Client Engine
pipeline (`emit → transform → generate`). It consumes a validated IR document and
returns generated C# source files.

This package is a **plain .NET class library** (`Ace.Generator.csproj`): it exposes
the generation API and the IR POCOs. It is **not** an npm-workspace member. A thin
executable, `host/Ace.Generator.Host.csproj`, adapts the library to the compiler's
out-of-process **`csharp-generate` tool** contract (JSON IR on stdin → NDJSON
files on stdout); the `ace` CLI's host runtime spawns it via `dotnet`. See
[Out-of-process host](#out-of-process-host).

## Layout

```
packages/generator/
  Directory.Build.props               Shared MSBuild settings (net10.0, nullable, ...)
  Ace.Generator.csproj                The generator library (project at the package root)
  src/
    Generation/                       Generation API, orchestrator + shared helpers
      IClientGenerator.cs               Generator contract + GeneratedFileHandler delegate (async, mirrors the TS `Generator` seam)
      CSharpClientGenerator.cs          Orchestrator: drives the artifact generators, streams + validates output
      EmitterExtensions.cs              Shared emit helpers: Identifier/Pascal, relative-path normalization, XML-doc trivia
      IrTypeMapper.cs                   IRTypeRef -> C# type expression
      EnumValueDocs.cs                  An enum's values as the XML-doc <remarks> of each member it types
      Generators/                       The artifact generators (pull-based)
        IArtifactGenerator.cs             Stage contract (`Produce` yields files lazily)
        ModelGenerator.cs                 IR models  -> model classes (real emission; owns the Roslyn model builder)
        ProcessorGenerator.cs             IR operations -> processor classes (ProcessorBase<TRequest,TResponse>; seam overrides)
        ProcessorBaseGenerator.cs         Processor runtime base: marker + generic ProcessorBase<TRequest,TResponse> (real emission)
        ProjectStubGenerator.cs           extension .csproj (System.Xml.Linq; per-artifact …Emit)
    Model/                            Run-facing types + generated IR POCOs
      GeneratedFile.cs                  A produced artifact (relative path + contents + class name)
      CSharpClass.cs                    Roslyn single-class scaffold (used by the stub generators)
      GenerationContext.cs              Read-only run state (IR, stage config, diagnostics)
      GenerationResult.cs               Run outcome: diagnostics, generated-class map, success
      GeneratorDiagnostic.cs            Structured diagnostic
      Ir/
        Ir.Generated.cs                   IR POCOs, generated from ir.schema.json
        IrJson.cs                         Canonical System.Text.Json options for IR (de)serialization
  host/                               Out-of-process host (the `csharp-generate` tool)
    Ace.Generator.Host.csproj           Executable that adapts the library to the compiler tool protocol
    Program.cs                          Entry point: wires HostRunner to the process std streams
    HostRunner.cs                       stdin JSON GenerateToolRequest -> NDJSON file/result frames on stdout
  test/Ace.Generator.Tests/           xUnit tests (incl. polymorphic round-trip, ElasticSan models)
    ArtifactWriter.cs                   Test handler: writes each run's output under artifacts/<test>/
    GeneratedFileCollector.cs           In-memory handler (buffers for assertions)
    HostRunnerTests.cs                  Contract tests for the host's NDJSON wire framing
    TestData/elasticsan.ir.json         Real ElasticSan IR used as a generation test case
    artifacts/                          Generated output from test runs (gitignored; for eyeballing)
  tools/AceIrModelGen/                Codegen tool (JSON Schema -> C# POCOs, via Roslyn)
    Program.cs                          Entry point (arg parsing, file I/O)
    IrModelGenerator.cs                 JSON Schema -> C# mapping + Roslyn emission
    SchemaModel.cs                      Internal model (enum/class/property build records)
```

There is no solution file: the library is the single project at the package root,
with the host, test, and codegen-tool projects in subfolders (built individually).
The library csproj excludes `host/**`, `test/**`, and `tools/**` from its source
glob so those nested projects are not compiled into it.

### Inspecting generated output

Running the tests writes every generated file to disk under
`test/Ace.Generator.Tests/artifacts/<TestName>/`, mirroring the relative paths
the generator produces (e.g. `Models/Widget.cs`, `Processors/…Processor.cs`,
the `.csproj`). Each test clears its own subfolder first, so the tree always
reflects the latest run. It's the easiest way to eyeball what the scaffold (and,
later, the real emission) produces — just run `dotnet test` and open the folder.
The directory is gitignored, and excluded from the test project's build
(`DefaultItemExcludes`) so the emitted `.cs`/`.csproj` never enter the test
compilation.

## Build / test

Uses the .NET SDK pinned by the repo `global.json`. Standalone .NET, no npm —
target each project by path:

```bash
# build the library (run from this package directory)
dotnet build Ace.Generator.csproj

# run the tests (also builds the library via its project reference)
dotnet test test/Ace.Generator.Tests/Ace.Generator.Tests.csproj
```

To build this package *and* the TypeScript packages together, run the toolkit's
`../../build.ps1` (see the toolkit README, "Building the whole toolkit").

## Out-of-process host

`CSharpClientGenerator` is a pure in-process library; the compiler runs it as an
external tool so the backbone stays free of any generator specifics. `host/`
provides that adapter — `Ace.Generator.Host`, a small `dotnet` executable that
implements the compiler's **`csharp-generate`** tool contract:

```
stdin  : one JSON GenerateToolRequest  { ir, config }
stdout : NDJSON — one {"kind":"file","path","contents"} per generated file
         (streamed as produced), then a terminal
         {"kind":"result","success","diagnostics":[{severity,code,message}]}
```

The IR is read with `IrJson.Options` (the polymorphic unions place their
discriminator out of order). `config` is the `csharp` stage's own stage config
(`stages.csharp.options`), as written; the generator takes none yet, and its
`GenerationContext.Config` carries it unread. Placement comes only from the
document's `target`,
which the `seed-ir` stage writes: the request carries no namespace, and a document
without a target is rejected with `ace-generator/missing-target` before anything
is written or removed. `HostRunner` holds all the framing and is unit-tested
in `HostRunnerTests`; `Program.cs` just wires it to the process std streams.

The `ace` CLI spawns the host via `dotnet`, resolving it at a **fixed location
relative to the CLI** — `packages/cli/generator-host/` — which the toolkit build
script populates by *publishing* the host (its dll + dependency dlls + runtime
config) there:

```bash
# from the toolkit root: builds the TS packages and publishes the host into the CLI
./build.ps1
```

Runtime code is deliberately build-config-agnostic (no Debug/Release probing):
choosing the configuration and gathering dependency dlls are the build script's
job. `ACE_GENERATOR_HOST_DLL` overrides the location for bespoke setups. Then:

```bash
ace compile [aceconfig.yaml]                              # the extension's config file, or a folder holding one
ace compile [aceconfig.yaml] --tsp-location <spec> [--entrypoint <path>] --out-dir <dir>   # flags override its run inputs
```

The config file (`./aceconfig.yaml` when no path is given; a run without one
fails) names the spec, its entrypoint and the output folder, plus the document
facts and per-stage settings; the flags override its run inputs. See
[Configuration](../../README.md#configuration-aceconfigyaml) in the toolkit README.
`tsp-location` (or `--tsp-location`) is the directory copied into `<dir>/source` (a
`.tsp` file copies its containing directory). It must be inside an
azure-rest-api-specs checkout, and lands at its path in the repo, under
`<dir>/source/specification/...`. It may instead be a GitHub URL
(`https://github.com/<owner>/<repo>/tree/<ref>/<path>`), fetched with git first;
that repo is then the root. The entrypoint (`entrypoints[].path`, or
`--entrypoint`) selects the `.tsp` file or directory to compile inside that copy —
absolute, or relative to the copied directory — and is `tsp-location` itself when
neither names one.
A sibling the spec imports, such as `../Contoso.Widgets.Shared`, is listed in the
config file's `additional-directories` and lands at its own path in the repo, so
the import resolves. `<dir>/source` must not overlap any copied directory.
`ACE_ALLOW_STANDALONE_SPEC=1` copies a spec from outside a checkout as is. The
root namespace and the other document facts come only from the config file
(`root-namespace: Contoso.Widgets`); without one, the namespace is the spec's own.

> **Temporary:** the `--entrypoint` flag stays until the flag review; the config
> file's `entrypoints` is the lasting way to choose an entrypoint.

## IR POCOs (single source of truth)

The IR is defined once, in Zod, in `@azure-client-engine/ir`. To avoid
hand-duplicating it in C#, the POCOs in `src/Model/Ir/Ir.Generated.cs` are
**code-generated** from the IR JSON Schema artifact, which is itself derived from
the Zod schema. The `AceIrModelGen` tool maps JSON Schema → C# itself (using
**Roslyn** to emit the code — no third-party codegen library). Regenerating is a
two-step flow across the TS/.NET boundary:

```bash
# 1) Emit the IR JSON Schema (owned by the `ir` package; writes packages/ir/dist/ir.schema.json).
#    Run from the toolkit root, or anywhere with `-w`:
npm run schema -w @azure-client-engine/ir

# 2) Generate the C# POCOs (run from this package directory).
#    The default input is ../ir/dist/ir.schema.json; specify the output and namespace:
dotnet run --project tools\AceIrModelGen -- --out src\Model\Ir\Ir.Generated.cs --namespace Ace.Generator.Model.Ir
```

The tool accepts `--schema <path>`, `--out <path>`, and `--namespace <ns>` to
override the defaults, and errors with a hint if the schema artifact is missing.
Re-run both steps whenever the IR schema changes. `src/Model/Ir/Ir.Generated.cs` is
committed so the library builds out-of-the-box; treat it as generated output (do
not hand-edit).

The root type is **`TransformedIR`**, not a neutral "document": the schema's root
is `TransformedIRSchema`, so everything the generator sees already carries
`Commands` and a resolved `Target`. `seed-ir` writes the target and
`create-command` the commands, and the name says both have run: on a bare IR the
target is optional and there are no commands.

### Discriminated unions

The IR's discriminated unions — `IRTypeRef`, `IRProperty`, `IRStatusCode`,
`IRLiteralValue`, `IRServer`, `IRMultipartPartBody`, `IROperationSemantics`, `IRAuthScheme`,
`IROAuth2Flow` — are emitted by `zod-to-json-schema` as `anyOf` + `const`
discriminators. `AceIrModelGen` maps each to an **`abstract` base + derived
types** wired for `System.Text.Json` polymorphism (`[JsonPolymorphic]` /
`[JsonDerivedType]`). Enums map to C# enums with `[JsonStringEnumMemberName]` for
wire fidelity (e.g. `date-time`).

Because the emitted IR does not place the discriminator first in each JSON
object, **deserialize IR via `IrJson.Options` / `IrJson.Deserialize`** (which sets
`AllowOutOfOrderMetadataProperties = true`); deserializing with default options
throws on the out-of-order discriminator.

### Source unions and paging (IR 0.10.0 / 0.11.0)

The POCOs preserve `TransformedIR.DiscriminatedUnions`, their tagged/default/null
variants, and `IRTypeRefDiscriminatedUnion` references. Operation semantics are
now an object with independent `Pageable` and `LongRunning` properties rather
than a `kind`-discriminated union. Paging types retain all input/output roles,
source declaration paths, HTTP bindings, and SDK settings.

This refresh does not add discriminator dispatch or new pagination strategies
to rendered clients. Existing LRO hooks and response-body next-link selectors
consume the new types; unsupported or ambiguous paging bindings are diagnosed.
LRO rendering still takes precedence when both traits are present.

### Flattening metadata (IR 0.12.0, reshaped in 0.13.0)

`ModelHints.Flattened` and `RequestBodyHints.Parameters` carry resolved property
flattening — `FlattenedMember` (name, leaf `Property` id plus the `Via` container
ids that reach it, `Optional`, intersected `Visibility`) and `FlattenedParameter`
(name, `Property` + `Via`, `Required`). Both are optional, so captures predating
0.12.0 still deserialize and emit exactly as before. 0.13.0 replaced each id `Path`
with `Property` + `Via` — the `IRPropertyRef` shape that command pagination's
`Items`/`NextLink` share — so regenerate any 0.12.0 capture that carries them.

Because `ModelHints` now has a member the other `CommonHints` aliases lack, it is
structurally distinct and `IRModel.Rendering` resolves to `ModelHints` instead of
collapsing onto `OperationHints`. `IREnum`, `IROperation`, and `IRParameter`
still share the collapsed `OperationHints` class; those three remain structurally
identical, and the shape is correct either way.

Both fields are consumed — see [Flattening](#flattening).

### Target metadata (IR 0.14.0, trimmed in 0.15.0)

`IRTarget` carries `ServiceName` and `Description`, both nullable, which `seed-ir`
fills from the facts of the same names. The generator does not read them yet;
writing them into the generated project (for example the `.csproj` `Description`)
is a follow-up. Captures without them deserialize and emit exactly as before.

0.15.0 removed `Directories` and `Version`. Models and processors always land in
`Models/` and `Processors/` (`Placement` fixes both, and the namespaces follow
them), and the generated `.csproj` carries no `<Version>`: an extension's version
is decided at release, and finalizing an extension adds one.

### Enum value types (IR 0.16.0)

`IREnumValueType` lists the precise kinds of an enum's values: `String`, the integer
kinds `Int8` through `Uint64`, and `Float32`/`Float64`. They replace 0.15.0's `Number`,
which no longer deserializes, so regenerate any capture that carries a numeric enum.

No enum types are emitted. `IrTypeMapper` renders an enum reference as the primitive
of its value type (a string enum is a `string`, an `Int64` enum a `long`), and value-type
checks such as a flat setter's `value ?? default` follow from that primitive. A reference
to an enum the document does not declare renders as `object`.

So that the spec's values stay visible, `EnumValueDocs` lists them in the XML doc
`<remarks>` of every member an enum types, after its `<summary>`: model properties and
flattened accessors, processor parameters and flat body members. The type may be the
enum itself, or a collection whose items or dictionary values are the enum. A fixed enum
lists its *allowed* values, an extensible one its *known* values, and the enum is named
as the document names it:

```csharp
/// <summary>Optional, used to delete volume if active sessions present. …</summary>
/// <remarks>Known values (extensible enum x-ms-force-delete): "true", "false".</remarks>
public string? XMsForceDelete { get; set; }

/// <summary>The sku name.</summary>
/// <remarks>
/// Known values (extensible enum SkuName):
/// <list type="bullet">
/// <item><description>"Premium_LRS": Premium locally redundant storage</description></item>
/// <item><description>"Premium_ZRS": Premium zone redundant storage</description></item>
/// </list>
/// </remarks>
[JsonPropertyName("name")]
public string Name { get; set; }
```

Values render as C# literals. Up to ten share one line. More than ten, or any member
with its own name or doc, turn the line into a bulleted list. A list shows the first twenty
values and ends with `...and N more.` A member's name follows its value only when the two
differ beyond case and punctuation (`"*" (Asterisk)`, but not `"RSA-HSM" (RSA_HSM)`). Its doc
follows only when it says more than both: specs often restate a value as its doc. An
undeclared enum has no values to list.

### Wire formatting (IR 0.17.0)

`CreateRequestUri()` and `CreateRequestHeaders()` put values on the wire through the
runtime's `WireFormat`, never through C# interpolation, which follows the current
culture (`0,5` on a German machine) and capitalizes booleans (`True`):

- A path value is percent-encoded (`Uri.EscapeDataString`), so a `/` or `#` in a
  name stays inside its segment. An `AllowReserved` parameter — a scope or resource
  id — goes in as written. A value that is not a plain `string` is formatted with
  `WireFormat.Format` first.
- Query entries after the leading literal ones are appended with `.WithQuery(name,
  value)`, which escapes the value and leaves an unset optional off the request instead
  of sending `name=`. Entries keep their IR order.
- Array query parameters with a declared `collectionFormat` use the collection overload:
  `csv`, `tsv`, `ssv`, and `pipes` join invariantly formatted items with the corresponding
  delimiter before escaping; `multi` emits one escaped query entry per item. Null and
  empty collections are omitted. For example, Storage `include` values `metadata` and
  `snapshots` become `include=metadata%2Csnapshots`.
- A header value that is not a string goes through `WireFormat.Format`.
- Literal path text has its braces doubled, so an unbound `{placeholder}` that
  `create-command` kept as text renders as text.

`WireFormat.Format` writes `null` as empty, a string as itself, a boolean in lowercase,
bytes as base64, and anything formattable with the invariant culture (a float in its
shortest round-trip form). Not handled yet: array query parameters without a declared
`collectionFormat`, header array `collectionFormat`, `base64url`, and date/time formats.

### Shared servers (IR 0.8.0)

`TransformedIR.Servers` holds `IRServerDefinition` entries (`Id`, `Endpoint`).
Both `IROperation.Server` and `IRSource.Server` are required `IRId` references;
the endpoint retains the existing literal/parameterized/oneOf `IRServer` union.
Processor generation resolves each operation's own reference by structural
identity (including namespace, interface, and recursive type arguments), never
by its source's default. Missing or ambiguous references report error diagnostics
and skip the affected processor. Literal URLs and parameterized template/default
substitution are unchanged; choosing a `oneOf` host remains unsupported.

A resolved endpoint is **not** baked into each URI. `ProcessorBase` exposes it as
an `Endpoint` member derived from the IR, and every generated
`CreateRequestUri()` interpolates `{Endpoint}`. One override therefore redirects
the whole extension — a sovereign cloud, a test host, a profile-resolved plane —
without regenerating. A processor whose own server resolves elsewhere emits
`protected override string Endpoint => …;`, so a document spanning several planes
still generates correctly.

Nothing is invented. The generator has no built-in host:

| IR server | emitted on the base |
|---|---|
| endpoint pinned (literal, or a template whose parameters all have defaults) | `protected virtual string Endpoint => "https://…";` |
| template with a parameter the spec gives **no** default (`https://{vaultName}.vault.azure.net`) | `protected virtual string Endpoint => $"https://{VaultName}…";` plus `public virtual string VaultName { get; set; } = string.Empty;` |
| no usable server at all | `public virtual string Endpoint { get; set; } = string.Empty;` |

An undefaulted server parameter is something only the caller can supply, so it
becomes a **public** input on the base — which is also what puts it in front of
argument binding and help. Because it lives on the base, a processor whose own
path/query parameter shares that name does not redeclare it: a server parameter
and a path parameter of the same name are the same value, and redeclaring would
hide the inherited member. Public base members (these inputs and `Profile`) are
`virtual` so a derived processor can override one to attach its own attributes,
such as per-dialect visibility.

### Service authentication

`ProcessorBase` also carries the service's OAuth2 values from `IRSource.auth`,
so token acquisition can use the scopes the spec declares (for example
`https://storage.azure.com/.default` for Storage Blob) instead of assuming ARM:

```csharp
protected virtual string? AuthorizationUrl => "https://login.microsoftonline.com/common/oauth2/authorize";
protected virtual string[] AuthScopes => ["https://storage.azure.com/.default"];
```

The values come from the first OAuth2 scheme across the auth options. The URL is
the first authorization URL among its flows, or `null` when none has one (client
credentials carries only a token URL). The scopes are the de-duplicated union
across its flows. Across several sources the most common value wins, as for
`Endpoint`. With no OAuth2 scheme (no auth, API key, Basic/Bearer) neither member
is emitted. Both are carried only; nothing passes them to token acquisition yet.

### Multipart metadata (IR 0.9.0)

`IRRequestBody.Multipart` and `IRResponse.Multipart` preserve model/tuple body
structure, ordered parts, per-part headers/media types, and file metadata.
`IRMultipartPartBody` has `Single` and `File` derived types; the latter identifies
the contents field and whether it contains text. Deserialize these nested
unions through `IrJson.Options`, like the other IR unions.

Multipart tuple bodies have no ordinary `Type` reference; inspect `Multipart`
before interpreting a missing `Type` as an empty body. Part headers are not
outer response headers, and raw file contents are not the JSON representation
of their containing model. Multipart processor serialization/deserialization
is not implemented by this metadata change. The generator reports
`ace-generator/multipart-serialization-not-supported` for these operations;
tuple request bodies use an `object` placeholder in the emitted scaffold.

## Flattening

`ModelGenerator` and `ProcessorGenerator` consume the flattening a transform
resolved (`ModelHints.Flattened` / `RequestBodyHints.Parameters`). The generator
**never derives names or resolves collisions** — the transform already did, and
paths are `IRId`s, so a later rename cannot invalidate them.

Flattening **reshapes the surface rather than doubling it**. The real nested
properties still carry the wire shape, but a container the transform hoisted
(`flatten.into == "self"`) leaves the public surface — otherwise it and every
member hoisted out of it would both be public:

```csharp
[JsonPropertyName("properties")]
[JsonInclude]
internal ElasticSanProperties Properties { get; set; }
```

`[JsonInclude]` is what keeps it serializing: System.Text.Json skips a non-public
member without it (verified for `internal` and `private`, both directions). The
container's *type* stays public — only the member is hidden, matching AutoRest
PowerShell. Hoisting is all-or-nothing per container, so nothing becomes
unreachable.

Each flattened member then adds a `[JsonIgnore]` accessor. Without the attribute
the same value would serialize at two depths.

```csharp
[JsonIgnore]
public long? ScaleUpPropertiesUnusedSizeTiB
{
    get => Properties?.AutoScaleProperties?.ScaleUpProperties?.UnusedSizeTiB;
    set
    {
        Properties ??= new ElasticSanUpdateProperties();
        Properties.AutoScaleProperties ??= new AutoScaleProperties();
        Properties.AutoScaleProperties.ScaleUpProperties ??= new ScaleUpProperties();
        Properties.AutoScaleProperties.ScaleUpProperties.UnusedSizeTiB = value;
    }
}
```

A hop is dereferenced with `?.` only when the preceding hop can be null, so a path
that is required end-to-end reads directly and keeps the leaf's own type. Setters
always null-coalesce intermediates: a hop the spec marks required can still arrive
null from deserialization.

A member whose `FlattenedMember.Visibility` names no create or update phase is
server-owned, so it is emitted **read-only**:

```csharp
[JsonIgnore]
public ProvisioningStates? ProvisioningState { get => Properties.ProvisioningState; }
```

Deserialization still fills it, because it writes the nested property rather than
this accessor — which is why this needs none of AutoRest's `IXInternal`
explicit-interface machinery.

Processor members from `RequestBodyHints.Parameters` are views over `Body`, so
`CreateRequestBody()` still just returns it. `Body` goes `internal` once flat
members cover it: argument binding and help both reflect over *public* instance
properties, so this is what removes the duplicate rather than merely hiding it.

```csharp
internal ElasticSan? Body { get; set; }
public long? BaseSizeTiB { get => Body?.BaseSizeTiB; set => (Body ??= new ElasticSan()).BaseSizeTiB = value ?? default; }
```

If no flat member resolves (an unresolvable reference warns and skips), `Body` stays
public — otherwise the operation would have no way to send a payload at all.

Each delegates to the **model's** member rather than re-walking the containers, so the
model stays the single place that knows how to read and materialize the graph. The
target is resolved *by id* (its `Property` plus its `Via` containers), never by name:
the transform resolves body parameter names against the operation's own
path/query/header parameters, so a parameter can legitimately be named differently
from the member it feeds.

**Narrowing caveat.** Reading through `Body?` or an optional hop always produces a
nullable value, so an accessor over a *non-nullable* leaf is widened. Assigning it
back has to narrow again: a reference type is null-forgiven (`value!`), and a value
type falls back to `value ?? default` — so setting null writes `0`/`default` rather
than throwing. Required-parameter validation (`[ArgumentInfo]`) is still deferred,
so nothing yet rejects the unset case.

A reference that does not resolve reports `ace-generator/unresolved-flattened-path` (or
`…/unresolved-flattened-parameter`) and skips only that member; the nested
properties and the rest of the type are emitted normally.

## Generation API

Asynchronous, streaming:

```csharp
Task<GenerationResult> GenerateAsync(
    TransformedIR ir, JsonElement? config,
    GeneratedFileHandler writeFile, CancellationToken ct = default);
```

The generator **streams** each generated file to the caller-supplied handler the
moment it is produced — awaiting it before producing the next (natural
backpressure) and never buffering file contents. This mirrors the TypeScript
backbone's `OutputDelegate`: the generator decides *when* each artifact is
emitted; the handler decides *where/how* it lands (an orchestrator writing to
disk, or a test collecting in memory). `GenerationResult` therefore carries only
the small aggregate outcome — `Diagnostics`, `Success`, and `GeneratedClasses`
(a `ClassName → relative file path` map; non-type artifacts like the project file
are not listed) — not the file contents.

**Async only, by design.** The generator is hosted as a spawned `dotnet`
subprocess (JSON in → files out) whose entry point is naturally `async Task Main`
— there is no sync-only seam (no Roslyn source generator, no sync interface, no
UI/`SynchronizationContext`). Async-all-the-way is idiomatic here, and any rare
sync caller can block on the returned `Task`; the reverse (faking async over sync)
is not worthwhile.

**Output paths are relative.** The handler receives a `relativePath` (using `/`
separators, never absolute, never escaping via `..`); the generator is
**filesystem-agnostic** and never knows the base directory — the handler owns it
(e.g. a filesystem writer that joins `baseDir + relativePath`). So the whole
output is one relocatable folder tree. The orchestrator validates every path
before streaming and reports `ace-generator/invalid-output-path` on a violation.

Callers that deliberately want to buffer (tests, tiny specs) can pass
`GeneratedFileCollector.WriteAsync` and read `.Files` afterwards. Implementations
are **pure**: no disk writes, no console output, no process side effects — the
handler owns all I/O.

`CSharpClientGenerator` is the orchestrator: it runs a fixed set of
**artifact generators** (`IArtifactGenerator`), each yielding files and reporting
diagnostics through a shared `GenerationContext`. The stages are *pull-based*
(each `Produce`s a lazy `IEnumerable<GeneratedFile>` via `yield`), so they stay
agnostic to the single async driver and output streams one file at a time:

| Stage | Type | Responsibility |
|-------|------|----------------|
| model | `ModelGenerator` | IR models → C# model types |
| processor | `ProcessorGenerator` | IR operations → one processor class each (IProcessor pattern) |
| base | `ProcessorBaseGenerator` | template-like processor base that wires runtime dependencies |
| stub | `ProjectStubGenerator` | `.csproj`, assembly metadata, resources |

The stages are at different maturity levels. **`ModelGenerator`,
`ProcessorGenerator`, and `ProcessorBaseGenerator` emit real code**;
`ProjectStubGenerator` is still a scaffold.

**`ModelGenerator`** emits one class per model with every property mapped
literally (logical `name` → C# member, `wireName` → `[JsonPropertyName]`,
`optional`/`nullable` → nullable type, `docs`/`summary` → XML doc). An enum-typed
property takes the enum's value type, and its doc lists the enum's values.

**`ProcessorGenerator`** emits one `{Command.Name}Processor : ProcessorBase<TRequest, TResponse>`
per `IRCommand`. The name is used **verbatim** — `create-command` already composed
it (`action` + `variant` + `subject`), resolved collisions, and applied C#
identifier rules, so the generator never re-derives a name from an operation. The
public surface mirrors the command's parameter surface exactly (one
property per path/query parameter — `subscriptionId` inline, not special-cased),
plus a single request-body member. It overrides the base's fine-grained request
seams — `Method`, `CreateRequestUri()` (`{Endpoint}` + path template with
`{wireName}` rewritten to the C# member, escaped per
[Wire formatting](#wire-formatting-ir-0170), + `api-version`/query string), and (body
ops) `CreateRequestBody() => Body` — rather than assembling the request itself.
`TRequest` is the body model (or `object` for no-body ops); `TResponse` the
success response (or `object`). Long-running operations also emit the four
final-state/polling hook overrides — mapped **strictly** from IR `finalStateVia`
(`location` → the `Location` header; otherwise the base re-GETs the original URI) —
and `SendAsync() => SendAsyncLRO()`. Pageable operations emit the pagination wiring
(`pageItems`/`nextLink` selectors from the IR's `items`/`nextLink` property references + a
`BeforeWriteOutputAsyncAndReturn` override handing them to the runtime
`WriteOutputWithPaginationAsync` helper). Spec-vs-reality deviations are handled by
customization on top of the generated (`partial`) processor, never by the generator.

**`ProcessorBaseGenerator`** emits the runtime base processors derive from — a
non-generic marker `ProcessorBase : IProcessor` plus
`ProcessorBase<TRequest, TResponse>` (fine-grained request seams — `Endpoint`,
`Method`, `CreateRequestUri`, `CreateRequestBody`, `CreateRequestHeaders` —
assembled by
`CreateRequestMessage`, and the standard `ProcessAsync` pipeline with
`BeforeProcessAsync` / `BeforeWriteOutputAsyncAndReturn` hooks). It also owns
`DefaultJsonOptions`, which every request body and response is (de)serialized
with until `ProcessorContext` carries its own: camelCase, case-insensitive
reads, and `DefaultIgnoreCondition = WhenWritingNull`. That last one is a
*correctness* setting, not a size one — without it an unset property goes on the
wire as an explicit `null`, which a PATCH reads as "clear this field". `AfterInit()`
is the seam to replace it. The
long-running-operation machinery lives in the runtime library; the base only
delegates (`SendAsync`/`SendAsyncLRO` → `HttpRuntime`). It's "template-like" (only
the package namespace varies), but built in the **house style**: signatures —
class/base list, generic params/constraints, properties, method signatures — are
composed structurally with Roslyn `SyntaxFactory` as a safeguard; only method
*implementations* are authored as raw code blocks via `WithRawBody`.

The IR-driven stages are deliberately faithful ("garbage in, garbage out") and
null-tolerant. Deferred by design: model inheritance dedup / polymorphism; and on
the processor side, `[ArgumentInfo]` + defaults. See each
stage's `TODO`. **`ProjectStubGenerator`**
emits the extension `.csproj` (identity, `Shared.props` import, compile-only
`ProjectReference`s) composed structurally with `System.Xml.Linq` — one `…Emit`
method per artifact, so assembly metadata / resources slot in later. This split
lets the model, operation, runtime-wiring, and project concerns evolve
independently.

The `ElasticSanModelTests` and `ElasticSanProcessorTests` emit the real ElasticSan
IR (`TestData/elasticsan.ir.json`, 59 models / 30 operations) and verify the
output parses; run `dotnet test` and open `artifacts/ElasticSan_Models/` and
`artifacts/ElasticSan_Processors/` to see it.

The fixture's IR 0.7 operation/parameter identities follow the
[upstream TypeSpec declarations](https://github.com/Azure/azure-rest-api-specs/tree/main/specification/elasticsan/resource-manager/Microsoft.ElasticSan/ElasticSan),
not the SDK relocations in `back-compatible.tsp`; the captured HTTP/model payloads
are unchanged. For example, `preBackup` belongs to `VolumeGroups`, snapshots to
`Snapshots`, and `restoreVolume` is namespace-level despite its old root-client
grouping.
