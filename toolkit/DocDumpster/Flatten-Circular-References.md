# Flatten: Circular References and the `properties` Envelope

Decisions for how the `flatten-model` stage
(`packages/transformers/src/flatten-model-step.ts`) handles recursive model
references. They are implemented in that file; "the current implementation"
below means the behavior before this change.

## Problem

Model references can form cycles. For example:

```tsp
model PublicIPAddress {
  properties?: PublicIPAddressPropertiesFormat;
}

model PublicIPAddressPropertiesFormat {
  servicePublicIPAddress?: PublicIPAddress;
}
```

Flattening every nested object reference would never finish. Recursive C# types
are valid; recursively flattening every member into parameters is not.

The current implementation reports a warning and stops at the first reference
that leads back to a model it is still flattening. Which reference that is
depends on the order of `IR.models`, so the order can change the public surface:

| Model flattened first | Reference left nested |
|---|---|
| `PublicIPAddress` | `servicePublicIPAddress`, the intended break |
| `PublicIPAddressPropertiesFormat` | `properties`, which should have been flattened |

The current `noInline` cannot express the intended break either. It stops the
named model from being flattened into its containers *and* stops the model's own
members from being flattened (the step returns before looking at its
properties). AutoRest's `skip-inline` behaves the same way
(`autorest.powershell/powershell/plugins/plugin-create-inline-properties.ts:162-168`).
Either choice in the cycle above therefore loses the `properties` flattening.

## Decisions

### 1. The `properties` envelope always flattens

Treat the ARM `properties` envelope separately from cycle handling. Flattening
a property whose wire name is `properties` is a default rule:

- It ignores `inliningThreshold`, as it already does.
- It ignores `noInline`, which is new.
- It never counts as a place to break a cycle.

The `error` envelope keeps its existing treatment: no name prefix and no
threshold, but `noInline` still applies to it.

An explicit spec hint on the property (`rendering.flatten.into = "never"`) still
overrides the default rule.

### 2. A cycle must be broken explicitly, or the run fails

Follow AutoRest: an unresolved cycle is an `error` diagnostic, so the stage stops
the run instead of picking a break silently. Developers must know where a cycle
is broken; flattening must not depend on traversal order.

- Detect cycles through every model reference the stage would flatten, whatever
  the property is called, not only through `properties`. Arrays and dictionaries
  are never flattened, so cycles through them are not reported.
- Report each cycle found once, all in a single run. Detection follows each
  reference that leads back to a model still being walked; in a tangle of
  overlapping cycles, fixing the reported ones can reveal another on the next run.
- List only the models in the cycle, not the whole traversal path. A model
  outside the cycle is not a valid fix.
- Name the exact configuration that fixes it. A `properties` reference cannot be
  a break, so the only candidates are the models that the cycle's other
  references point to. For the example above, the only candidate is
  `noInline: [PublicIPAddress]`.

### 3. `noInline` keeps references to a model nested

`noInline: [M]` means: **every property typed `M` stays a nested `M` object; `M`'s
own members still flatten as usual.**

With `noInline: [PublicIPAddress]`, the example renders as:

```text
PublicIPAddress (model and command surface)
  properties              -> always flattened (decision 1)
  ServicePublicIPAddress  -> stays a nested PublicIPAddress object (the configured break)
  ...other properties.* members flattened
```

- One break applies to both layers. The C# model accessors and command
  parameters both come from `ModelHints.flattened`, so the model and the command
  surface agree.
- The break is deterministic, so the order of `IR.models` no longer matters.
- `noInline` remains a general "keep this nested" setting and can name models
  that are not in a cycle.
- The setting is per model, so it keeps every reference to `M` nested, wherever
  it appears. A property-level setting is deferred until a real case needs one.

## Edge cases

| Case | Behavior |
|---|---|
| A cycle made only of `properties` references (`A.properties -> B`, `B.properties -> A`) | `error`: `noInline` cannot break it. Expected to be very rare. |
| `noInline` names a model that is only reached through `properties` | `warning`: the entry has no effect. The same warning covers an entry no property refers to at all. |
| `noInline` names a model that is not in any cycle | Allowed, with the same meaning as decision 3. |
| The reference in the cycle is read-only | Still an `error`. The model has the cycle even if the command surface would not show it. |

## Out of scope

- **Command-line input for a nested-object parameter.** A `noInline` break puts a
  model-typed public parameter on the command, an explicit exception to the
  rule that public parameters are primitives the command line can supply. How
  users supply such a value is handled separately.
- **Arrays and dictionaries of objects.** They are never flattened. Their public
  surface is not part of this decision.

## Compared with AutoRest PowerShell

| Concern | AutoRest | Decided here |
|---|---|---|
| Unresolved cycle | Error; run fails | Error; run fails |
| Suggested fix | Lists every model on the traversal path | Names only the valid `noInline` candidates in the cycle |
| `no-inline` / `noInline` on a model | Model is not flattened into containers, and its own members are not flattened either | Model is not flattened into containers; its own members still flatten |
| `properties` envelope | Exempt from the size threshold; can still be blocked by `no-inline` | Always flattened; never a place to break a cycle |
