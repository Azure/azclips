/**
 * Project operations into commands — the node every client transform and the
 * generator actually consume.
 *
 * An `IROperation` is wire truth. An `IRCommand` is a processor: one flat parameter
 * surface, a declarative request, a projected response, and the placement the
 * generated project needs. Every decision the generator would otherwise infer is
 * made here, once, and written down:
 *
 *   - the **parameter surface** — today a union of path/query/header/body computed
 *     inside the C# generator, and the annotation target client transforms need;
 *   - the **request path**, as ordered segments rather than a template the generator
 *     string-replaces `{wireName}` in;
 *   - **pagination paths**, resolved to property ids rather than wire segments the
 *     generator PascalCases and hopes match an emitted member;
 *   - the **surface types** the processor's generics are built from.
 *
 * Nothing is removed: `IR.operations` is carried through untouched and every step
 * points back at the operation it projects, so a captured document can always be
 * traced to wire truth.
 *
 * The step returns a `TransformedIR` — `IR` plus `commands` and `target` — and says
 * so in its type, so a client transform that follows can read `ir.commands` without
 * a cast. Two things enforce that at runtime: the catalog's
 * `requires: ["create-command"]` on the generate stage makes skipping this step a
 * config error, and `parseTransformedIR` at the generate boundary rejects a document
 * this step never ran on.
 *
 * ## What this step does not do
 *
 * **No client dialects.** It knows nothing about PowerShell or the CLI; `dialect` is
 * left absent for the client transforms that run after it.
 *
 * **Only base surfaces beyond the `by` split.** `id.variant` is always populated,
 * and is inferred where an operation name carries a `by` qualifier
 * (`listByResourceGroup` is `List` in its `ByResourceGroup` surface). Everything
 * else names its variant after the action. Surfaces that come from *parameters*
 * rather than the name — a resource-id form beside a path-parameter form — are a
 * later transform that adds commands sharing an `id.action`.
 *
 * **Only the `atomic` template.** Read-before-write (`getPut`) needs operation
 * pairing that belongs in its own pass, so every command here has exactly one step.
 */

import type { TransformContext, TransformStep } from "@azure-client-engine/compiler";
import {
  commandKey,
  indexIR,
  serializeId,
  type IR,
  type IRCommand,
  type IRCommandId,
  type IRCommandPagination,
  type IRCommandParameter,
  type IRCommandResponse,
  type IRCommandStep,
  type IRCommandUriSegment,
  type IRCommandWireEntry,
  type IRIndex,
  type IRId,
  type IRModel,
  type IROperation,
  type IRPagingProperty,
  type IRParameter,
  type IRPropertyRef,
  type IRTarget,
  type IRTypeRef,
  type TransformedIR,
} from "@azure-client-engine/ir";

const STEP_NAME = "create-command";
const DIAGNOSTIC_PREFIX = `@azure-client-engine/transformers/${STEP_NAME}`;

/** Fallback when a document declares no source to take a namespace from. */
const DEFAULT_ROOT_NAMESPACE = ["Generated"];

const DEFAULT_DIRECTORIES = { models: "Models", processors: "Processors" } as const;

/**
 * Headers the HTTP protocol owns, not the caller.
 *
 * TCGC synthesizes `Content-Type` and `Accept` as ordinary required header
 * parameters, so they arrive indistinguishable from a spec-declared header like
 * `If-Match`. Surfacing them would put `--accept` on every command; dropping them
 * would lose data the request needs. They are carried as parameters and marked
 * `internal`, so the transport supplies them and no client renders them.
 *
 * Matched on wire name because the IR records no provenance for synthesized
 * parameters — the one thing that would make this exact rather than conventional.
 */
const PROTOCOL_HEADERS = new Set(["content-type", "accept"]);

// ─── Options ────────────────────────────────────────────────────────────

export interface CreateCommandOptions {
  /**
   * Root namespace of the generated project, dotted or segmented. Defaults to the
   * primary source's `rootNamespace`, which is the spec's own namespace.
   */
  rootNamespace?: string | string[];
  /** Generated project (and assembly) name. Defaults to the root namespace, dotted. */
  projectName?: string;
  /** Relative output directories. Defaults to `Models` / `Processors`. */
  directories?: { models?: string; processors?: string };
}

interface ResolvedOptions {
  rootNamespace: string[] | undefined;
  projectName: string | undefined;
  directories: { models: string; processors: string };
}

/** Options arrive from JSON config, so validate rather than trust the types. */
function resolveOptions(raw: Record<string, unknown>): ResolvedOptions {
  const rootNamespace = raw["rootNamespace"];
  const projectName = raw["projectName"];
  const directories = isRecord(raw["directories"]) ? raw["directories"] : {};

  return {
    rootNamespace: segments(rootNamespace),
    projectName: typeof projectName === "string" && projectName.length > 0 ? projectName : undefined,
    directories: {
      models: stringOr(directories["models"], DEFAULT_DIRECTORIES.models),
      processors: stringOr(directories["processors"], DEFAULT_DIRECTORIES.processors),
    },
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function stringOr(value: unknown, fallback: string): string {
  return typeof value === "string" && value.length > 0 ? value : fallback;
}

function segments(value: unknown): string[] | undefined {
  if (typeof value === "string" && value.length > 0) {
    return value.split(".").filter((segment) => segment.length > 0);
  }
  if (Array.isArray(value)) {
    const parts = value.filter((part): part is string => typeof part === "string" && part.length > 0);
    return parts.length > 0 ? parts : undefined;
  }
  return undefined;
}

// ─── Pass plumbing ──────────────────────────────────────────────────────

type Report = (severity: "error" | "warning", code: string, message: string) => void;

interface PassContext {
  readonly ir: IR;
  readonly options: ResolvedOptions;
  readonly report: Report;
  readonly index: IRIndex;
  /** Command names already taken, so a collision resolves instead of colliding. */
  readonly usedNames: Set<string>;
  /**
   * Command identities already taken, mapped to the operation that claimed each.
   * Keeping the claimant (not just the key) is what lets a clash name *both* sides:
   * the incoming operation alone is rarely enough to see why they collided.
   */
  readonly usedIdentities: Map<string, IROperation>;
}

export function createCommandStep(
  options: Record<string, unknown> = {},
): TransformStep<IR, TransformedIR> {
  const resolvedOptions = resolveOptions(options);
  return {
    name: STEP_NAME,
    run(ir: IR, context: TransformContext): TransformedIR {
      const pass: PassContext = {
        ir,
        options: resolvedOptions,
        report: (severity, code, message) =>
          context.reportDiagnostic({ severity, code: `${DIAGNOSTIC_PREFIX}/${code}`, message }),
        index: indexIR(ir),
        usedNames: new Set<string>(),
        usedIdentities: new Map<string, IROperation>(),
      };

      // Grouping is scratch, not shape: commands land flat, and downstream regroups
      // by reading `id.subject` / `id.action` rather than relying on array order.
      const commands: IRCommand[] = [];
      for (const [, operations] of groupOperations(pass)) {
        for (const operation of operations) {
          const command = buildCommand(pass, operation);
          if (command !== undefined) commands.push(command);
        }
      }

      const transformed: TransformedIR = {
        ...ir,
        commands,
        target: buildTarget(pass),
      };
      return transformed;
    },
  };
}

// ─── Grouping ───────────────────────────────────────────────────────────

/**
 * Bucket operations by `(server, subject)`.
 *
 * The server level is first because a server decides which generated base a command
 * derives from; the subject level scopes naming. Both are read back off
 * `IRCommandId` downstream — this map exists only so names resolve against the right
 * neighbours.
 */
function groupOperations(pass: PassContext): Map<string, IROperation[]> {
  const buckets = new Map<string, IROperation[]>();
  for (const operation of pass.ir.operations) {
    const key = `${serializeId(operation.server)}\u0000${subjectOf(operation)}`;
    const bucket = buckets.get(key);
    if (bucket === undefined) buckets.set(key, [operation]);
    else bucket.push(operation);
  }
  return buckets;
}

/**
 * The subject prefix of an operation's declared name, or `""` when the name is
 * exactly the action.
 *
 * A TypeSpec interface is not reliably the operation group. Brownfield specs use an
 * interface as a *file* container and put several resources in it, marking the real
 * group with `@tag` — which the IR does not carry, and which is free-form
 * documentation text anyway (inconsistently cased, sometimes not an identifier).
 * What survives reliably is the declared name: TCGC renders swagger's
 * `ExpressRouteCrossConnectionPeerings_Get` as
 * `expressRouteCrossConnectionPeeringsGet` while the client name stays `Get`.
 *
 * Compared word-wise, not by string suffix, so an action that happens to end another
 * word is not sliced out of it (`blocklist` does not yield subject `Block` for
 * action `List`).
 */
function subjectPrefixOf(operation: IROperation): string {
  const actionWords = deconstruct(operation.name);
  const nameWords = deconstruct(operation.id.name);
  const offset = nameWords.length - actionWords.length;
  if (actionWords.length === 0 || offset <= 0) return "";

  for (const [index, word] of actionWords.entries()) {
    if (nameWords[offset + index]?.toLowerCase() !== word.toLowerCase()) return "";
  }
  return nameWords.slice(0, offset).map(capitalize).join("");
}

/**
 * What a command acts on: the group the spec explicitly placed the operation in,
 * else its subject prefix, else the declaring interface, else the last namespace
 * segment for a namespace-level operation.
 *
 * `@clientLocation` wins because it is the author stating the grouping outright,
 * while the prefix merely infers it. Measured across 1,161 operations in three
 * specs the two never disagree, and the decorator covers 228 operations the prefix
 * cannot see — so preferring the inference would only ever discard a better answer.
 *
 * The prefix is kept beneath it for a spec that renames without relocating: it fires
 * nowhere in that sample, but costs nothing when it does not.
 *
 * Deliberately not written back onto `IROperation.id.interface`: that field asserts
 * *a TypeSpec interface declared this*, so synthesizing one would be a lie. A command
 * has no declaration site, so its own `subject` can be derived freely.
 */
function subjectOf(operation: IROperation): string {
  const located = operation.clientLocation;
  if (located !== undefined && located.length > 0) return pascal(located);
  const prefix = subjectPrefixOf(operation);
  if (prefix.length > 0) return prefix;
  const declared = operation.id.interface;
  if (declared !== undefined && declared.length > 0) return pascal(declared);
  const tail = operation.id.namespace.at(-1);
  return tail !== undefined && tail.length > 0 ? pascal(tail) : "Operations";
}

/**
 * Split an operation name into `(action, variant)` on a `by` qualifier:
 * `listByResourceGroup` is the `List` action in its `ByResourceGroup` surface.
 *
 * This is the one place a variant is *inferred* rather than assigned. It is exactly
 * the variant axis: `List` and `ListByResourceGroup` are the same action differing
 * only in what the caller must supply (`ListByResourceGroup` requires a resource
 * group; `List` does not), which is what makes them siblings rather than two
 * unrelated commands.
 *
 * Scoped to `by` alone. AutoRest splits on `with/at/by/for/in/of` and then needs a
 * `specialWords` table to stop `signIn` becoming `Sign` + `""`; across 776 Network
 * and 30 ElasticSan operations only `by` ever fires, so the wider set buys nothing
 * and costs that hazard. A leading `by` is not a qualifier, so index 0 never splits.
 */
function splitAction(name: string): { action: string; variant: string } {
  const words = deconstruct(name);
  const at = words.findIndex((word, index) => index > 0 && word.toLowerCase() === "by");
  if (at < 0 || at === words.length - 1) {
    return { action: pascal(name), variant: "" };
  }
  return {
    action: words.slice(0, at).map(capitalize).join(""),
    variant: words.slice(at).map(capitalize).join(""),
  };
}

// ─── Target ─────────────────────────────────────────────────────────────

/**
 * Resolved placement for the generated project.
 *
 * Config wins; otherwise the primary source's own root namespace is used, so a
 * document generates somewhere sensible before generation-time configuration exists.
 */
function buildTarget(pass: PassContext): IRTarget {
  const source = pass.ir.sources[0];
  const rootNamespace =
    pass.options.rootNamespace ??
    (source !== undefined && source.rootNamespace.length > 0
      ? source.rootNamespace
      : DEFAULT_ROOT_NAMESPACE);

  return {
    rootNamespace,
    projectName: pass.options.projectName ?? rootNamespace.join("."),
    directories: pass.options.directories,
  };
}

// ─── One command ────────────────────────────────────────────────────────

function buildCommand(pass: PassContext, operation: IROperation): IRCommand | undefined {
  if (operation.name.length === 0) {
    pass.report("warning", "empty-operation-name", "Skipped an operation with an empty name.");
    return undefined;
  }

  const subject = subjectOf(operation);
  const { action, variant } = splitAction(operation.name);
  const id: IRCommandId = {
    module: operation.source,
    subject,
    action,
    // Always populated, so consumers never branch on an absent variant. An operation
    // with no `by` qualifier is its action's only surface, and names that surface
    // after the action itself.
    variant: variant.length > 0 ? variant : action,
  };

  // Identity is `(module, subject, action, variant)`. `subject` is derived from the
  // operation's own name, so sibling resources under one interface no longer
  // collapse — what remains is a genuine clash (same subject, action, and surface).
  // Reported rather than emitted: a second command under one identity is an invalid
  // document, and inventing a distinguishing name here would silently rename a
  // user-facing command.
  const identity = commandKey(id);
  const claimant = pass.usedIdentities.get(identity);
  if (claimant !== undefined) {
    pass.report("error", "duplicate-command-identity", duplicateIdentityMessage(identity, claimant, operation));
    return undefined;
  }
  pass.usedIdentities.set(identity, operation);

  const parameters = buildParameters(pass, operation);
  const request = buildRequest(pass, operation, parameters);
  const responses = operation.responses.map(toCommandResponse);
  const pagination = buildPagination(pass, operation);

  const step: IRCommandStep = {
    operation: operation.id,
    request,
    // A step mirrors its operation, which must declare at least one response.
    responses: responses.length > 0 ? responses : [{ statusCodes: [{ kind: "default" }], isError: false }],
    ...(operation.semantics?.longRunning !== undefined
      ? { longRunning: operation.semantics.longRunning }
      : {}),
    ...(pagination !== undefined ? { pagination } : {}),
  };

  const success = responses.find((response) => !response.isError && response.type !== undefined);

  return {
    id,
    name: uniqueName(pass, commandName(id)),
    template: "atomic",
    server: operation.server,
    steps: [step],
    parameters,
    surface: {
      ...(request.body?.type !== undefined ? { request: request.body.type } : {}),
      ...(success?.type !== undefined ? { response: success.type } : {}),
      // A paged command writes items, not the envelope that carries them.
      ...(pagination !== undefined ? { output: pagination.itemType } : {}),
      paginated: pagination !== undefined,
    },
    ...(operation.access !== undefined ? { access: operation.access } : {}),
    ...(operation.docs !== undefined ? { docs: operation.docs } : {}),
    ...(operation.summary !== undefined ? { summary: operation.summary } : {}),
  };
}

/**
 * The generated C# type stem: action, then the surface qualifier, then subject —
 * `ListByResourceGroupElasticSans`. The variant sits in the middle because it
 * qualifies the action, not the subject.
 *
 * Concatenation only. AutoRest additionally collapses a subject its qualifier
 * already named (`ListByElasticSan` over `ElasticSans`) by singularizing every word
 * through an English pluralization service — but that is a PowerShell cmdlet-naming
 * rule, and a client dialect's business, not this step's. Measured across 1,105
 * operations, every such collapse depended on the singularization: a strict
 * comparison never fires once. So there is no non-linguistic rule to keep, and a
 * doubled word is preferred over guessing at English here.
 *
 * C# naming is still applied — `uniqueName` runs the stem through `identifier`, so
 * the result is PascalCase, legal, and unique document-wide.
 */
function commandName(id: IRCommandId): string {
  const variant = id.variant !== undefined && id.variant !== id.action ? id.variant : "";
  return `${id.action}${variant}${id.subject}`;
}

/**
 * Report a clash by naming **both** operations, each by declared `IRId` *and* client
 * name.
 *
 * Both halves are needed to see the cause. Identity is derived from the client name
 * (`operation.name`), but an `IRId` records the declared one, so the two are
 * routinely different — a `@clientName` rename is the usual reason two operations
 * collide at all, and printing only the id hides the very rename that caused it.
 * Printing only the incoming operation hides the other half entirely.
 *
 * The path is included because when the names have been flattened together it is
 * often the only thing left that tells a reader these are different resources.
 */
function duplicateIdentityMessage(
  identity: string,
  claimant: IROperation,
  incoming: IROperation,
): string {
  const describe = (operation: IROperation): string =>
    `${serializeId(operation.id)} (client name '${operation.name}', ${operation.method} ${operation.pathTemplate})`;

  return (
    `Command identity ${identity} is derived by two operations:\n` +
    `  1. ${describe(claimant)}\n` +
    `  2. ${describe(incoming)}\n` +
    "Only the first is emitted. Identity comes from the client name and the subject " +
    "derived from the declared name, so two operations collide when a rename leaves " +
    "them sharing both. Disambiguate with @clientName, or give one an explicit subject."
  );
}

/**
 * `name` is the generated C# type stem, so it must be unique document-wide. A
 * collision qualifies with a counter rather than silently emitting two types under
 * one name.
 */
function uniqueName(pass: PassContext, preferred: string): string {
  let name = identifier(preferred);
  for (let suffix = 2; pass.usedNames.has(name); suffix++) {
    name = `${identifier(preferred)}${suffix}`;
  }
  pass.usedNames.add(name);
  return name;
}

// ─── Parameters ─────────────────────────────────────────────────────────

/**
 * The whole processor surface as one list: path, query, and header parameters, then
 * the request body — expanded into flat members when `flatten-model` projected it,
 * otherwise as a single payload.
 *
 * Operation parameters are named first and keep their names, so a projected body
 * member yields to them on collision. That ordering is the reason the surface is
 * built here rather than unioned by each consumer.
 */
function buildParameters(pass: PassContext, operation: IROperation): IRCommandParameter[] {
  const parameters: IRCommandParameter[] = [];
  const used = new Set<string>();

  const add = (parameter: IRCommandParameter): void => {
    used.add(parameter.name);
    parameters.push(parameter);
  };

  for (const parameter of operation.pathParameters) add(fromParameter(parameter, "path", used));
  for (const parameter of operation.queryParameters) add(fromParameter(parameter, "query", used));
  for (const parameter of operation.headerParameters) add(fromParameter(parameter, "header", used));

  for (const parameter of bodyParameters(pass, operation, used)) add(parameter);

  return parameters;
}

function fromParameter(
  parameter: IRParameter,
  kind: "path" | "query" | "header",
  used: ReadonlySet<string>,
): IRCommandParameter {
  const protocolHeader = kind === "header" && PROTOCOL_HEADERS.has(parameter.wireName.toLowerCase());
  return {
    name: distinct(identifier(parameter.name), used),
    source: { kind, parameter: parameter.id },
    type: parameter.type,
    // A path parameter is structural: the URI cannot be built without it.
    required: kind === "path" ? true : !parameter.optional,
    nullable: parameter.nullable,
    ...(parameter.defaultValue !== undefined ? { defaultValue: parameter.defaultValue } : {}),
    ...(parameter.collectionFormat !== undefined
      ? { collectionFormat: parameter.collectionFormat }
      : {}),
    ...(parameter.format !== undefined ? { format: parameter.format } : {}),
    ...(protocolHeader ? { access: "internal" as const } : {}),
    ...(parameter.docs !== undefined ? { docs: parameter.docs } : {}),
    ...(parameter.summary !== undefined ? { summary: parameter.summary } : {}),
  };
}

/**
 * Body members, from the projection `flatten-model` resolved.
 *
 * `RequestBodyHints.parameters` already filtered by lifecycle phase and resolved
 * naming, so this only re-homes them onto the command surface. Without that hint the
 * body stays a single payload member and no flat parameters are emitted.
 */
function bodyParameters(
  pass: PassContext,
  operation: IROperation,
  used: ReadonlySet<string>,
): IRCommandParameter[] {
  const body = operation.requestBody;
  const projected = body?.rendering?.parameters;
  if (body === undefined || projected === undefined || projected.length === 0) return [];

  const taken = new Set(used);
  const parameters: IRCommandParameter[] = [];

  for (const parameter of projected) {
    const leaf = pass.index.property(parameter.property);
    if (leaf === undefined) {
      pass.report(
        "warning",
        "unresolved-body-parameter",
        `Operation '${operation.name}' projects body parameter '${parameter.name}', whose property ` +
          "does not resolve to a model property. The parameter is skipped; the body member remains.",
      );
      continue;
    }

    const name = distinct(identifier(parameter.name), taken);
    taken.add(name);
    parameters.push({
      name,
      source: { kind: "body", property: parameter.property, via: parameter.via },
      type: leaf.type,
      required: parameter.required,
      nullable: leaf.nullable,
      ...(leaf.defaultValue !== undefined ? { defaultValue: leaf.defaultValue } : {}),
      ...(leaf.access !== undefined ? { access: leaf.access } : {}),
      ...(leaf.docs !== undefined ? { docs: leaf.docs } : {}),
      ...(leaf.summary !== undefined ? { summary: leaf.summary } : {}),
    });
  }

  return parameters;
}

/** Qualify a name until it is free within the command. */
function distinct(preferred: string, used: ReadonlySet<string>): string {
  if (!used.has(preferred)) return preferred;
  for (let suffix = 2; ; suffix++) {
    const candidate = `${preferred}${suffix}`;
    if (!used.has(candidate)) return candidate;
  }
}

// ─── Request ────────────────────────────────────────────────────────────

function buildRequest(
  pass: PassContext,
  operation: IROperation,
  parameters: readonly IRCommandParameter[],
): IRCommandStep["request"] {
  const byId = new Map<string, string>();
  for (const parameter of parameters) {
    if (parameter.source.kind === "path" || parameter.source.kind === "query" || parameter.source.kind === "header") {
      byId.set(serializeId(parameter.source.parameter), parameter.name);
    }
  }

  const query: IRCommandWireEntry[] = [];
  const headers: IRCommandWireEntry[] = [];

  // The API version is pinned, not supplied: the IR was emitted *at* a version, so
  // the models correspond to it and a caller override would mismatch model and wire.
  const stamp = operation.apiVersion;
  const stampEntry: IRCommandWireEntry = {
    wireName: stamp.parameterName,
    value: { kind: "literal", text: stamp.value },
  };
  if (stamp.location === "query") query.push(stampEntry);
  else headers.push(stampEntry);

  for (const parameter of operation.queryParameters) {
    const name = byId.get(serializeId(parameter.id));
    if (name !== undefined) {
      query.push({ wireName: parameter.wireName, value: { kind: "parameter", parameter: name } });
    }
  }
  for (const parameter of operation.headerParameters) {
    const name = byId.get(serializeId(parameter.id));
    if (name !== undefined) {
      headers.push({ wireName: parameter.wireName, value: { kind: "parameter", parameter: name } });
    }
  }

  const body = operation.requestBody;
  return {
    method: operation.method,
    path: buildPath(pass, operation, parameters),
    query,
    headers,
    ...(body !== undefined
      ? {
          body: {
            ...(body.type !== undefined ? { type: body.type } : {}),
            contentType: body.contentType,
            ...(body.multipart !== undefined ? { multipart: body.multipart } : {}),
            // Flat members cover the body, so the payload can leave the public
            // surface instead of duplicating every one of them.
            projected: parameters.some((parameter) => parameter.source.kind === "body"),
            ...(body.docs !== undefined ? { docs: body.docs } : {}),
            ...(body.summary !== undefined ? { summary: body.summary } : {}),
          },
        }
      : {}),
  };
}

/**
 * Split the path template into literal and parameter segments.
 *
 * Declarative so the generator only spells it: rebuilding the path by replacing
 * `{wireName}` with a C# identifier leaks identifier rules into path handling, and
 * silently leaves the placeholder behind when a parameter is missing.
 */
function buildPath(
  pass: PassContext,
  operation: IROperation,
  parameters: readonly IRCommandParameter[],
): IRCommandUriSegment[] {
  const byWireName = new Map<string, string>();
  for (const [index, parameter] of operation.pathParameters.entries()) {
    const projected = parameters.find(
      (candidate) =>
        candidate.source.kind === "path" &&
        serializeId(candidate.source.parameter) === serializeId(parameter.id),
    );
    byWireName.set(parameter.wireName, projected?.name ?? identifier(parameter.name));
    void index;
  }

  const segments: IRCommandUriSegment[] = [];
  const pattern = /\{([^{}]*)\}/g;
  let cursor = 0;

  for (let match = pattern.exec(operation.pathTemplate); match !== null; match = pattern.exec(operation.pathTemplate)) {
    if (match.index > cursor) {
      segments.push({ kind: "literal", text: operation.pathTemplate.slice(cursor, match.index) });
    }
    const wireName = match[1] ?? "";
    const parameter = byWireName.get(wireName);
    if (parameter === undefined) {
      // An unbound placeholder would otherwise reach the generator as a literal and
      // ship as part of the URI.
      pass.report(
        "warning",
        "unbound-path-placeholder",
        `Operation '${operation.name}' has path placeholder '{${wireName}}' with no matching ` +
          "path parameter. It is emitted literally.",
      );
      segments.push({ kind: "literal", text: match[0] });
    } else {
      segments.push({ kind: "parameter", parameter });
    }
    cursor = match.index + match[0].length;
  }

  if (cursor < operation.pathTemplate.length) {
    segments.push({ kind: "literal", text: operation.pathTemplate.slice(cursor) });
  }
  return segments;
}

// ─── Responses ──────────────────────────────────────────────────────────

/**
 * One response per declaration, error responses included.
 *
 * Every status code is kept rather than collapsing to the first success: which error
 * model to deserialize is a `status -> type` decision the runtime makes, and it needs
 * the whole table.
 */
function toCommandResponse(response: IROperation["responses"][number]): IRCommandResponse {
  return {
    statusCodes: [response.statusCode],
    ...(response.type !== undefined ? { type: response.type } : {}),
    ...(response.contentType !== undefined ? { contentType: response.contentType } : {}),
    ...(response.multipart !== undefined ? { multipart: response.multipart } : {}),
    ...(response.headers !== undefined ? { headers: response.headers } : {}),
    isError: response.isError,
    ...(response.docs !== undefined ? { docs: response.docs } : {}),
  };
}

// ─── Pagination ─────────────────────────────────────────────────────────

/**
 * Resolve declared paging roles into property references.
 *
 * `IRPageableSemantics` records paths as *wire* segments, which leaves the generator
 * PascalCasing each one and hoping it matches an emitted member — explicitly a
 * heuristic, and a fragile one now that flattening renames members. Resolving here
 * against the response model makes the reference exact, the same way a
 * `FlattenedMember`'s is.
 */
function buildPagination(
  pass: PassContext,
  operation: IROperation,
): IRCommandPagination | undefined {
  const pageable = operation.semantics?.pageable;
  if (pageable === undefined) return undefined;

  const envelope = responseModel(pass, operation);
  if (envelope === undefined) {
    pass.report(
      "warning",
      "paging-response-unresolved",
      `Operation '${operation.name}' is pageable, but its success response is not a model. ` +
        "Pagination is omitted; the command renders as a plain call.",
    );
    return undefined;
  }

  const items = resolveWirePath(pass, envelope, pageable.output.pageItems);
  if (items === undefined) {
    pass.report(
      "warning",
      "paging-binding-unresolved",
      `Operation '${operation.name}' declares page items by a path that does not resolve to ` +
        `properties of '${envelope.name}'. Pagination is omitted; its wiring requires customization.`,
    );
    return undefined;
  }

  const nextLink =
    pageable.output.nextLink !== undefined
      ? resolveWirePath(pass, envelope, pageable.output.nextLink)
      : undefined;
  if (pageable.output.nextLink !== undefined && nextLink === undefined) {
    pass.report(
      "warning",
      "paging-binding-unresolved",
      `Operation '${operation.name}' declares a continuation link by a path that does not ` +
        `resolve to properties of '${envelope.name}'. Pagination is omitted.`,
    );
    return undefined;
  }

  return {
    itemType: pageable.itemType,
    items,
    ...(nextLink !== undefined ? { nextLink } : {}),
    nextLinkVerb: pageable.sdk.nextLinkVerb,
  };
}

/** The model behind an operation's first typed success response. */
function responseModel(pass: PassContext, operation: IROperation): IRModel | undefined {
  for (const response of operation.responses) {
    if (response.isError || response.type === undefined) continue;
    const model = modelOf(pass, response.type);
    if (model !== undefined) return model;
  }
  return undefined;
}

function modelOf(pass: PassContext, type: IRTypeRef): IRModel | undefined {
  return type.kind === "model" ? pass.index.model(type.id) : undefined;
}

/**
 * Walk a paging role's response-body wire path to the property it names, through
 * the containers on the way.
 *
 * Only an unambiguous body binding can be resolved: a header or query binding names
 * no model property, and bindings that disagree describe more than one shape.
 */
function resolveWirePath(
  pass: PassContext,
  envelope: IRModel,
  property: IRPagingProperty,
): IRPropertyRef | undefined {
  const bindings = property.bindings.filter((binding) => binding.response !== undefined);
  if (bindings.length === 0 || bindings.some((binding) => binding.location !== "body")) {
    return undefined;
  }

  const wirePath = bindings[0]!.path;
  if (
    !bindings.every(
      (binding) =>
        binding.path.length === wirePath.length &&
        binding.path.every((segment, index) => segment === wirePath[index]),
    )
  ) {
    return undefined;
  }
  if (wirePath.length === 0) return undefined;

  const hops: IRId[] = [];
  let current: IRModel | undefined = envelope;
  for (const segment of wirePath) {
    if (current === undefined) return undefined;
    const match: IRModel["properties"][number] | undefined = current.properties.find(
      (candidate) => candidate.wireName === segment,
    );
    if (match === undefined) return undefined;
    hops.push(match.id);
    current = nested(pass, match.type);
  }
  const leaf = hops.pop();
  return leaf !== undefined ? { property: leaf, via: hops } : undefined;
}

/** The model a property's type leads into, following an array's element type. */
function nested(pass: PassContext, type: IRTypeRef): IRModel | undefined {
  if (type.kind === "model") return pass.index.model(type.id);
  if (type.kind === "array") return nested(pass, type.element);
  return undefined;
}

// ─── Naming ─────────────────────────────────────────────────────────────

/** PascalCase a spec name, splitting on the usual separators and case boundaries. */
function pascal(value: string): string {
  return deconstruct(value).map(capitalize).join("");
}

/** A C#-safe identifier: PascalCase, no illegal characters, never leading a digit. */
function identifier(value: string): string {
  const pascalCased = pascal(value).replace(/[^A-Za-z0-9_]/g, "_");
  return /^[0-9]/.test(pascalCased) ? `_${pascalCased}` : pascalCased || "_";
}

function deconstruct(identifierText: string): string[] {
  return identifierText
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .split(/[^A-Za-z0-9]+|\s+/)
    .filter((part) => part.length > 0);
}

function capitalize(word: string): string {
  return word.length === 0 ? word : word[0]!.toUpperCase() + word.slice(1);
}
