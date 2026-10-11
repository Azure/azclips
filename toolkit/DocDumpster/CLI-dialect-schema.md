# The CLI dialect schema

`IRCommand.dialect.cli` and `IRCommandParameter.dialect.cli`: how a command is spoken by the Azure CLI. This is the CLI counterpart of the PowerShell dialect in #802, and it stacks on that PR. The shared command model is described in [TransformedIR-schema.md](TransformedIR-schema.md). The schema is in `toolkit/packages/ir/src/dialect.ts`, in the CLI section, and the rules that span commands are in `toolkit/packages/ir/src/cli.ts`.

This is a first version for review, not the final spec. Where in doubt it carries more fields rather than fewer, because merging fields later is easier than adding them.

## Where it sits

```
TypeSpec ──emitter──▶ IR operation ──create-command──▶ IRCommand ──CLI transform──▶ dialect.cli ──generator──▶ processor + ExtensionIndex ──▶ az runtime
                                                       (client-neutral)            (this schema)
```

- `create-command` writes no dialect. A CLI transform, which references Azure CLI codegen (AAZ), writes `dialect.cli`. Customizations may edit it.
- The generator turns it into the processor attributes and the `ExtensionIndex` entries the CLI reads at runtime.
- The dialect holds presentation only. What a parameter binds to (`source`), its type and its shared docs stay on the shared parameter.

## Principles

- **One command, one processor, one parameter set.** Every `IRCommand` is one atomic processor. Its parameter set is named by its `id.variant`.
- **Same-name siblings form one CLI command.** Commands from the same action (`commandActionKey`) that take the same `name` become one public command with one set per variant. For example, `ListBySubscription` and `ListByResourceGroup` both become `elastic-san list`.
- **Not offered vs hidden.** No `dialect.cli` means the CLI has nothing to do with the command. `hidden: true` means it is generated and usable internally, for example by a customization, but users cannot invoke it.
- **Decisions are written down.** The transformer writes every decision into the dialect: names, groups, value sets and API references. A captured IR then shows what the CLI will do, and an author can override any of it.
- **Aligned with PowerShell where the meaning matches.** Field names follow `PsCommandDialect`/`PsParameterDialect` where possible, so the two can later share a parent type.

## Command dialect

| Field | Type | Meaning | Runtime consumer |
|---|---|---|---|
| `name` | `string`, required | Command name without `az`: lowercase kebab tokens separated by single spaces, e.g. `elastic-san list` | `ExtensionIndex` key, `ProcessorInfo.Name` |
| `aliases` | `string[]` | Alternate complete names. Kept for parity; expected to be empty | `CommandMetadata.Aliases` |
| `hidden` | `boolean` | Generated but not public | Not offered to users |
| `description` | `string` | Overrides the shared docs | Help |
| `defaultParameterSet` | `string` | Set chosen when selection cannot decide; one sibling's `id.variant` | Set selection |
| `examples` | `CliExample[]` | Command-line examples. Code generation leaves them empty; a build-time step fills them | `ProcessorInfo.Examples` (`"title \| command"`) |
| `preview` | `CliPreview` | Preview command | `HelpTag.Preview`, `PreviewMessage` |
| `deprecated` | `CliDeprecated` | Deprecated command | `HelpTag.Deprecated`, `DeprecatedMessage` |
| `api` | `ApiReference[]` | The source API(s): API → IR operation → IR command → dialect. Several for get+put commands or orchestrators | Provenance only, for docs and tooling |

## Parameter dialect

| Field | Type | Meaning | Runtime consumer |
|---|---|---|---|
| `name` | `string` | Long option, bare: `resource-group` for `--resource-group`. Required unless `hidden` | `ParameterMetadata` name |
| `aliases` | `string[]` | Extra spellings, bare; one character is a short option (`g` for `-g`) | `ParameterMetadata.Aliases` |
| `hidden` | `boolean` | Not in the CLI syntax; the shared binding stays | Not exposed |
| `description` | `string` | Overrides the shared docs | Help |
| `parameterSets` | `CliParameterSetMembership[]`, at most one | Membership in the command's own set; `mandatory` replaces shared `required` | `ArgumentInfo.ParameterSetName`, `ParameterMetadata.IsRequired` |
| `group` | `string` | Help section only, stored bare (`Properties`) | `[ParameterGroup("Properties Arguments")]` |
| `validateSet` | `string[]` | Closed enum: only these values are accepted | `ParameterMetadata.AllowedValues` (enforced, completed, listed in help) |
| `completer` | `IRLiteralValue[]` | Open enum: suggestions only, any value accepted | None yet |
| `preview` | `CliPreview` | Preview parameter | None yet |
| `deprecated` | `CliDeprecated` | Deprecated parameter | None yet |
| `deprecatedAliases` | `CliDeprecatedAlias[]` | Old spellings that still parse but point users to `name`, e.g. `--target-id` → `--target` | None yet |

## Supporting types

```ts
type CliParameterSetMembership = {
  set?: string;        // must equal the command's id.variant; absent = the command's own set
  mandatory: boolean;  // overrides IRCommandParameter.required
};
type CliExample = { title?: string; command: string };              // title cannot contain "|"
type CliPreview = { message?: string; estimatedGaDate?: string };   // ISO date
type CliDeprecated = {
  message?: string;          // optional: the runtime can print a standard notice
  replacement?: string;      // replacement command name or long option, bare
  removalDate?: string;      // ISO date
  removalVersion?: string;
};
type CliDeprecatedAlias = {
  name: string;              // the old spelling, bare; the replacement is the parameter's name
  hidden?: boolean;          // keep it out of help and completion; it still parses
  message?: string;
  removalDate?: string;
  removalVersion?: string;
};
type ApiReference = { method: HttpMethod; path: string; apiVersion: string };
```

## Parameter sets

A parameter set is a variant. The ElasticSan list family shows how it works:

```
ListBySubscriptionElasticSans   (variant BySubscription)   accepts {subscription-id}
ListByResourceGroupElasticSans  (variant ByResourceGroup)  accepts {subscription-id, resource-group}, requires resource-group
                     └──────── same action, same name ────────┘
                                  az elastic-san list [-g]
```

**Selection, "Option A":**

1. Keep the sets that accept every argument given.
2. Drop the sets whose mandatory arguments are missing.
3. If several remain, `defaultParameterSet` decides when it is among them.

Configured or profile-supplied values, such as the subscription, never take part in selection.

**Checked at validation time:**
- A set that can never be selected is an error, unless it is the default.
- An invocation that fits two sets with no default to resolve it is an error.

**Customizations and orchestrators.** Hand-written customizations may add sets through C# attributes. These are merged at build time and are not part of this schema.

## Transformer defaults

The CLI transformer is not part of this change. These are the expected rules, following AAZ:

| Field | Default |
|---|---|
| Command `name` | Module name plus kebab-cased `id.subject` and `id.action`, e.g. `elastic-san list` |
| `SubscriptionId` | `name: "subscription-id"`, `mandatory: false`; the runtime fills it from the active profile |
| Parameter `name` | Kebab-cased parameter name |
| `group` | Body parameters: the last object in `source.via`, capitalized (`properties` → `Properties`, `sku` → `Sku`). Path, query and header parameters get no group |
| `validateSet` / `completer` | From the parameter's shared enum: `isFixed` → `validateSet`, otherwise `completer` |
| `api` | One entry per `steps[].operation`: method, path template, API version |
| `examples` | Empty |

## Validation

Single dialect (`CliCommandDialectSchema`, `CliParameterDialectSchema`):

- names and aliases are bare and lowercase kebab-case; a long option has at least two characters; a one-character alias is a short option;
- a parameter's spellings, including deprecated ones, are unique case-insensitively;
- an exposed parameter needs a `name`; a hidden parameter cannot be mandatory or have deprecated aliases;
- `validateSet` and `completer` are mutually exclusive; `validateSet` values are unique case-insensitively;
- `group` is bare: it cannot end in `Arguments`, and `Global` is reserved for global arguments;
- example titles cannot contain `|`; dates are ISO 8601; `parameterSets` has at most one entry.

Across commands (`checkCliCommands`, run from `TransformedIRSchema`):

| Area | Rule |
|---|---|
| Accounting | Every non-internal parameter of an offered command has a CLI dialect: a `name`, or `hidden`. Internal parameters are never presented |
| Sets | A membership's `set` equals the command's variant |
| Public command | A name belongs to one action. Same-name siblings agree on command-level fields, and each has a variant |
| One spelling, one argument | Within a command, spellings are unique. Across sets, a spelling means the same flag with the same shared type and presentation; only `mandatory` may differ |
| Values | `validateSet` values are members of the parameter's shared enum |
| Selection | Unreachable sets and unresolved overlaps are errors (see Parameter sets) |

## Alignment with PowerShell (#802)

| PowerShell field | CLI |
|---|---|
| `verb`, `subject` | Replaced by `name` |
| `defaultParameterSet` | Same |
| `examples: string[]` | Structured `CliExample[]`: help splits `"title \| command"` at the first pipe, so a shell pipe in a raw string would be misread |
| `preview: { message, estimatedGaDate? }` | Same fields, `message` optional |
| `deprecated: { message }` | Superset: `message?`, `replacement?`, `removalDate?`, `removalVersion?`. These match Azure CLI's `redirect`/`expiration` and AutoRest's `replacement-cmdlet`, `deprecated-by-version` and `change-effective-date` |
| `httpPath: { path, apiVersion }` | `api: ApiReference[]`. Same concept (AutoRest's `[HttpPath]`); proposed as one shared field |
| `supportsShouldProcess`, `confirmImpact` | Not in the dialect: confirmation is runtime behaviour |
| `helpUri`, `outputTypes` | Not needed for the CLI |
| `parameterSets[].mandatory` | Same |
| `parameterSets[].position`, `valueFromPipeline*` | Not applicable: generated commands take no positionals, and there is no object pipeline |
| `validateSet`, `completer` | Same, same projection from the shared enum |
| `removed` (dropped from #802 on 12 October) | Not used: `hidden` covers it |
| `defaultValue` | Not included: configured defaults are a runtime supplier |
| — | CLI only: `group`, `deprecatedAliases` |

## Open questions

- **Shared `api`:** can PowerShell use `api` instead of `httpPath`, and is there a case where they differ?
- **Shared deprecation shape:** can `PSDeprecated` take the same four optional fields?
- **Structured examples:** can PowerShell use `{ title?, command }` too?
- **Confirmation:** does a shared "confirmation required" fact (Azure/CLIPS#928) feed both `--yes` and `supportsShouldProcess`?
- **Shared validators:** which TypeSpec constraints (`@minValue`, `@pattern`, `@secret`, …) should the shared IR carry before dialects present them?
- **Group-level help:** CLI command groups carry help text and lifecycle (for example a preview group). Should those live on the target, as PowerShell's module dialect does?
- **Runtime support** is still needed for:
  - several processors under one CLI name, with set selection;
  - completion-only values;
  - parameter-level preview, deprecation and deprecated aliases.

## Evidence

- **Corpus:** a basic CLI transform (the defaults above) was run over 31 real TypeSpec projects compiled with this branch. See the PR description for the numbers.
- **Legacy Azure CLI 2.89.0, for comparison:**
  - 2,931 `arg_group` uses in bundled AAZ code, 1,713 of them `Properties`;
  - 50 deprecated option spellings across 11 handwritten modules;
  - 251 preview, 66 deprecated and 5 experimental commands out of 6,052.
