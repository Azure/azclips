/**
 * Two ordered sub-passes over one in-memory IR (deliberately one registered
 * step, not two: the passes share scratch state that has no business in the
 * cross-language IR contract, and a body projection without its model
 * flattening is not a valid configuration):
 *
 *   1. `flatten-models`      — hoist nested object members onto their containing
 *                              model as `ModelHints.flattened`.
 *   2. `project-body-params` — project a request body onto the operation's
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
import {
  idKey,
  indexIR,
  serializeId,
  type FlattenedMember,
  type FlattenedParameter,
  type HttpMethod,
  type IR,
  type IRId,
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

// ─── Options ────────────────────────────────────────────────────────────

export interface FlattenModelOptions {
  /**
   * A nested model is flattened into its container only while it renders fewer
   * members than this. The count is cumulative — the model's own properties that
   * were not themselves flattened, plus every member flattened into it — so a
   * model can stay nested because of its descendants. `properties` and `error`
   * containers are exempt, and spec decorators such as `@flattenProperty` do not
   * bypass it. Defaults to `DEFAULT_INLINING_THRESHOLD` (100).
   */
  inliningThreshold?: number;
  /**
   * Model names (or `serializeId` forms) that must never be flattened into their containing model.
   */
  noInline?: string[];
}

interface ResolvedOptions {
  threshold: number;
  noInline: ReadonlySet<string>;
}

/** Options arrive from JSON config, so validate rather than trust the types. */
function resolveOptions(raw: Record<string, unknown>): ResolvedOptions {
  const threshold = raw["inliningThreshold"];
  const noInline = raw["noInline"];
  return {
    threshold:
      typeof threshold === "number" && Number.isFinite(threshold) && threshold >= 0
        ? threshold
        : DEFAULT_INLINING_THRESHOLD,
    noInline: new Set(
      Array.isArray(noInline) ? noInline.filter((v): v is string => typeof v === "string") : [],
    ),
  };
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
  run(pass: PassContext): void;
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

export function flattenModelStep(options: Record<string, unknown> = {}): TransformStep {
  const option = resolveOptions(options);
  return {
    name: STEP_NAME,
    run(ir: IR, context: TransformContext): IR {
      // Written in place: the catalog declares this stage `additive`, so the
      // orchestrator has already handed us an IR we own, and no sibling branch
      // can observe a value we change. A defensive deep copy here would double
      // peak memory on a large spec for nothing.
      const pass: PassContext = {
        ir,
        options: option,
        report: (severity, code, message) =>
          context.reportDiagnostic({ severity, code: `${DIAGNOSTIC_PREFIX}/${code}`, message }),
        index: indexIR(ir),
      };      for (const subPass of SUB_PASSES) subPass.run(pass);
      return ir;
    },
  };
}

/** Ordered sub-passes. The order is data: pass 2 reads what pass 1 wrote. */
const SUB_PASSES: readonly SubPass[] = [
  { name: "flatten-models", run: flattenModels },
  { name: "project-body-params", run: projectBodyParameters },
];

// ─── Sub-pass 1: flatten models ─────────────────────────────────────────

type InlineState = "visited" | "unvisited" | "inprogress";

function flattenModels(pass: PassContext): void {
  const states = new Map<string, InlineState>();
  const members = new Map<string, MemberNode[]>();

  for (const model of pass.ir.models) {
    resolveModel(pass, model, [], states, members);
  }

  for (const model of pass.ir.models) {
    const memberNodes = members.get(idKey(model.id));
    if (memberNodes === undefined || memberNodes.length === 0) continue;
    model.rendering = { ...model.rendering, flattened: memberNodes.map(toFlattenedMember) };
  }
}

/**
 * Flatten `model` (memoized), returning whether a *container* may inline it.
 *
 * Re-entering a model that is
 * still `inprogress` is a cycle: the back edge is refused so recursion
 * terminates, while the model itself still completes normally for its other
 * containers.
 */
function resolveModel(
  pass: PassContext,
  model: IRModel,
  stack: readonly IRId[],
  states: Map<string, InlineState>,
  members: Map<string, MemberNode[]>,
): boolean {
  const key = idKey(model.id);
  const state = states.get(key);
  if (state === "visited") return true;
  if (state === "unvisited") return false;
  if (state === "inprogress") {
    pass.report(
      "warning",
      "circular-reference",
      `Circular model reference while flattening: ${[...stack, model.id]
        .map(serializeId)
        .join(" -> ")}. The back edge into '${model.name}' is left un-flattened. ` +
        "Set `noInline` on one of these models to choose a different break point.",
    );
    return false;
  }
  // `noInline` is user-facing config, so it matches the display form, not the key.
  if (pass.options.noInline.has(model.name) || pass.options.noInline.has(serializeId(model.id))) {
    states.set(key, "unvisited");
    return false;
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
    if (!resolveModel(pass, target, [...stack, model.id], states, members)) continue;

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
  return true;
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

// ─── Sub-pass 2: project the request body onto parameters ───────────────

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
