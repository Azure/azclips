/**
 * Root IR document.
 *
 * Validation strategy:
 *   - `IRSchema.parse(json)` performs a full Zod validation. Use it at
 *     pipeline boundaries (emit→pipeline, pipeline→generator) and in tests.
 *   - For per-stage validation inside `runPipeline`, the same parse is
 *     fast enough at our expected sizes (low-MB IR); don't pre-optimize.
 */

import { z } from "zod";
import { IR_SCHEMA_VERSION } from "./version.js";
import { IRModelSchema, type IRModel } from "./model.js";
import { IREnumSchema, type IREnum } from "./enum.js";
import { IROperationSchema, type IROperation } from "./operation.js";
import { IRSourceSchema, type IRSource } from "./source.js";
import { IRServerDefinitionSchema, type IRServerDefinition } from "./server.js";
import { IRDiscriminatedUnionSchema, type IRDiscriminatedUnion } from "./discriminated-union.js";
import { IRCommandSchema, commandKey, type IRCommand } from "./command.js";
import { IRTargetSchema, type IRTarget } from "./target.js";
import { idKey, serializeId, type IRId } from "./id.js";
import { IRDeploymentContractSchema, type IRDeploymentContract } from "./deployment.js";
import type { IRTypeRef } from "./type-ref.js";

export type IR = {
  schemaVersion: string;
  sources: IRSource[];
  servers: IRServerDefinition[];
  models: IRModel[];
  enums: IREnum[];
  discriminatedUnions?: IRDiscriminatedUnion[] | undefined;
  operations: IROperation[];
  deployment?: IRDeploymentContract | undefined;
  /**
   * Resolved generation target. Optional here because the emitter cannot produce it:
   * a field is required on `IR` only if the emitter emits it. A transform writes it,
   * and `TransformedIR` requires it.
   */
  target?: IRTarget | undefined;
};

/**
 * The unrefined object shape behind `IRSchema` — the root every document starts
 * from. `TransformedIRSchemaRoot` **extends** it rather than restating its fields, so
 * the two roots cannot drift.
 *
 * Kept separate from `IRSchema` because `.superRefine()` returns a `ZodEffects`,
 * which has no `.extend()`; the refinement is applied last, to each root.
 *
 * Not `z.intersection(IRSchema, …)`: intersecting two `.strict()` objects makes each
 * side reject the other's keys, so every parse fails. It typechecks and fails only at
 * runtime.
 */
const IRSchemaRoot = z
  .object({
    schemaVersion: z.string().min(1),
    sources: z.array(IRSourceSchema).min(1),
    servers: z.array(IRServerDefinitionSchema),
    models: z.array(IRModelSchema),
    enums: z.array(IREnumSchema),
    discriminatedUnions: z.array(IRDiscriminatedUnionSchema).optional(),
    operations: z.array(IROperationSchema),
    deployment: IRDeploymentContractSchema.optional(),
    target: IRTargetSchema.optional(),
  })
  .strict();

/** Server definitions are unique, and every server reference resolves to one. */
function checkServers(ir: IR, ctx: z.RefinementCtx): void {
  // Keyed rather than pairwise: `idKey` agrees with `idEquals`, so this is one
  // pass instead of the quadratic scan a `some()` over earlier entries needs.
  const defined = new Set<string>();
  ir.servers.forEach((server, index) => {
    const key = idKey(server.id);
    if (defined.has(key)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: `Duplicate server definition ${serializeId(server.id)}`,
        path: ["servers", index, "id"],
      });
    }
    defined.add(key);
  });
  const checkReference = (id: IRId, path: (string | number)[]): void => {
    if (!defined.has(idKey(id))) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: `Server reference ${serializeId(id)} has no definition in IR.servers`,
        path,
      });
    }
  };
  ir.sources.forEach((source, index) => checkReference(source.server, ["sources", index, "server"]));
  ir.operations.forEach((operation, index) =>
    checkReference(operation.server, ["operations", index, "server"]),
  );
}

function checkDeployment(ir: IR, ctx: z.RefinementCtx): void {
  if (!ir.deployment) return;
  const issue = (path: (string | number)[], message: string): void =>
    ctx.addIssue({ code: z.ZodIssueCode.custom, path: ["deployment", ...path], message });
  const operations = new Map<string, IROperation[]>();
  for (const operation of ir.operations) {
    const key = idKey(operation.id);
    operations.set(key, [...(operations.get(key) ?? []), operation]);
  }
  const targets = new Map<string, IRTypeRef[]>();
  for (const model of ir.models) {
    targets.set(idKey(model.id), [{ kind: "model", id: model.id }]);
    for (const property of model.properties) {
      const key = idKey(property.id);
      targets.set(key, [...(targets.get(key) ?? []), property.type]);
    }
  }
  for (const operation of ir.operations) {
    for (const parameter of [...operation.pathParameters, ...operation.queryParameters, ...operation.headerParameters]) {
      const key = idKey(parameter.id);
      targets.set(key, [...(targets.get(key) ?? []), parameter.type]);
    }
  }
  const seen = new Set<string>();
  const visitedTypes = new Set<string>();
  const checkType = (type: IRTypeRef, path: (string | number)[]): void => {
    if (type.kind === "array") return checkType(type.element, path);
    if (type.kind === "dictionary") return checkType(type.value, path);
    if (type.kind === "union") {
      type.variants.forEach((variant) => checkType(variant, path));
      return;
    }
    if (type.kind === "primitive") return;
    const key = idKey(type.id);
    const definitions = type.kind === "model" ? ir.models : type.kind === "enum" ? ir.enums : ir.discriminatedUnions ?? [];
    if (definitions.filter((definition) => idKey(definition.id) === key).length !== 1) {
      issue(path, `Deployment ${type.kind} reference must resolve unambiguously: ${serializeId(type.id)}`);
      return;
    }
    const visitKey = `${type.kind}:${key}`;
    if (visitedTypes.has(visitKey)) return;
    visitedTypes.add(visitKey);
    if (type.kind === "model") {
      const model = ir.models.find((definition) => idKey(definition.id) === key)!;
      model.properties.forEach((property) => checkType(property.type, path));
      if (model.additionalProperties) checkType(model.additionalProperties, path);
      if (model.parent) checkType({ kind: "model", id: model.parent }, path);
      if (model.polymorphism) {
        if (model.properties.filter((property) => idKey(property.id) === idKey(model.polymorphism!.discriminator)).length !== 1) {
          issue(path, "Deployment discriminator property must resolve unambiguously");
        }
        model.polymorphism.subtypes.forEach((subtype) => checkType({ kind: "model", id: subtype.type }, path));
      }
    } else if (type.kind === "discriminatedUnion") {
      const union = ir.discriminatedUnions!.find((definition) => idKey(definition.id) === key)!;
      union.variants.forEach((variant) => { if (variant.type) checkType(variant.type, path); });
    }
  };
  ir.deployment.resources.forEach((resource, index) => {
    const path = ["resources", index];
    const key = idKey(resource.operation);
    if (seen.has(key)) issue([...path, "operation"], "Duplicate deployment operation");
    seen.add(key);
    const matches = operations.get(key);
    if (matches?.length !== 1) {
      issue([...path, "operation"], "Deployment operation must resolve unambiguously");
      return;
    }
    const operation = matches[0]!;
    const bodyType = operation.requestBody?.type;
    if (operation.method !== "PUT" || bodyType?.kind !== "model") {
      issue([...path, "operation"], "Deployment operation must be a PUT with a model body");
    } else {
      checkType(bodyType, [...path, "operation"]);
    }
    if (resource.apiVersion !== operation.apiVersion.value) {
      issue([...path, "apiVersion"], "Deployment and operation API versions differ");
    }
    const checkInput = (id: IRId, bindingPath: (string | number)[]): void => {
      if (operation.pathParameters.filter((parameter) => idKey(parameter.id) === idKey(id)).length !== 1) {
        issue(bindingPath, "Deployment input must resolve to an operation path parameter");
      }
    };
    resource.name.forEach((binding, bindingIndex) => {
      if (binding.kind === "parameter") checkInput(binding.parameter, [...path, "name", bindingIndex, "parameter"]);
    });
    resource.scopeParameters.forEach((id, bindingIndex) =>
      checkInput(id, [...path, "scopeParameters", bindingIndex]));
  });
  if (ir.deployment.selection) {
    const selection = new Set<string>();
    ir.deployment.selection.forEach((value, index) => {
      const separator = value.lastIndexOf("@");
      const key = value.slice(0, separator).toLowerCase() + value.slice(separator);
      if (selection.has(key)) issue(["selection", index], "Duplicate deployment selection");
      selection.add(key);
      const matches = ir.deployment!.resources.filter((resource) =>
        `${resource.resourceType.toLowerCase()}@${resource.apiVersion}` === key);
      if (matches.length !== 1) issue(["selection", index], "Deployment selection must resolve to exactly one descriptor");
    });
    ir.deployment.resources.forEach((resource, index) => {
      if (!selection.has(`${resource.resourceType.toLowerCase()}@${resource.apiVersion}`)) {
        issue(["resources", index], "Deployment descriptor is outside the explicit selection");
      }
    });
  }
  const facets = new Set<string>();
  ir.deployment.facets.forEach((facet, index) => {
    const key = `${idKey(facet.target)}:${facet.typePath.join("/")}`;
    if (facets.has(key)) issue(["facets", index], "Duplicate deployment facet");
    facets.add(key);
    const types = targets.get(idKey(facet.target));
    if (!types?.length) {
      issue(["facets", index, "target"], "Deployment facet target has no canonical definition");
      return;
    }
    if (new Set(types.map((type) => JSON.stringify(type))).size !== 1) {
      issue(["facets", index, "target"], "Deployment facet target has ambiguous types");
    }
    for (let type of types) {
      for (const segment of facet.typePath) {
        if (segment === "element" && type.kind === "array") type = type.element;
        else if (segment === "value" && type.kind === "dictionary") type = type.value;
        else if (segment === "additionalProperties" && type.kind === "model") {
          const key = idKey(type.id);
          const additional = ir.models.find((model) => idKey(model.id) === key)?.additionalProperties;
          if (additional) type = additional;
          else issue(["facets", index, "typePath"], "Deployment model has no additional-properties type");
        }
        else {
          issue(["facets", index, "typePath"], "Deployment facet type path does not resolve");
          break;
        }
      }
    }
  });
}

export const IRSchema: z.ZodType<IR> = IRSchemaRoot.superRefine((ir, ctx) => {
  checkServers(ir, ctx);
  checkDeployment(ir, ctx);
});

/**
 * Compile-time guard that a hand-written type and its schema stay in step.
 *
 * The `z.ZodType<T>` annotation alone only catches one direction: a field declared on
 * the type but missing from the schema. A field added to the *schema* but not the
 * type passes silently — it would validate and then be invisible to every consumer.
 *
 * Mutual assignability is not enough either: an **optional** property is assignable
 * in both directions whether present or not, so `a?: string` drift slips through.
 * This is the invariant-position trick, which compares the two types exactly,
 * optionality included.
 */
type Exact<Schema, Declared> =
  (<T>() => T extends Schema ? 1 : 2) extends <T>() => T extends Declared ? 1 : 2
    ? true
    : { error: "schema and declared type have drifted"; schema: Schema; declared: Declared };

const _irMatchesSchema: Exact<z.infer<typeof IRSchemaRoot>, IR> = true;
void _irMatchesSchema;

/**
 * Parse-and-validate an arbitrary value as an IR document. Throws a
 * ZodError on failure; the error's `issues` array pinpoints the path
 * of every violation.
 */
export function parseIR(value: unknown): IR {
  return IRSchema.parse(value);
}

/**
 * Non-throwing variant. Returns `{ success: true, data }` or
 * `{ success: false, error }`.
 */
export function safeParseIR(value: unknown): z.SafeParseReturnType<unknown, IR> {
  return IRSchema.safeParse(value);
}

/** Construct an empty IR scaffold, useful as a transform starting point and in tests. */
export function emptyIR(): IR {
  return {
    schemaVersion: IR_SCHEMA_VERSION,
    sources: [],
    servers: [],
    models: [],
    enums: [],
    discriminatedUnions: [],
    operations: [],
  };
}

// ─── Transformed document ───────────────────────────────────────────────

/**
 * The document once commands exist — the contract between the **transformers and the
 * generator**, as `IR` is the contract between the emitter and the transformers.
 *
 * Two roots rather than one optional field, because the boundaries genuinely differ:
 * the emitter cannot produce commands and must not reference them, while the
 * generator cannot run without them. Making `commands` and `target` required here
 * turns "the command stage did not run" into a validation failure at the generator
 * boundary instead of a null dereference inside it. (`target` may already sit on an
 * `IR`, where it is optional.)
 *
 * Everything in `IR` is carried through unchanged: commands are a *projection* beside
 * the operations, never a replacement, so a captured document can always be traced
 * back to wire truth.
 */
/**
 * Collapse an intersection into a flat object type. `A & B` and `{...A, ...B}` are
 * mutually assignable but not *identical*, and `Exact` compares identity — so the
 * authoring form stays readable while the guard still sees a single object.
 */
type Flatten<T> = { [K in keyof T]: T[K] };

export type TransformedIR = Flatten<
  IR & {
    /**
     * Every command, flat. **Array order is not meaningful** — group with
     * `id.subject` / `id.action`, not with position.
     */
    commands: IRCommand[];
    /** Resolved generation target; optional on `IR`, required here. */
    target: IRTarget;
  }
>;

/**
 * Extends `IRSchemaRoot`, so a field added to `IR` reaches both roots. `target` is
 * re-declared to make it required.
 */
const TransformedIRSchemaRoot = IRSchemaRoot.extend({
  commands: z.array(IRCommandSchema),
  target: IRTargetSchema,
});

const _transformedIrMatchesSchema: Exact<z.infer<typeof TransformedIRSchemaRoot>, TransformedIR> = true;
void _transformedIrMatchesSchema;

export const TransformedIRSchema: z.ZodType<TransformedIR> = TransformedIRSchemaRoot.superRefine(
  (ir, ctx) => {
    checkServers(ir, ctx);
    checkDeployment(ir, ctx);

    // Commands reference operations and servers by id. A dangling reference is a
    // generator crash otherwise, so it is caught at the boundary instead.
    const operations = new Set(ir.operations.map((operation) => idKey(operation.id)));
    const servers = new Set(ir.servers.map((server) => idKey(server.id)));
    const names = new Set<string>();
    const identities = new Set<string>();

    ir.commands.forEach((command, index) => {
      if (!servers.has(idKey(command.server))) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `Command '${command.name}' references server ${serializeId(command.server)}, which has no definition in IR.servers.`,
          path: ["commands", index, "server"],
        });
      }

      command.steps.forEach((step, stepIndex) => {
        if (!operations.has(idKey(step.operation))) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: `Command '${command.name}' step ${stepIndex} references operation ${serializeId(step.operation)}, which is not in IR.operations.`,
            path: ["commands", index, "steps", stepIndex, "operation"],
          });
        }
      });

      // `name` is the generated C# type stem, so a collision would emit two types
      // with one name.
      if (names.has(command.name)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `Two commands are named '${command.name}'; command names must be unique across the document.`,
          path: ["commands", index, "name"],
        });
      }
      names.add(command.name);

      // Identity must distinguish commands even when display names diverge.
      const key = commandKey(command.id);
      if (identities.has(key)) {        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `Two commands share the identity ${key}; (module, group, action, variant) must be unique.`,
          path: ["commands", index, "id"],
        });
      }
      identities.add(key);
    });
  });

/** Parse-and-validate a transformed IR document. Throws a ZodError on failure. */
export function parseTransformedIR(value: unknown): TransformedIR {
  return TransformedIRSchema.parse(value);
}

/** Non-throwing variant of {@link parseTransformedIR}. */
export function safeParseTransformedIR(
  value: unknown,
): z.SafeParseReturnType<unknown, TransformedIR> {
  return TransformedIRSchema.safeParse(value);
}
