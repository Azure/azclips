# AutoRest PowerShell transformers

## `plugin-tweak-m4-model.ts`

Analysis of `C:\workspace\autorest.powershell\powershell\plugins\plugin-tweak-m4-model.ts`.
Line references in this section refer to that source file unless stated
otherwise. This is a Modeler Four compatibility/normalization pass over the PowerShell
code model. Several transformations compensate for differences from the
older Modeler Three/remodeler representation.

It runs six transformations in a fixed sequence: parameter ordering,
response-header schema creation, dictionary API-version handling, default
description cleanup, exception filtering, and no-inline annotations.
It mutates and returns the existing model rather than generating cmdlets or
C#.

- **Reorder operation parameters using the request path** (lines 52-59)

  Uses the first request's HTTP path and delegates to `sortPathParameters()`.
  The helper mutates `operation.parameters` in place, attempting to place
  parameters in URL-placeholder order. It does not reorder each request's
  separate parameter collection.

  In `powershell\utils\sort-parameters.ts:3-21`, matching uses
  `language.default.serializedName` wrapped in braces and compares it with whole
  slash-delimited path segments. The helper does not explicitly filter by HTTP
  parameter location. Its loop is bounded by parameter count rather than path
  segment count, and its cached positions are not fully updated after swaps;
  it should not be treated as a general, robust path-ordering algorithm.

- **Create separate header schemas for responses without body schemas**
  (lines 72-130) --Should Skip

  For a response with no existing `schema`, but with a defined headers
  collection and a first status code, creates or finds an object schema named
  from the operation group, operation, status-code label, and `ResponseHeaders`.
  Known status codes use the `StatusCodes` lookup; otherwise the raw code is
  used.

  The schema is marked `isHeaderModel`. Each header becomes an optional,
  writable property whose name and serialized name use the header name, with
  an empty description and `HeaderProperty = 'Header'`.

  The model is attached through `response.language.default.headerSchema`;
  it is not assigned as the response body schema. This complements
  `plugin-tweak-model.ts`, which adds header properties to existing object
  response schemas.

  Lookup can reuse an existing schema, but the implementation still pushes it
  into `model.schemas.objects` and appends header properties without
  deduplication. Repeated processing can therefore accumulate duplicate schema
  references or properties.

- **Fill missing dictionary API versions and mark redundant dictionaries**
  (lines 132-168) --Should Skip

  For a dictionary without `apiVersions`, first copies versions from its
  element type when available. It then visits versioned object schemas and
  copies their versions to dictionary-valued properties that still lack
  versions, including properties returned by `getAllProperties()`.

  Version metadata is deep-copied through JSON serialization rather than
  shared by reference. Existing values, including an existing empty array, are
  not replaced.

  For dictionary schemas appearing in an object's `parents.all`, the code
  marks an unversioned dictionary parent `skip = true` to avoid redundant
  dictionary model generation. That branch does not assign the parent's API
  versions.

- **Remove a narrowly matched set of Modeler Four default descriptions**
  (lines 170-209) --Should Skip

  Recursively visits object, dictionary, and array schemas. It follows
  collection element types and object properties, using a visited set to avoid
  repeated traversal and cycles.

  Dictionary and array schema descriptions matching the configured default
  patterns are cleared. Property descriptions are considered only when the
  property's schema is a dictionary, even though the comment also mentions
  array properties.

  The actual regular expressions are `/Dictionary of <.?>$/` and
  `/Array of .?$/`. Their `.?` fragments accept at most one character, not an
  arbitrary type name, so the implementation does not remove every description
  resembling `Dictionary of <string>` or `Array of string`.

- **Keep only default exception responses** (lines 43-50) --Should Skip

  Filters `operation.exceptions`, retaining only entries whose first HTTP
  status-code entry is exactly `default`. Explicit status-code exceptions are
  removed rather than merged into the default exception.

  This transformation does not filter the normal `operation.responses`
  collection or change the contents of retained exception schemas.

- **Apply configured no-inline annotations** (lines 61-71) --Should Skip

  Collects values from directives with a truthy `no-inline` field. Object
  schemas whose `language.default.name` exactly matches a configured name are
  marked `language.default['skip-inline'] = true`.

  This records a decision for downstream generation; it does not perform
  inlining, undo an existing inline transformation, or apply the marker to
  every schema kind.


## `plugin-tweak-model.ts`

Analysis of `C:\workspace\autorest.powershell\powershell\plugins\plugin-tweak-model.ts`.
Line references below refer to that source file. This is a preprocessing pass that mutates AutoRest's PowerShell code model
before generation. It combines structural changes with generator-specific
metadata; it does not generate cmdlets or C# itself.

The `tweakModelPlugin()` entry point initializes `ModelState<PwshModel>`, runs
`tweakModelV2()`, serializes the returned model, and writes
`code-model-v4-tweakcodemodel-v2.yaml` with artifact type `code-model-v4`.

- **Create a universal resource identity model** (lines 88-135) --should skip

   Adds an object schema named `<ServiceName>Identity`, identified by the
   `universal-parameter-type` UID.

   The schema collects distinct path parameters from `operation.parameters`
   across operation groups. Parameter names are deduplicated case-insensitively,
   and the resulting properties are optional and writable. When `azure` is
   enabled, an additional optional, writable `id` property represents the
   resource identity path.

- **Mark object schemas returned by operations** (lines 98-107) --should skip

   Object schemas used by operation responses receive
   `extensions['is-return-object'] = true`. This is an annotation for downstream
   generation rather than a change to the response's HTTP shape.

- **Remove duplicate inherited properties** (lines 36-69, 153-161) --should skip

   Recursively visits parent schemas before the child and compares properties
   by serialized name. A child property is removed when its schema kind matches
   the inherited property's kind. For object-valued properties, the schema
   objects must also be the exact same instance.

   Conflicting object-schema duplicates produce an error, and the enclosing
   pass throws if conflict detection fails. This is not a general structural
   equivalence check: non-object schemas are compared by schema kind.

- **Merge response headers into object response models** (lines 164-233) --should skip

   Unless `use-storage-pipeline` is enabled, the pass examines response headers
   and adds properties to the corresponding object response schemas.

   Models are marked `hasHeaders`. A new header property receives optional
   metadata and the `Header` marker. If a matching property already exists
   without a header marker, it becomes `HeaderAndBody`, allowing values such as
   an ETag to come from either location.

   Responses without schemas and non-object response schemas are not enriched
   this way. The storage-pipeline branch is currently a placeholder.

- **Annotate multipart binary properties as named streams** (lines 238-253) --Example: specification\containerregistry\data-plane\Registry\routes.tsp

   Examines the first request of each operation and marks binary properties
   found through the selected multipart body parameters with
   `language.default.isNamedStream = true`.

   The active parameter filter selects non-object schemas with body location
   and multipart style before inspecting their properties; this is not a
   general traversal of every multipart request. Despite the surrounding
   comment discussing filename parameters, this code only records the named
   stream annotation and does not create a filename parameter itself.

- **Annotate polymorphism** (lines 263-281)

   Object schemas with a discriminator are marked `isPolymorphic`, and their
   available descendants are recorded in `polymorphicChildren`.

   For schemas with a discriminator value, the pass copies the
   `x-ms-discriminator-value` extension into
   `language.default.discriminatorValue`. These annotations prepare information
   for downstream generation rather than implementing polymorphic
   serialization here.

- **Identify constants and skippable single-value enums** (lines 283-352) --don't do it by default, provide option "omit single value enum" conifg

   Processes operation parameters and object properties. Required members
   receive `language.default.constantValue` when they use a constant schema or
   a choice/sealed-choice schema containing exactly one value.

   A single-value sealed-choice schema can be marked `skip = true` when used by
   a required member. An optional use sets `skip = false`; required uses respect
   that value, so an optional use prevents the shared enum schema from being
   skipped regardless of traversal order. The schema is marked for downstream
   handling, not removed from the model here.

## `plugin-tweak-model-azure-v2.ts`

Analysis of `C:\workspace\autorest.powershell\powershell\plugins\plugin-tweak-model-azure-v2.ts`.
Line references in this section refer to that source file. Adds Azure-oriented collection, long-running-operation, API-version, and
resource-reference behavior to the existing PowerShell code model.
`tweakModel()` mutates the model in that order and returns it.

`tweakModelAzurePluginV2()` initializes `ModelState<PwshModel>`, runs the pass,
and writes `code-model-v4-tweakcodemodelazure-v2.yaml` with artifact type
`code-model-v4`. It does not implement paging, polling, jobs, or resource
dereferencing itself; it prepares metadata and response shapes for downstream
generation. Replacing resource models with common Azure resource types is
mentioned as a TODO, not implemented.

- **Translate explicit pageable extensions into operation metadata**
  (lines 27-41)

  Operations with a truthy `x-ms-pageable` extension receive
  `language.default.pageable` with `responseType = 'pageable'`.

  The pass takes `nextLinkName` from the extension, converting falsy values to
  `undefined`. It defaults `itemName` to `value` and `operationName` to the
  operation's default-language name followed by `Next`.

  A null or absent next-link name still produces collection metadata, allowing
  downstream code to unwrap the items without following another page.
  The next-operation name is recorded, not resolved or generated here.
  Explicit pageable metadata takes precedence and bypasses response-shape
  inference for that operation.

- **Infer collection unwrapping from response shapes** (lines 43-103)

  Without `x-ms-pageable`, examines response body schemas. A direct array
  response produces `pageable.responseType = 'array'`.

  An object with exactly one own property, where that property is an array,
  produces `responseType = 'nested-array'` and uses the property's serialized
  name as `itemName`. An object with exactly two own properties can also match
  when one is named `nextLink` and the other is an array; the resulting metadata
  additionally records `nextLinkName = 'nextLink'`.

  Both object heuristics require absent/falsy `parents` metadata. They do not
  inspect inherited properties, unwrap arbitrary nested structures, or check
  the type of the `nextLink` property. Multiple matching responses can overwrite
  the operation's metadata; there is no cross-response reconciliation.

- **Annotate long-running operations and job support** (lines 106-114)

  Operations with a truthy `x-ms-long-running-operation` extension receive
  `language.default.asjob = true`.

  Their `language.default.lro` is the supplied
  `x-ms-long-running-operation-options` object, or a fallback object containing
  `final-state-via = 'default'`. The code selects one object rather than merging
  defaults into existing options. These fields describe generator/runtime
  behavior; this pass does not start a job or execute a polling loop.

- **Normalize the exposed response list for long-running operations**
  (lines 116-130)

  Removes LRO response entries whose first HTTP status-code entry is `201` or
  `202`, on the basis that those initial responses are handled internally.

  For LROs whose first request method is `delete` or `post`, adds a synthetic
  `200` response if none remains. This response contains protocol/status
  metadata but no body schema. Despite the comment mentioning deletion, the
  implementation explicitly includes POST operations.

  Only the first request method and first status-code entry of each response
  are considered. Paging inference has already run, and this response cleanup
  does not recompute its metadata.

- **Treat API-version parameters as constants or selectable versions**
  (lines 136-165)

  Scans `operation.parameters` for the exact
  `language.default.serializedName` value `api-version`, using the containing
  operation's `apiVersions` list.

  A list containing exactly one entry supplies
  `parameter.language.default.constantValue`. Any other present list, including
  an empty array, clears that constant and sets
  `language.default.apiversion = true` for downstream version/profile handling.
  When `apiVersions` is absent, the parameter is left unchanged.

  The comment proposes defaulting a selectable version to the latest version,
  but this code does not select or assign such a default. The commented-out
  guard for preserving an existing constant is also inactive.

- **Mark eligible resource models for by-reference handling** (lines 168-197)

  Runs only when both `azure` and `make-sub-resources-byreference` are enabled.
  Other transformations in this file are not individually gated by those
  configuration values.

  A candidate object schema must have a property, including inherited
  properties returned by `getAllProperties()`, whose serialized name is `id`
  and whose `language.default.readOnly` value is falsy.

  The pass then looks for a PUT request on an operation whose response schema
  is the exact same schema object. A match sets
  `schema.language.default.byReference = true`, marking the shared model for
  downstream handling rather than replacing or inlining it here.

  Having an `id` property alone is insufficient: the PUT-response relationship
  is required. The writable check reads `language.default.readOnly`, not the
  property's top-level `readOnly` field.

## `plugin-create-inline-properties.ts`

Analysis of `C:\workspace\autorest.powershell\powershell\plugins\plugin-create-inline-properties.ts`.
Line references in this section refer to that source file unless stated
otherwise.   Builds a flattened, generator-facing view of model properties and command
parameters. It records virtual members over the existing schemas rather than
replacing nested wire properties with a flattened JSON body.

`createVirtuals()` reads `inlining-threshold`, defaulting to `24`, processes
object schemas, then processes `state.model.commands.operations`. These are
command operations, not the original REST operation groups.

`createInlinedPropertiesPlugin()` initializes `ModelState<PwshModel>`, runs
the pass, and writes `code-model-v4-create-virtual-properties-v2.yaml` with
artifact type `code-model-v4`.

- **Build owned, inherited, and inlined property collections**
(lines 56-71, 100-157, 294-318, 422-426)

Removes falsy entries from each processed schema's property list and creates
`language.default.virtualProperties` with `owned`, `inherited`, and `inlined`
arrays. Properties that are not expanded remain represented in `owned`.

Immediate object parents are processed recursively; non-object parents are
skipped. The parent's inherited, inlined, and owned virtual properties are
copied into the child's `inherited` collection, preserving access links,
visibility, mutability, and requiredness metadata.

Inherited copies retain references to their underlying properties and share
a `sharedWith` array with related virtual members. They are not independent
copies of the source schemas.

The recursive helper uses `inline = 'yes'` or `'no'` to reuse prior decisions.
Separately, the outer schema loop skips schemas with a truthy `inlined` flag;
those are two different metadata fields.

- **Decide which nested object properties can be expanded** (lines 160-196)

  A candidate must have an object schema with direct or inherited properties.
  Neither the containing schema nor the property's schema may be marked
  `skip-inline`. Arrays, dictionaries, and empty objects remain ordinary
  properties rather than having their contents expanded.

  After recursively preparing the child, expansion also requires its
  `inline` state to be `yes`, no `byReference` marker, and no immediate
  dictionary parent. This consumes the no-inline and resource-reference
  annotations established by earlier passes.

  The child count sums all three virtual-property collections, including
  private entries, and must be strictly less than the threshold. A property
  whose default-language name is exactly `properties` bypasses the count
  limit, but not the other eligibility checks.

  The apparent requiredness safeguard is ineffective: `allNotRequired` calls
  `getAllPublicVirtualProperties()` without passing the child's collection.
  That helper returns an empty array when no argument is supplied
  (`powershell\utils\schema.ts:161-169`), so the all-not-required check succeeds
  without examining any child properties.

- **Preserve private backing properties and record forwarding paths**
  (lines 198-289)

  For an expanded object member, adds a private virtual property to `owned`
  representing the original object-valued property. It then adds forwarding
  entries to `inlined` for the child's inherited, owned, and already-inlined
  members.

  Each forwarding entry retains its underlying `Property` and records
  `accessViaProperty`, `accessViaMember`, and `accessViaSchema`. These links
  describe how downstream code reaches the nested value without removing the
  original object boundary from the wire schema. Private child members retain
  their private marker when copied.

- **Construct flattened names and resolve inlined-property collisions**
  (lines 19-32, 42-54, 217-227, 253-268, 320-335)

  Direct and inherited child members normally receive a PascalCase prefix
  derived from the singularized containing property name. The exact names
  `properties` and `error` suppress that prefix. Already-inlined descendants
  keep their existing names instead of receiving another outer prefix.

  Singularization uses `EnglishPluralizationService` with explicit
  Database/Databases and Premise/Premises entries, including lowercase forms.
  Name components remove consecutive duplicates, and alternatives include
  component-path variants and a type-name-plus-leaf fallback.

  Collision counting includes owned, inherited, and inlined entries, but this
  loop renames only inlined entries. It processes those with fewer naming
  alternatives first and delegates alternative selection to `selectName()`.
  Comparisons use case-sensitive map/set keys; duplicates confined to owned or
  inherited entries are not repaired by this loop.

- **Propagate mutability, read-only status, and requiredness**
  (lines 132-149, 211-213, 226-249, 263-285, 300-315)

  `getMutability()` enables create, update, and read by default; when
  `x-ms-mutability` is present, only the listed capabilities are enabled
  (`powershell\utils\schema.ts:350-360`).

  Ordinary virtual properties combine those capabilities with the source
  property's top-level `readOnly` flag. Inlined members intersect the
  containing property's capabilities with the child's capabilities. Read-only
  status also propagates from either level, and a readable member with neither
  create nor update capability becomes read-only.

  An owned member is required when either the source property's `required` or
  `language.default.required` is truthy. An inlined member is required only
  when both the child member and its private containing property are required.
  Inherited copies preserve their existing flags.

- **Detect recursive inlining and fail with no-inline guidance**
  (lines 74-100, 337-338, 432-438)

  Marks schemas `inline = 'inprogress'` while traversing them and `yes` when
  processing finishes. Revisiting an in-progress schema records a circular
  reference diagnostic, includes the traversal's model names in a suggested
  `no-inline` directive, and returns a negative inlining decision.

  After schema processing, any accumulated conflicts are reported through
  `state.error()` and cause an exception before command-parameter processing.
  The local negative decision is not a successful fallback that silently
  accepts recursive expansion.

- **Enrich descriptions without using one uniform documentation field**
  (lines 34-40, 201-210, 301-310, 378-381, 391-399)

  Appends an external-documentation message when a schema's `externalDocs.url`
  is nonblank. For properties, the result is written to the underlying
  property's `language.default.description`; for ordinary command parameters,
  it is written to the parameter schema's description.

  Virtual-property descriptions themselves come from `property.summary` or an
  empty string. Expanded body parameters use the underlying property's full
  description, while ordinary virtual parameters use
  `parameter.details.default.description`. Updating a schema description does
  not automatically update all those other fields.

- **Build the ordinary virtual command-parameter surface**
  (lines 341-360, 389-410)

  Creates separate `operation` and `body` virtual-parameter arrays under
  `operation.details.default.virtualParameters`. Parameters with a truthy
  `constantValue`, `fromHost`, or `apiversion` are omitted from the exposed
  surface.

  The constant test here checks truthiness, not whether a value is defined;
  falsy constants are not excluded by that check. This differs from the
  constant check used for expanded body properties.

  Unless a body parameter is being dropped and expanded, the parameter becomes
  an ordinary virtual parameter retaining its schema and source parameter as
  `origin`. A retained body parameter is always marked required, whereas other
  parameters preserve their existing requiredness.

  Finally, `resolveParameterNames([], virtualParameters)` resolves parameter
  collisions with operation parameters taking precedence over expanded body
  parameters. Its helper uses case-sensitive names and receives no reserved
  names from this call (`powershell\utils\resolve-conflicts.ts:21-52`).

- **Expand dropped body parameters into eligible writable members**
  (lines 349-388)

  Expansion requires both the command's `dropBodyParameter` flag and the
  parameter's `isBodyParameter` flag. It reads the schema's existing inherited,
  owned, and inlined virtual properties; it does not discover new properties
  directly from the schema at this point.

  Excludes private members, virtual or underlying read-only properties,
  properties with a defined `language.default.constantValue`, and properties
  marked `HeaderProperty = 'Header'`. The header check does not itself exclude
  `HeaderAndBody`.

  Create operations additionally require the member's `create` capability;
  update operations require `update`. Each retained member becomes a body
  virtual parameter carrying its name alternatives, description, underlying
  schema, and virtual-property origin.

  If the dropped body's schema has no virtual-property collection, this
  branch adds no parameters and does not restore the original body parameter.

- **Relax required body members for read-modify-write command variants**
  (lines 382, 448-457)

  An optional original body parameter makes every expanded body member
  optional. For a required body, normal commands retain each virtual member's
  requiredness.

  `GetPut` and `ManagedIdentityUpdate` command types instead make members
  optional unless the member is unreadable but has create or update
  capability. Such write-only members retain their original requiredness.
  This decision uses command-type and mutability metadata, not just the HTTP
  method or the member's schema-level required flag.

## `create-commands-v2.ts`

Analysis of `C:\workspace\autorest.powershell\powershell\plugins\create-commands-v2.ts`.
Line references in this section refer to that source file unless stated
otherwise.

Invents the cmdlet surface. It reads `model.operationGroups` and writes
`model.commands.operations`, a dictionary keyed by insertion index where each
entry is one PowerShell parameter set: a verb, a subject, a variant suffix, and
its own cloned parameter list. One REST operation normally yields several
entries. Nothing is removed from the operation groups — commands are an
additional projection beside them.

This is the stage `plugin-create-inline-properties.ts` consumes: the `Expanded`
commands emitted here carry `dropBodyParameter`, which that later pass turns
into virtual parameters.

`createCommandsV2()` (line 732) builds `ModelState<PwshModel>`, runs
`new Inferrer(state).init()` then `createCommands()`, and writes
`code-model-v4-createcommands-v2.yaml` with artifact type `code-model-v4`.

- **Resolve configuration and the service name** (lines 29-38, 122-163)

  Reads `azure`, `verb-mapping`, `prefix`, `service-name`, `subject-prefix`, and
  `support-json-input` (defaulting to `true` under `azure`, `false` otherwise),
  then writes `serviceName`, `subjectPrefix`, `prefix`, and `isAzure` back onto
  the model and state.

  `titleToAzureServiceName()` strips `Client`, `Azure`, `Microsoft`, `APIs`,
  `API`, `REST`, and non-leading `Management`, then removes a trailing
  `ServiceResourceProvider`, `ResourceProvider`, `DataPlane`, or `Data`. Its
  `// UNUSED: Moved to plugin-tweak-model.ts` comment is stale — `init()` calls
  it. The `(?=.*)` in the regex matches unconditionally and has no effect.

- **Classify an operation group and synthesize Update from GET+PUT**
  (lines 164-262, 264-281)

  Each group is scanned once, bucketing operations by HTTP method into
  `getOperations`, `putOperation`, and `patchOperation`, while emitting the
  ordinary variants for every operation.

  Under `azure` with identity transformation enabled, a PATCH whose body carries
  a writable `identity.type` property is *skipped* from ordinary generation, and
  such a PUT is tagged `CommandType.ManagedIdentityNew`.

  `supportsGetPut()` then decides whether a GET can feed a PUT: the GET must
  have no query parameter other than `apiVersion`, the PUT must have exactly one
  request with a body schema, and *every* GET response schema must be that same
  schema object. Combined with a path match between the two, this produces an
  `Update` command built from the **PUT's** parameters but the **GET's** inferred
  name, with the GET recorded as a pre-operation — `CommandType.GetPut`, or
  `ManagedIdentityUpdate` when identity is involved.

  A managed-identity PATCH that cannot be combined is a hard **error**: the pass
  throws unless the operation id appears in
  `disable-transform-identity-type-for-operation`, pointing at the
  managed-identity best-practices document.

- **Infer verb and subject from the operation id** (lines 283-362, 364-394, 701-730)

  The group name is deconstructed, sequential duplicates removed, its last word
  singularized, and PascalCased. An entry in `verb-mapping` short-circuits
  everything; otherwise `inferCommand()` walks the deconstructed method name
  (the literal word `all` is dropped first):

  - one word — that word is the action, the group is the subject;
  - two words — whichever is a known verb becomes the action, else the first is
    taken as the action with a warning;
  - three or more — split on `or` (yielding two commands), else on the first of
    `with`, `at`, `by`, `for`, `in`, `of` (the tail becomes the variant suffix),
    else locate a verb.

  `filterSpecialWords()` suppresses the `in` split when preceded by `sign`, so
  `signIn` survives. When the located verb is neither first nor last, the subject
  is built from `operation.slice(i, i)` (line ~355) — always empty, so the
  subject collapses to the group name alone. `slice(0, i)` appears to be
  intended.

  `createCommandVariant()` maps the action through `verb-mapping`, falling back
  to `Invoke`; when it falls back and the action is not `post`, the action is
  prepended to the subject. Every subject word is singularized and sequential
  duplicates removed. `alias` is always emitted empty.

- **Emit the parameter-set variants for one operation** (lines 561-595, 637-697)

  Parameters are split into constants (`constantValue` or `fromHost`) and the
  rest. The body is the first request parameter not marked
  `modelerfour:synthesized`. A `Update` verb on a PUT is rewritten to `Set`;
  the presence of pre-operations forces both action and verb to `Update`.

  With `support-json-input` — and only when the command is neither `GetPut` nor
  `ManagedIdentityUpdate` — two further commands are added,
  `…ViaJsonString` and `…ViaJsonFilePath`. Each drops the real body parameter,
  adds a synthesized required string parameter, and re-adds a body parameter
  carrying the schema. Both push the *same* `IParameter` instance, so the two
  commands share one object.

- **ViaIdentity variants walk the resource path outward** (lines 596-636)

  Skipped entirely under `disable-via-identity`. Path parameters are filtered
  against `reservedPathParam`, which holds `'SubscriptionId'` and
  `'resourceGroupName'` — inconsistent casing against the
  `language.default.name` values it is compared with, so one of the two will not
  match on a given spec.

  With `enable-parent-pipeline-input` off (it defaults to `isAzure`), a single
  `ViaIdentity` variant is emitted when path parameters remain and the action is
  not `list`. With it on, the path is walked from the innermost segment outward,
  producing `ViaIdentity` for the resource itself and `ViaIdentity{Resource}` for
  each ancestor, with the remaining inner names kept as parameters. Resource
  names come from `getResourceNameFromPath()`; the walk stops at the first
  segment whose name cannot be derived.

- **Attach the body and emit the `Expanded` companion** (lines 396-450)

  `addVariant()` first rewrites `operation.language.default.description`,
  replacing a leading "create or update"/"creates"/"updates" phrase with the
  variant's action. This mutates the shared operation object, so the result
  depends on how many variants have already been generated from it.

  A body parameter is appended as an `IParameter` marked `isBodyParameter`. When
  the body schema is an object, dictionary, or `any`, a second command named
  `{vname}Expanded` is registered with `dropBodyParameter = true` and a
  `{bodyParameterName}Body` parameter — the hand-off to
  `plugin-create-inline-properties.ts`.

- **Register the command, trim and de-duplicate its name** (lines 452-559)

  Names longer than 64 characters are rebuilt from deconstructed words until the
  result exceeds 60. The loop assigns before testing, so the kept value is the
  first that *exceeds* 60 rather than the last under it; and when no prefix
  exceeds 60 it ends at `slice(0, length - 1)`, silently dropping the final word.

  Uniqueness is enforced on `{verb}-{subject}-{vname}`: on collision, trailing
  digits are stripped and an incrementing counter appended.

  Pre-operations force `commandName` and `language.default.name` to `Update` and
  prepend them to `callGraph`. `operationType` — later consumed for
  `x-ms-mutability` — is `Create` for a PUT whose action is create (or update via
  the `Set` verb), `Update` for a PATCH or PUT whose action is update, otherwise
  `Other`.

  Every parameter is cloned per command (excluding `schema` and `origin`) so
  later passes can mutate one command's copy without affecting another. The clone
  PascalCases `language.default.name`, keeps the original under `httpParameter`,
  and sets `name` to the serialized name. `operationId` is deliberately written
  as an empty string.

- **Dead code** (lines 56-68, 112-126)

  `fn()`/`combinations()` compute all subsequences and are never called.
  `commonParameters` is assigned in `init()` — `['subscriptionId']` under
  `azure`, with `resourceGroupName` commented out — and never read.