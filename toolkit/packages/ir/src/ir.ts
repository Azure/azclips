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
import { IRCommandSchema, commandActionKey, commandKey, type IRCommand } from "./command.js";
import { IRTargetSchema, type IRTarget } from "./target.js";
import { idKey, serializeId, type IRId } from "./id.js";

export type IR = {
  schemaVersion: string;
  sources: IRSource[];
  servers: IRServerDefinition[];
  models: IRModel[];
  enums: IREnum[];
  discriminatedUnions?: IRDiscriminatedUnion[] | undefined;
  operations: IROperation[];
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

export const IRSchema: z.ZodType<IR> = IRSchemaRoot.superRefine(checkServers);

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
 * boundary instead of a null dereference inside it.
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
    /** Every command, flat. Array order is not meaningful. */
    commands: IRCommand[];
    /** Resolved placement for the generated project. */
    target: IRTarget;
  }
>;

/** Extends `IRSchemaRoot`, so a field added to `IR` reaches both roots. */
const TransformedIRSchemaRoot = IRSchemaRoot.extend({
  commands: z.array(IRCommandSchema),
  target: IRTargetSchema,
});

const _transformedIrMatchesSchema: Exact<z.infer<typeof TransformedIRSchemaRoot>, TransformedIR> = true;
void _transformedIrMatchesSchema;

export const TransformedIRSchema: z.ZodType<TransformedIR> = TransformedIRSchemaRoot.superRefine(
  (ir, ctx) => {
    checkServers(ir, ctx);

    // Commands reference operations and servers by id. A dangling reference is a
    // generator crash otherwise, so it is caught at the boundary instead.
    const operations = new Set(ir.operations.map((operation) => idKey(operation.id)));
    const pathParameters = new Set(
      ir.operations.flatMap((operation) =>
        operation.pathParameters.map((parameter) => idKey(parameter.id)),
      ),
    );
    const servers = new Set(ir.servers.map((server) => idKey(server.id)));
    const names = new Set<string>();
    const identities = new Set<string>();
    const models = new Map(ir.models.map((model) => [idKey(model.id), model]));
    const properties = new Map(ir.models.flatMap((model) => model.properties.map((property) => [idKey(property.id), { model, property }] as const)));
    const psFamilies = new Map<
      string,
      { commands: { index: number; name: string; defaultSet?: string | undefined }[]; sets: Set<string> }
    >();

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
      if (identities.has(key)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `Two commands share the identity ${key}; (module, group, action, variant) must be unique.`,
          path: ["commands", index, "id"],
        });
      }
      identities.add(key);

      const identity = command.dialect?.ps?.identity;
      if (identity !== undefined) {
        const parameterIndex = command.parameters.findIndex((parameter) => parameter.name === identity.parameter);
        const parameter = command.parameters[parameterIndex];
        let identityModel: IRModel | undefined;
        if (parameter === undefined || parameter.source.kind !== "synthetic" || parameter.type.kind !== "model") {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: `Command '${command.name}' PowerShell identity must reference a synthetic model-typed command parameter.`,
            path: ["commands", index, "dialect", "ps", "identity", "parameter"],
          });
        } else {
          identityModel = models.get(idKey(parameter.type.id));
          if (identityModel === undefined) {
            ctx.addIssue({
              code: z.ZodIssueCode.custom,
              message: `Command '${command.name}' identity parameter '${parameter.name}' references model ${serializeId(parameter.type.id)}, which is not in IR.models.`,
              path: ["commands", index, "parameters", parameterIndex, "type", "id"],
            });
          }
        }

        identity.properties.forEach((binding, propertyIndex) => {
          if (identityModel !== undefined) {
            const resolved = properties.get(idKey(binding.property));
            if (resolved === undefined || idKey(resolved.model.id) !== idKey(identityModel.id)) {
              ctx.addIssue({
                code: z.ZodIssueCode.custom,
                message: `Command '${command.name}' identity property ${serializeId(binding.property)} is not on its identity model.`,
                path: ["commands", index, "dialect", "ps", "identity", "properties", propertyIndex, "property"],
              });
            }
          }
          binding.sourceParameters.forEach((sourceParameter, sourceParameterIndex) => {
            if (!pathParameters.has(idKey(sourceParameter))) {
              ctx.addIssue({
                code: z.ZodIssueCode.custom,
                message: `Command '${command.name}' identity property supplies ${serializeId(sourceParameter)}, which is not a canonical path parameter.`,
                path: ["commands", index, "dialect", "ps", "identity", "properties", propertyIndex, "sourceParameters", sourceParameterIndex],
              });
            }
          });
        });
      }

      if (command.dialect?.ps !== undefined) {
        const familyKey = commandActionKey(command.id);
        const family = psFamilies.get(familyKey) ?? { commands: [], sets: new Set<string>() };
        family.commands.push({
          index,
          name: command.name,
          defaultSet: command.dialect.ps.defaultParameterSet,
        });
        command.parameters.forEach((parameter) => {
          parameter.dialect?.ps?.parameterSets?.forEach((membership) => {
            if (membership.set !== undefined) family.sets.add(membership.set);
          });
        });
        psFamilies.set(familyKey, family);
      }
    });

    psFamilies.forEach((family) => {
      const defaults = new Set(
        family.commands.flatMap((command) =>
          command.defaultSet !== undefined ? [command.defaultSet] : [],
        ),
      );
      if (defaults.size === 0) return;

      const [defaultSet] = defaults;
      family.commands.forEach((command) => {
        if (defaults.size > 1 || command.defaultSet !== defaultSet) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            message: `PowerShell sibling commands must agree on one default parameter set; '${command.name}' does not.`,
            path: ["commands", command.index, "dialect", "ps", "defaultParameterSet"],
          });
        }
      });

      if (defaults.size === 1 && defaultSet !== undefined && !family.sets.has(defaultSet)) {
        const command = family.commands[0]!;
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `PowerShell command family names default parameter set '${defaultSet}', which none of its parameters declares.`,
          path: ["commands", command.index, "dialect", "ps", "defaultParameterSet"],
        });
      }
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
