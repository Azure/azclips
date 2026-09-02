# Azure Client Engine compiler

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
| `npm run schema`    | Emit the IR JSON Schema artifact from the Zod IR schema.      |
| `npm run schema:check` | Fail if the emitted IR JSON Schema is stale vs. the Zod schema (CI). |

## Building the whole toolkit

`npm run build` (`tsc -b`) builds only the **TypeScript** packages (`ir`,
`typespec-emitter`, `compiler`, `cli`). The **C# generator** under
`packages/generator` is a standalone .NET package and is *not* part of that build.

For the full cross-toolchain build — TypeScript **and** the .NET generator +
its out-of-process host — run the toolkit build script from the toolkit root:

```powershell
./build.ps1                       # Debug
./build.ps1 -Configuration Release
```

This is the single entry point CI/pipelines should call; it wraps `npm run build`
and `dotnet build` so callers need not know the per-package commands. `npm run
build` stays the fast inner loop for TypeScript-only work.

## IR JSON Schema (cross-language contract)

The IR is defined once, in TypeScript, as [Zod](https://zod.dev) schemas under
`packages/ir/src` — that is the single source of truth. Non-TypeScript consumers
(notably the C# generator) must **not** hand-duplicate those types. Instead, a
JSON Schema is *derived* from the Zod schema; downstream languages code-generate
their own types from it (e.g. C# via NJsonSchema / a Roslyn source generator).

`ir.schema.json` is a **build artifact, not source** — it is not committed.
Generate it on demand into wherever a consumer needs it via `--output-dir`
(default: the package's gitignored `dist/`):

```bash
# Emit to a chosen directory (note the `--` separating npm's args from the script's):
npm run schema -w @azure-client-engine/ir -- --output-dir <dir>

# Verify a previously-emitted artifact is in sync with the Zod schema (CI):
npm run schema:check -w @azure-client-engine/ir -- --output-dir <dir>

# With no --output-dir, both default to packages/ir/dist/ir.schema.json:
npm run schema
```

The `--` is required so npm forwards `--output-dir` to the script rather than
consuming it as its own option. (In PowerShell, invoke through `cmd /c "…"` or
call `node packages/ir/scripts/emit-json-schema.mjs --output-dir <dir>` directly,
since PowerShell strips a bare `--`.)

**Workflow:** a build/codegen step regenerates the artifact into its output
location as needed; `schema:check` guards against drift between the emitted
artifact and the Zod source.

**Fidelity caveat:** JSON Schema is a *structural* contract only. Cross-field
refinements expressed with Zod `.superRefine` (e.g. a pageable operation must use
either next-link *or* continuation-token fields, and a `custom` LRO polling
strategy requires `customPollingUrlPath`) cannot be represented and are **not**
carried across. Authoritative validation stays in the Zod emitter; generators may
trust that any IR they receive has already been validated, and should gate on the
`schemaVersion` field (see `IR_SCHEMA_VERSION`).

**Fidelity caveat:** JSON Schema is a *structural* contract only. Cross-field
refinements expressed with Zod `.superRefine` (e.g. a pageable operation must use
either next-link *or* continuation-token fields, and a `custom` LRO polling
strategy requires `customPollingUrlPath`) cannot be represented and are **not**
carried across. Authoritative validation stays in the Zod emitter; generators may
trust that any IR they receive has already been validated, and should gate on the
`schemaVersion` field (see `IR_SCHEMA_VERSION`).
