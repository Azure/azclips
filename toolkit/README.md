# Azure Client Engine compiler

## Pipeline status

TypeSpec -> `IR` (`@azure-client-engine/typespec-emitter`) is complete. `IR -> IR` transforms and
`IR -> files` generation (`@azure-client-engine/compiler`) are a working backbone, with one real
transform/generator pair implemented so far:

| Package | Role | Status |
|---|---|---|
| `ir` | The `IR` schema (models, operations, hints). | Complete. |
| `typespec-emitter` | TypeSpec -> `IR`, out-of-process via the CLI. | Complete. |
| `compiler` | `TransformStep`/`Generator` backbone (`compile()`, registry, pipeline). | Backbone complete. `create-ps-commands` (enriches `IROperation`/`IRParameter.rendering.powershell` -- verb/noun/`ConfirmImpact`/mutability/etc., plus `select`/`where`/`set` directive overrides) is the first real, non-identity `TransformStep`. `generate-ps-commands` is a placeholder `Generator`: it calls `derivePsCommands(ir)` and writes the result as `ps-commands.json` for inspection -- **not** real C# output. `noopGenerator`/the identity pipeline remain the defaults when neither is configured. |
| `ps-commands` | `PsCommand`/`PsParameterSet` aggregate + `derivePsCommands(ir): PsCommand[]` grouping (multi-operation merge, base/Expanded variant fan-out). | Complete for the scope described below. |
| `cli` | Thin `ace compile` front end wiring the above into a real, out-of-process TypeSpec compile. | `create-ps-commands`/`generate-ps-commands` are opt-in via `--config`'s `pipeline`/`generator` keys. |

**Still missing**: an actual Roslyn/C# generator that turns `PsCommand[]` into real `Cmdlet`/
`Processor`/`ExtensionIndex` source -- that's a separate, later effort. See
`openspec/changes/ps-cmdlet-schema/` for the full design/tasks behind the PowerShell-targeting
work above, including open questions (`ExtensionIndex` isn't a `partial class` today, and
`hints.powershell` isn't yet a shared abstraction with `Core.Abstractions`'s
`ArgumentInfoAttribute`/`ProcessorInfoAttribute`).

## Dependency Management

### Emitter

The `@azure-client-engine/typespec-emitter` package uses the **peer + dev duplication** pattern for TypeSpec compiler and related libraries:

- **`peerDependencies`** declare the broad version ranges that consumers must provide (e.g. `"@typespec/compiler": ">=1.7.0 <2.0.0"`). These describe compatibility, not what gets installed when developing this package.
- **`devDependencies`** mirror every peer entry with a specific version that satisfies the peer range (e.g. `"@typespec/compiler": "^1.12.0"`). This is what actually gets installed locally so that `tsc`, tests, and IDEs can resolve the modules during development.

Both lists must stay in sync. To guard against drift, the workspace uses [`syncpack`](https://jamiemason.github.io/syncpack/) at the root, configured in `.syncpackrc.json` with two rules:

1. Every dependency that appears in `peerDependencies` must also appear in `devDependencies`, and the dev version must satisfy the peer range.
2. `peerDependencies` versions must remain ranges (no pinned or caret/tilde-only versions).

**Workflow for adding or upgrading a TypeSpec / Azure peer dependency:**

```bash
# from repo root
npm install -w @azure-client-engine/typespec-emitter --save-dev <pkg>@<version>
# then add or update the matching entry in "peerDependencies" by hand
npm run lint:deps   # verify both lists agree; non-zero exit fails CI
```

**Useful scripts (run from repo root):**

| Script              | Purpose                                                        |
|---------------------|----------------------------------------------------------------|
| `npm run lint:deps` | Fail if a peer dep is missing from devDeps or versions drift. |
| `npm run fix:deps`  | Auto-rewrite mismatched versions where safe.                  |
| `npm run build`     | Build all workspace packages via TypeScript project references.|
