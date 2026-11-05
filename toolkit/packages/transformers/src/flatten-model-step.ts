/**
 * Ordered sub-passes over one in-memory IR (deliberately one registered
 * step, not several: the passes share scratch state that has no business in the
 * cross-language IR contract, and a body projection without its model
 * flattening is not a valid configuration):
 *
 *   1. `check-no-inline`     — warn about `noInline` entries that keep nothing nested.
 *   2. `check-cycles`        — fail on circular references the developer has not
 *                              broken with `noInline`; nothing is flattened then.
 *   3. `flatten-models`      — hoist nested object members onto their containing
 *                              model as `ModelHints.flattened`.
 *   4. `project-body-params` — project a request body onto the operation's
 *                              parameter surface as `RequestBodyHints.parameters`.
 *
 * The step is **additive**: it only writes rendering hints, never altering or
 * removing anything the emitter produced. `IRModel.properties` keeps wire truth,
 * and every decision lands under `rendering`. That is what the catalog's
 * `effect: "additive"` declares, and it is why the orchestrator can let this
 * stage share one IR with a sibling branch instead of copying. A container
 * property that is hoisted is marked `flatten.into = "self"` so both this step
 * and the generator can tell it should not also render on its own.
 *
 * It writes **in place**. The orchestrator owns IR lifetime — it hands each
 * stage an object the stage may write to, and isolates a branch itself when the
 * catalog says a stage mutates. A defensive deep copy here would silently double
 * peak memory on a large spec.
 */

import type { TransformContext, TransformStep } from "./transform.js";
import { describeValue, warnUnknownKeys } from "./stage-config.js";
import {
  idKey,
  indexIR,
  serializeId,
  type FlattenedMember,
  type FlattenedParameter,
  type HttpMethod,
  type IR,
  type IRIndex,
  type IRModel,
  type IRProperty,
  type IRPropertyObject,
  type IRPropertyRef,
  type IRVisibility,
} from "@azure-client-engine/ir";

const STEP_NAME = "flatten-model";
const DIAGNOSTIC_PREFIX = `@azure-client-engine/transformers/${STEP_NAME}`;

/**
 * Default `inliningThreshold`: a nested model rendering this many members or more
 * stays nested rather than being flattened into its container. Raising it trades
 * model-typed members for longer flat member and parameter lists.
 */
const DEFAULT_INLINING_THRESHOLD = 100;

const ALL_PHASES: readonly IRVisibility[] = ["read", "create", "update", "delete", "query"];

/**
 * Containers whose members flatten without picking up a name prefix, whatever
 * their size — the inlining threshold does not apply. `properties` is the ARM
 * resource envelope and `error` the ARM error envelope.
 */
const TRANSPARENT_CONTAINERS = new Set(["properties", "error"]);

/**
 * The ARM resource envelope always flattens: `noInline` does not apply to it, so it
 * is never where a circular reference is broken. Only an explicit
 * `flatten.into = "never"` keeps it nested.
 */
const ENVELOPE = "properties";

// ─── Options ────────────────────────────────────────────────────────────

export interface FlattenModelOptions {
  /**
   * A nested model is flattened into its container only while it renders fewer
   * members than this. The count is cumulative — the model's own properties that
   * were not themselves flattened, plus every member flattened into it — so a
   * model can stay nested because of its descendants. `properties` and `error`
   * containers are exempt, and spec decorators such as `@flattenProperty` do not
   * bypass it. 100 when absent; anything but a number, 0 or more, is an error.
   */
  inliningThreshold?: number;
  /**
   * Model names (or `serializeId` forms) that stay nested wherever a property refers
   * to them, which is how a circular reference is broken. The model's own members
   * still flatten, and a `properties` envelope flattens regardless. None when absent;
   * anything but a list of names is an error.
   */
  noInline?: string[];
}

const OPTION_KEYS: ReadonlySet<string> = new Set<keyof FlattenModelOptions>([
  "inliningThreshold",
  "noInline",
]);

interface ResolvedOptions {
  threshold: number;
  noInline: ReadonlySet<string>;
}

/**
 * The step's options, checked: they arrive from config, so the types are not trusted.
 * An absent option (or `null`) takes its default; an invalid one is an error, and
 * every option is checked so one run reports them all. Returns `undefined` when any
 * value is invalid.
 */
function resolveOptions(
  config: Readonly<Record<string, unknown>>,
  report: Report,
): ResolvedOptions | undefined {
  let valid = true;
  const invalid = (key: string, expected: string, value: unknown): void => {
    report("error", "invalid-option", `'${key}' must be ${expected}, not ${describeValue(value)}.`);
    valid = false;
  };
  const present = (value: unknown): boolean => value !== undefined && value !== null;

  const { inliningThreshold, noInline } = config;
  let threshold = DEFAULT_INLINING_THRESHOLD;
  if (present(inliningThreshold)) {
    if (typeof inliningThreshold === "number" && Number.isFinite(inliningThreshold) && inliningThreshold >= 0) {
      threshold = inliningThreshold;
    } else {
      invalid("inliningThreshold", "a number, 0 or more", inliningThreshold);
    }
  }
  const names: string[] = [];
  if (present(noInline)) {
    if (!Array.isArray(noInline)) {
      invalid("noInline", "a list of model names", noInline);
    } else {
      for (const [index, name] of noInline.entries()) {
        if (typeof name === "string") names.push(name);
        else invalid(`noInline[${index}]`, "a model name", name);
      }
    }
  }

  return valid ? { threshold, noInline: new Set(names) } : undefined;
}

// ─── Pass plumbing ──────────────────────────────────────────────────────

type Report = (severity: "error" | "warning", code: string, message: string) => void;

interface PassContext {
  readonly ir: IR;
  readonly options: ResolvedOptions;
  readonly report: Report;
  /** Shared identity lookup, built once for the whole step. */
  readonly index: IRIndex;
}

interface SubPass {
  readonly name: string;
  /** Returning `false` stops the step: later sub-passes depend on what this one checked. */
  run(pass: PassContext): boolean | void;
}

/**
 * A member node carries the naming scratch (`components`, `leafTypeName`)
 * that collision resolution needs but the IR deliberately does not persist.
 */
interface MemberNode extends IRPropertyRef {
  name: string;
  optional: boolean;
  visibility: IRVisibility[] | undefined;
  /** Name fragments, outermost → leaf, used only to build collision fallbacks. */
  components: string[];
  /** Declaring model of the leaf, for the last-ditch `<Type><Member>` option. */
  leafTypeName: string;
}

/** The `flatten-model` stage. Its options are `inliningThreshold` and `noInline`. */
export const flattenModelStep: TransformStep = {
  name: STEP_NAME,
  run(ir: IR, context: TransformContext): IR {
    const report: Report = (severity, code, message) =>
      context.reportDiagnostic({ severity, code: `${DIAGNOSTIC_PREFIX}/${code}`, message });

    warnUnknownKeys(context.config, OPTION_KEYS, "option", report);
    // An invalid option fails the stage. No default stands in for a value the config
    // got wrong, so nothing is flattened, and the compiler stops the run.
    const options = resolveOptions(context.config, report);
    if (options === undefined) return ir;

    // Written in place: the catalog declares this stage `additive`, so the
    // orchestrator has already handed us an IR we own, and no sibling branch
    // can observe a value we change. A defensive deep copy here would double
    // peak memory on a large spec for nothing.
    const pass: PassContext = { ir, options, report, index: indexIR(ir) };
    for (const subPass of SUB_PASSES) {
      if (subPass.run(pass) === false) break;
    }
    return ir;
  },
};

/**
 * Ordered sub-passes. The order is data: flattening relies on the cycle check,
 * and the body projection reads what flattening wrote.
 */
const SUB_PASSES: readonly SubPass[] = [
  { name: "check-no-inline", run: checkNoInline },
  { name: "check-cycles", run: checkCycles },
  { name: "flatten-models", run: flattenModels },
  { name: "project-body-params", run: projectBodyParameters },
];

// ─── Edges ──────────────────────────────────────────────────────────────

function isEnvelope(property: IRProperty): boolean {
  return property.wireName.toLowerCase() === ENVELOPE;
}

function isNoInline(pass: PassContext, model: IRModel): boolean {
  // `noInline` is user-facing config, so it matches the display form, not the key.
  return pass.options.noInline.has(model.name) || pass.options.noInline.has(serializeId(model.id));
}

/**
 * The model a property refers to, when the property is one flattening could walk
 * through: a named model with no explicit `flatten.into = "never"`. A dictionary is
 * an open map with no fixed members, so it never qualifies.
 */
function referencedModel(pass: PassContext, property: IRProperty): IRModel | undefined {
  if (property.typeKind !== "object" || property.type.kind !== "model") return undefined;
  if (property.rendering?.flatten?.into === "never") return undefined;
  return pass.index.model(property.type.id);
}

/** True when `noInline` keeps `property` nested. A `properties` envelope is exempt. */
function keptNested(pass: PassContext, property: IRProperty, target: IRModel): boolean {
  return !isEnvelope(property) && isNoInline(pass, target);
}

/** The model `property` flattens through, once the configured breaks are applied. */
function inlineTarget(pass: PassContext, property: IRProperty): IRModel | undefined {
  const target = referencedModel(pass, property);
  return target === undefined || keptNested(pass, property, target) ? undefined : target;
}

// ─── Sub-pass: check noInline ───────────────────────────────────────────

/** Warn about each `noInline` entry that keeps no property nested. */
function checkNoInline(pass: PassContext): void {
  const effective = new Set<string>();
  for (const model of pass.ir.models) {
    for (const property of model.properties) {
      const target = referencedModel(pass, property);
      if (target === undefined || !keptNested(pass, property, target)) continue;
      effective.add(target.name);
      effective.add(serializeId(target.id));
    }
  }
  for (const name of pass.options.noInline) {
    if (effective.has(name)) continue;
    pass.report(
      "warning",
      "no-inline-unused",
      `'noInline' entry '${name}' has no effect: no property refers to that model outside ` +
        `a '${ENVELOPE}' envelope, which always flattens.`,
    );
  }
}

// ─── Sub-pass: check cycles ─────────────────────────────────────────────

/**
 * Fail on every circular reference flattening would walk. Breaking one is a
 * decision the developer makes with `noInline`; picking whichever reference the
 * traversal happens to meet last would make the surface depend on model order.
 */
function checkCycles(pass: PassContext): boolean {
  const states = new Map<string, "inprogress" | "done">();
  const models: IRModel[] = [];
  // edges[i] leads from models[i] to models[i + 1].
  const edges: IRProperty[] = [];
  let acyclic = true;

  const visit = (model: IRModel): void => {
    const key = idKey(model.id);
    states.set(key, "inprogress");
    models.push(model);

    for (const property of model.properties) {
      const target = inlineTarget(pass, property);
      if (target === undefined) continue;
      const state = states.get(idKey(target.id));
      if (state === "done") continue;
      if (state === "inprogress") {
        const start = models.findIndex((candidate) => idKey(candidate.id) === idKey(target.id));
        reportCycle(pass, models.slice(start), [...edges.slice(start), property]);
        acyclic = false;
        continue;
      }
      edges.push(property);
      visit(target);
      edges.pop();
    }

    models.pop();
    states.set(key, "done");
  };

  for (const model of pass.ir.models) {
    if (!states.has(idKey(model.id))) visit(model);
  }
  return acyclic;
}

/** `edges[i]` leads from `models[i]` to the next model, wrapping back to the first. */
function reportCycle(pass: PassContext, models: readonly IRModel[], edges: readonly IRProperty[]): void {
  const path = [...models.map((model, i) => `${model.name}.${edges[i]!.name}`), models[0]!.name].join(" -> ");
  const candidates = [
    ...new Set(
      edges.flatMap((edge, i) => (isEnvelope(edge) ? [] : [models[(i + 1) % models.length]!.name])),
    ),
  ];
  const fix =
    candidates.length > 0
      ? `Add one of these models to the 'noInline' option to keep references to it nested: ` +
        `${candidates.join(", ")}.`
      : `Every reference in it is a '${ENVELOPE}' envelope, which always flattens, so 'noInline' ` +
        `cannot break it; mark one of them 'flatten.into = never'.`;
  pass.report("error", "circular-reference", `Circular model reference: ${path}. ${fix}`);
}

// ─── Sub-pass: flatten models ───────────────────────────────────────────

type InlineState = "visited" | "inprogress";

function flattenModels(pass: PassContext): void {
  const states = new Map<string, InlineState>();
  const members = new Map<string, MemberNode[]>();

  for (const model of pass.ir.models) {
    resolveModel(pass, model, states, members);
  }

  for (const model of pass.ir.models) {
    const memberNodes = members.get(idKey(model.id));
    if (memberNodes === undefined || memberNodes.length === 0) continue;
    model.rendering = { ...model.rendering, flattened: memberNodes.map(toFlattenedMember) };
  }
}

/**
 * Flatten `model` (memoized) by hoisting the members of each model it refers to,
 * except where `noInline` keeps that reference nested.
 *
 * `check-cycles` has already rejected every circular reference this walks, so
 * re-entering a model that is still `inprogress` is a bug, not a spec problem.
 */
function resolveModel(
  pass: PassContext,
  model: IRModel,
  states: Map<string, InlineState>,
  members: Map<string, MemberNode[]>,
): void {
  const key = idKey(model.id);
  const state = states.get(key);
  if (state === "visited") return;
  if (state === "inprogress") {
    throw new Error(
      `${STEP_NAME}: reached '${serializeId(model.id)}' again while flattening it, after the cycle check passed.`,
    );
  }

  states.set(key, "inprogress");
  const memberNodes: MemberNode[] = [];

  for (const property of model.properties) {
    // Only object-typed properties carry a `flatten` hint, and only a named
    // model can be hoisted — a dictionary is an open map with no fixed members.
    if (property.typeKind !== "object" || property.type.kind !== "model") continue;
    if (property.rendering?.flatten?.into === "never") continue;
    const target = pass.index.model(property.type.id);
    if (target === undefined) {
      pass.report(
        "warning",
        "unresolved-model",
        `Property '${model.name}.${property.name}' references model ` +
          `${serializeId(property.type.id)}, which is not in IR.models. Left un-flattened.`,
      );
      continue;
    }
    if (keptNested(pass, property, target)) continue;
    resolveModel(pass, target, states, members);

    const childMembers = members.get(idKey(target.id)) ?? [];
    const childCount = renderableCount(target, childMembers);
    if (childCount === 0) continue;

    const transparent = TRANSPARENT_CONTAINERS.has(property.wireName.toLowerCase());
    if (!transparent && childCount >= pass.options.threshold) continue;

    // Committed: the container renders through its hoisted members, not as a
    // member of its own.
    property.rendering = { ...property.rendering, flatten: { into: "self" } };
    const prefix = transparent ? [] : deconstruct(property.name);

    for (const child of target.properties) {
      // Already hoisted one level down; it arrives via `childMembers` instead.
      if (isHoisted(child)) continue;
      memberNodes.push({
        name: prefixed(prefix, child.name),
        property: child.id,
        via: [property.id],
        optional: property.optional || child.optional,
        visibility: intersectVisibility(property.visibility, child.visibility),
        components: dedupeSequential([property.name, child.name]),
        leafTypeName: target.name,
      });
    }

    for (const nested of childMembers) {
      // A member hoisted deeper keeps the name it already earned — re-prefixing
      // at every level would produce `AutoScalePropertiesScaleUpProperties…`.
      memberNodes.push({
        name: nested.name,
        property: nested.property,
        via: [property.id, ...nested.via],
        optional: property.optional || nested.optional,
        visibility: intersectVisibility(property.visibility, nested.visibility),
        components: dedupeSequential([property.name, ...nested.components]),
        leafTypeName: nested.leafTypeName,
      });
    }
  }

  resolveMemberCollisions(model, memberNodes);
  states.set(key, "visited");
  members.set(key, memberNodes);
}

/**
 * True when a property was hoisted into its container, so it no longer renders
 * as a member of its own. Only object-typed properties can carry the hint.
 */
function isHoisted(property: IRProperty): property is IRPropertyObject {
  return property.typeKind === "object" && property.rendering?.flatten?.into === "self";
}

/** Members a model renders: its own un-hoisted properties plus what it hoisted. */
function renderableCount(model: IRModel, members: readonly MemberNode[]): number {
  const own = model.properties.filter((property) => !isHoisted(property)).length;
  return own + members.length;
}

function toFlattenedMember(member: MemberNode): FlattenedMember {
  return {
    name: member.name,
    property: member.property,
    via: member.via,
    optional: member.optional,
    ...(member.visibility !== undefined ? { visibility: member.visibility } : {}),
  };
}

// ─── Sub-pass: project the request body onto parameters ─────────────────

/** One candidate on a body model's renderable surface. */
interface SurfaceMember extends IRPropertyRef {
  name: string;
  optional: boolean;
  visibility: IRVisibility[] | undefined;
  access: "public" | "internal" | undefined;
}

function projectBodyParameters(pass: PassContext): void {
  for (const operation of pass.ir.operations) {
    const body = operation.requestBody;
    if (body === undefined) continue;
    // A multipart body's shape lives in `multipart.parts`, not in a flat
    // property surface, so it is not projected here.
    if (body.multipart !== undefined) continue;
    if (body.type === undefined || body.type.kind !== "model") continue;
    if (body.rendering?.flatten?.into === "never") continue;

    const model = pass.index.model(body.type.id);
    if (model === undefined) {
      pass.report(
        "warning",
        "unresolved-model",
        `Operation '${operation.name}' has a body typed ${serializeId(body.type.id)}, ` +
          "which is not in IR.models. Body left unexpanded.",
      );
      continue;
    }

    // Operation parameters were named first and keep their names, exactly as
    // AutoRest's `resolveParameterNames` gives them first crack.
    const used = new Set<string>([
      ...operation.pathParameters.map((p) => p.name),
      ...operation.queryParameters.map((p) => p.name),
      ...operation.headerParameters.map((p) => p.name),
    ]);

    const phase = lifecyclePhaseFor(operation.method);
    const parameters: FlattenedParameter[] = [];
    for (const member of bodySurfaceOf(model)) {
      if (!isWritable(member, phase)) continue;
      const name = selectName(member.name, nameOptionsForRef(pass, member), used);
      used.add(name);
      parameters.push({ name, property: member.property, via: member.via, required: !member.optional });
    }

    if (parameters.length === 0) continue;
    body.rendering = { ...body.rendering, flatten: { into: "self" }, parameters };
  }
}

/** A body model's renderable surface: un-hoisted properties + hoisted members. */
function bodySurfaceOf(model: IRModel): SurfaceMember[] {
  const surface: SurfaceMember[] = [];
  for (const property of model.properties) {
    if (isHoisted(property)) continue;
    surface.push({
      name: property.name,
      property: property.id,
      via: [],
      optional: property.optional,
      visibility: property.visibility,
      access: property.access,
    });
  }
  for (const member of model.rendering?.flattened ?? []) {
    surface.push({
      name: member.name,
      property: member.property,
      via: member.via,
      optional: member.optional,
      visibility: member.visibility,
      // Flattened members carry no access of their own; a hop's `access` was
      // already honoured when the member was hoisted.
      access: undefined,
    });
  }
  return surface;
}

/** The lifecycle phase an operation writes in, if it writes one at all. */
function lifecyclePhaseFor(method: HttpMethod): IRVisibility | undefined {
  switch (method) {
    case "PATCH":
      return "update";
    case "PUT":
    case "POST":
      return "create";
    default:
      return undefined;
  }
}

function isWritable(member: SurfaceMember, phase: IRVisibility | undefined): boolean {
  if (member.access === "internal") return false;
  const visibility = member.visibility;
  if (visibility === undefined) return true; // unconstrained: visible in every phase
  if (visibility.length === 0) return false; // reachable in no phase at all
  if (phase !== undefined) return visibility.includes(phase);
  // No write phase (e.g. a body on GET): keep everything except read-only.
  return !(visibility.length === 1 && visibility[0] === "read");
}

// ─── Naming ─────────────────────────────────────────────────────────────

/**
 * Progressively more-qualified alternatives for a colliding member: the leaf
 * name, then one ancestor, then two, ... then the fully qualified form.
 */
function nameOptions(components: readonly string[], leafTypeName?: string): string[] {
  const options: string[] = [];
  for (let take = 1; take <= components.length; take++) {
    options.push(pascalCase(dedupeSequential(components.slice(components.length - take))));
  }
  const leaf = components[components.length - 1];
  if (leafTypeName !== undefined && leaf !== undefined) {
    options.push(pascalCase(dedupeSequential([leafTypeName, leaf])));
  }
  return [...new Set(options)];
}

/** Rebuild name alternatives for an already-published member from the hops it references. */
function nameOptionsForRef(pass: PassContext, ref: IRPropertyRef): string[] {
  return nameOptions([...ref.via, ref.property].map((id) => pass.index.property(id)?.name ?? id.name));
}

/** First unused candidate, else `<preferred>2`, `<preferred>3`, ... */
function selectName(
  preferred: string,
  options: readonly string[],
  used: ReadonlySet<string>,
): string {
  if (!used.has(preferred)) return preferred;
  for (const option of options) {
    if (!used.has(option)) return option;
  }
  for (let suffix = 2; ; suffix++) {
    const candidate = `${preferred}${suffix}`;
    if (!used.has(candidate)) return candidate;
  }
}

/**
 * Resolve collisions within one model. Real (un-hoisted) properties always win;
 * hoisted members with the fewest alternatives pick first, so a member that can
 * still qualify itself further yields to one that cannot.
 */
function resolveMemberCollisions(model: IRModel, members: MemberNode[]): void {
  const used = new Set<string>();
  for (const property of model.properties) {
    if (!isHoisted(property)) used.add(property.name);
  }
  const options = new Map<MemberNode, string[]>(
    members.map((member) => [member, nameOptions(member.components, member.leafTypeName)]),
  );
  const ordered = [...members].sort(
    (a, b) => (options.get(a)?.length ?? 0) - (options.get(b)?.length ?? 0),
  );
  for (const member of ordered) {
    member.name = selectName(member.name, options.get(member) ?? [], used);
    used.add(member.name);
  }
}

/** Prefix an existing member name, preserving its own casing. */
function prefixed(prefix: readonly string[], name: string): string {
  if (prefix.length === 0) return name;
  return pascalCase(prefix) + capitalize(name);
}

/** Split an identifier into lowercase words on camel/Pascal and separator boundaries. */
function deconstruct(identifier: string): string[] {
  return identifier
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2")
    .split(/[^a-zA-Z0-9]+/)
    .filter((word) => word.length > 0)
    .map((word) => word.toLowerCase());
}

function pascalCase(words: readonly string[]): string {
  return words.flatMap(deconstruct).map(capitalize).join("");
}

function capitalize(word: string): string {
  return word.length === 0 ? word : word[0]!.toUpperCase() + word.slice(1);
}

/** Drop adjacent duplicates so `properties.properties.name` reads as `properties.name`. */
function dedupeSequential(words: readonly string[]): string[] {
  return words.filter(
    (word, index) => index === 0 || word.toLowerCase() !== words[index - 1]?.toLowerCase(),
  );
}

// ─── Visibility ─────────────────────────────────────────────────────────

/**
 * Intersect lifecycle phases along a hop. `undefined` means unconstrained, so it
 * is the identity here — and a result covering every phase normalizes back to
 * `undefined` rather than spelling out the full set.
 */
function intersectVisibility(
  outer: IRVisibility[] | undefined,
  inner: IRVisibility[] | undefined,
): IRVisibility[] | undefined {
  if (outer === undefined && inner === undefined) return undefined;
  const a = outer ?? ALL_PHASES;
  const b = inner ?? ALL_PHASES;
  const result = ALL_PHASES.filter((phase) => a.includes(phase) && b.includes(phase));
  return result.length === ALL_PHASES.length ? undefined : result;
}
