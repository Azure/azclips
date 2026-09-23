/**
 * Annotate `TransformedIR` commands with their PowerShell dialect -- the `ps.derive`
 * stage. Runs after `create-command`, which has already decided command identity,
 * the flat parameter surface (body leaves included, via `flatten-model`'s
 * projection), and merged nothing across operations.
 *
 * This step annotates canonical commands and derives the shared PowerShell client
 * surface. It creates no commands and no second command graph -- `IR.ps` is gone.
 *
 * ## What this derives
 *
 *   - **verb/noun** -- resolved once per cmdlet family (`commandActionKey`: same
 *     module, subject, and action) and written onto every sibling identically,
 *     since `PsCommandDialect.verb`/`.noun` is a family-level fact (see
 *     `dialect.ts`'s docs on `PsCommandDialect`).
 *   - **defaultParameterSet** -- resolved from the family member whose own parameters
 *     need the fewest required values, then written identically onto every sibling.
 *     The transformed-document schema validates this family-level agreement and
 *     resolves the named set across all siblings' memberships.
 *   - **supportsShouldProcess/confirmImpact** -- per command, from its own step's
 *     HTTP method.
 *   - **parameter-set membership** -- only stated when a family has more than one
 *     sibling: a single-command family has exactly one implicit set, and
 *     `PsParameterDialect.parameterSets` being absent already means "every set"
 *     (see `dialect.ts`).
 *   - **name shortening** -- a path parameter whose name restates the subject
 *     (`ElasticSanName` on `Get-AzElasticSan`) is shortened to `Name`, the full name
 *     kept as an alias, unless that would collide with another parameter.
 *   - **module and identity** -- target-owned module metadata, one canonical input
 *     model built from path parameters, and one synthetic InputObject parameter per
 *     command with command-local property-to-path bindings.
 *
 * ## What it deliberately does not derive (schema gaps, not oversights)
 *
 *   - **No `createOrUpdate` -> New/Update fan-out.** `create-command` decided
 *     command identity once and never splits one operation into two commands, so a
 *     compound action word resolves to a single verb (`resolveVerb`'s leading-word
 *     fallback -- `New` for `CreateOrUpdate`) instead of the pair AutoRest emits.
 *   - **`completer`/`defaultValue`/`examples`/`helpUri`/`lifecycleTag`.** These have
 *     no IR-derivable source; they are authored, and belong to `ps.directives`.
 */

import {
  commandActionKey,
  idKey,
  type IRCommand,
  type IRCommandParameter,
  type IRId,
  type IRModel,
  type IRProperty,
  type PsParameterDialect,
  type TransformedIR,
} from "@azure-client-engine/ir";
import { resolveVerb } from "./verb-mapping.js";
import { deduplicateNoun } from "./naming.js";

export type DeriveDialectOptions = {
  /** Module-level prefix applied to every noun, e.g. "Az". Default: undefined (none). */
  modulePrefix?: string | undefined;
  /** Authored service prefix, e.g. "ElasticSan" -> "AzElasticSanVolume". Default: undefined (none). */
  subjectPrefix?: string | undefined;
};

/** PUT/POST/PATCH/DELETE mutate; GET does not. */
const MUTATING_METHODS: ReadonlySet<string> = new Set(["PUT", "POST", "PATCH", "DELETE"]);

/**
 * Mutate every command in `ir.commands`, setting `dialect.ps` on each command and
 * its parameters. Returns the same document: commands are annotated in place, which
 * is the shape a `TransformStep` expects (see `transform.ts`).
 */
export function deriveDialect(ir: TransformedIR, options: DeriveDialectOptions = {}): TransformedIR {
  ir.target.dialect = {
    ...ir.target.dialect,
    ps: {
      ...ir.target.dialect?.ps,
      name: ir.target.projectName,
      ...(options.modulePrefix !== undefined ? { prefix: options.modulePrefix } : {}),
      ...(options.subjectPrefix !== undefined ? { subjectPrefix: options.subjectPrefix } : {}),
    },
  };

  removeDerivedIdentities(ir);

  const identity = deriveIdentity(ir, options);
  if (identity !== undefined) ir.models.push(identity.model);

  const families = new Map<string, IRCommand[]>();
  for (const command of ir.commands) {
    const key = commandActionKey(command.id);
    const bucket = families.get(key);
    if (bucket) bucket.push(command);
    else families.set(key, [command]);
  }

  for (const siblings of families.values()) {
    applyFamily(siblings, options, identity);
  }

  return ir;
}

type DerivedIdentity = {
  model: IRModel;
  propertiesByParameterName: ReadonlyMap<string, IRProperty>;
};

function removeDerivedIdentities(ir: TransformedIR): void {
  const modelIds = new Set<string>();
  for (const command of ir.commands) {
    const identity = command.dialect?.ps?.identity;
    if (identity === undefined) continue;

    const parameter = command.parameters.find(
      (candidate) => candidate.name === identity.parameter && candidate.source.kind === "synthetic",
    );
    if (parameter?.type.kind === "model") modelIds.add(idKey(parameter.type.id));
    command.parameters = command.parameters.filter((candidate) => candidate !== parameter);
    delete command.dialect?.ps?.identity;
  }

  if (modelIds.size > 0) {
    ir.models = ir.models.filter((model) => !modelIds.has(idKey(model.id)));
  }
}

function deriveIdentity(
  ir: TransformedIR,
  options: DeriveDialectOptions,
): DerivedIdentity | undefined {
  const candidates = new Map<string, IRCommandParameter>();
  for (const command of ir.commands) {
    for (const parameter of command.parameters) {
      if (parameter.source.kind !== "path") continue;

      const key = parameter.name.toLowerCase();
      if (!candidates.has(key)) candidates.set(key, parameter);
    }
  }

  if (candidates.size === 0) return undefined;

  const rootName = ir.target.rootNamespace[ir.target.rootNamespace.length - 1];
  const name = `${options.subjectPrefix ?? rootName ?? "Module"}Identity`;
  const source = ir.sources[0]?.id ?? ir.target.projectName;
  const id: IRId = { module: source, namespace: [], name };
  const propertiesByParameterName = new Map<string, IRProperty>();
  for (const [key, parameter] of candidates) {
    propertiesByParameterName.set(key, identityProperty(id, parameter));
  }

  return {
    model: {
      id,
      name,
      source,
      usage: ["input"],
      properties: [...propertiesByParameterName.values()],
    },
    propertiesByParameterName,
  };
}

function identityProperty(model: IRId, parameter: IRCommandParameter): IRProperty {
  const common = {
    id: { ...model, name: `${model.name}.${parameter.name}` },
    name: parameter.name,
    wireName: parameter.name,
    optional: false,
    nullable: parameter.nullable,
    ...(parameter.docs !== undefined ? { docs: parameter.docs } : {}),
  };

  switch (parameter.type.kind) {
    case "primitive":
      return { ...common, typeKind: "primitive", type: parameter.type };
    case "model":
    case "dictionary":
      return { ...common, typeKind: "object", type: parameter.type };
    case "array":
      return { ...common, typeKind: "collection", type: parameter.type };
    case "enum":
      return { ...common, typeKind: "enum", type: parameter.type };
    case "union":
    case "discriminatedUnion":
      return { ...common, typeKind: "union", type: parameter.type };
  }
}

function requiredCount(command: IRCommand): number {
  return command.parameters.filter((parameter) => parameter.required).length;
}

/** The sibling whose own parameters need the fewest required values (e.g. `List` over `Get`). */
function defaultSiblingOf(siblings: readonly IRCommand[]): IRCommand {
  return siblings.reduce((best, command) => (requiredCount(command) < requiredCount(best) ? command : best));
}

function applyFamily(
  siblings: readonly IRCommand[],
  options: DeriveDialectOptions,
  identity: DerivedIdentity | undefined,
): void {
  const primary = siblings[0]!;
  const verb = resolveVerb(primary.id.action);
  const deduped = deduplicateNoun(options.subjectPrefix ?? "", primary.id.subject);
  const noun = (options.modulePrefix ?? "") + deduped.subjectPrefix + deduped.subject;

  // Only meaningful, and only valid, when more than one set exists: a single-command
  // family has one implicit set, and naming it would be noise no set declares.
  const hasIdentitySet = siblings.some((command) =>
    command.parameters.some((parameter) => parameter.source.kind === "path"),
  );
  const multipleSets = siblings.length > 1 || hasIdentitySet;
  const winner = multipleSets ? defaultSiblingOf(siblings) : undefined;
  // A command with no parameters at all (a bare `List`) has no membership from which
  // to declare its variant as a set. Leave the family default unstated rather than
  // inventing a parameter solely to name it.
  const canStateDefault = winner !== undefined && winner.parameters.length > 0;

  for (const command of siblings) {
    const mutating = MUTATING_METHODS.has(command.steps[0]!.request.method);

    command.dialect = {
      ...command.dialect,
      ps: {
        ...command.dialect?.ps,
        verb,
        noun,
        ...(canStateDefault ? { defaultParameterSet: winner.id.variant } : {}),
        ...(mutating ? { supportsShouldProcess: true, confirmImpact: "Medium" } : {}),
        ...(command.access === "internal" ? { hidden: true } : {}),
      },
    };

    const pathParameters = command.parameters.filter(
      (parameter) => parameter.source.kind === "path",
    );
    const identitySet = pathParameters.length > 0 ? `${command.id.variant}ViaIdentity` : undefined;

    for (const parameter of command.parameters) {
      applyParameter(parameter, command, multipleSets, deduped.subject, identitySet);
    }

    if (identity !== undefined && identitySet !== undefined) {
      const parameterName = uniqueParameterName("InputObject", command.parameters);
      command.parameters.push({
        name: parameterName,
        source: { kind: "synthetic" },
        type: { kind: "model", id: identity.model.id },
        required: true,
        nullable: false,
        docs: "Identity Parameter",
        dialect: {
          ps: {
            parameterSets: [{ set: identitySet, mandatory: true, valueFromPipeline: true }],
          },
        },
      });

      command.dialect!.ps!.identity = {
        parameter: parameterName,
        properties: pathParameters.map((parameter) => ({
          property: identity.propertiesByParameterName.get(parameter.name.toLowerCase())!.id,
          sourceParameters: parameter.source.kind === "path" ? [parameter.source.parameter] : [],
        })),
      };
    }
  }
}

function applyParameter(
  parameter: IRCommandParameter,
  command: IRCommand,
  multipleSets: boolean,
  subject: string,
  identitySet: string | undefined,
): void {
  const shortened =
    parameter.source.kind === "path"
      ? withoutSubjectPrefix(parameter.name, subject, command.parameters)
      : undefined;

  const ps: PsParameterDialect = {
    ...(shortened !== undefined ? { name: shortened, aliases: [parameter.name] } : {}),
    ...(multipleSets
      ? {
          parameterSets: [
            { set: command.id.variant, mandatory: parameter.required },
            ...(identitySet !== undefined && parameter.source.kind !== "path"
              ? [{ set: identitySet, mandatory: parameter.required }]
              : []),
          ],
        }
      : {}),
  };

  if (Object.keys(ps).length > 0) {
    parameter.dialect = { ...parameter.dialect, ps };
  }
}

function uniqueParameterName(
  preferred: string,
  parameters: readonly IRCommandParameter[],
): string {
  const names = new Set(parameters.map((parameter) => parameter.name.toLowerCase()));
  if (!names.has(preferred.toLowerCase())) return preferred;

  let suffix = 2;
  while (names.has(`${preferred}${suffix}`.toLowerCase())) suffix++;
  return `${preferred}${suffix}`;
}

/**
 * Drops a redundant subject prefix from a path parameter name, as `ps-namer-v2.ts`
 * does via `removeProhibitedPrefix`: `Get-AzElasticSan -ElasticSanName` reads better
 * as `-Name`. Refuses a result under two characters, or one that would collide with
 * another parameter already on the command.
 */
function withoutSubjectPrefix(
  name: string,
  subject: string,
  siblings: readonly IRCommandParameter[],
): string | undefined {
  if (subject === "" || !name.toLowerCase().startsWith(subject.toLowerCase())) return undefined;

  const stripped = name.slice(subject.length);
  if (stripped.length < 2) return undefined;

  const candidate = stripped[0]!.toUpperCase() + stripped.slice(1);
  const collides = siblings.some((other) => other !== undefined && other.name === candidate);
  return collides ? undefined : candidate;
}
