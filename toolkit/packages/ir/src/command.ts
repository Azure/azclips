/**
 * Commands: the generated processor surface, projected from operations.
 *
 * An `IROperation` is wire truth — one HTTP call, exactly as the spec declares it.
 * An `IRCommand` is what a command-shaped client offers: a named processor with one
 * flat parameter list, a surfaced request and response, and (sometimes) more than one
 * operation behind it. The two live side by side; nothing is removed from
 * `IR.operations`, and every command's steps point back at the operations they
 * project, so a captured IR can always be traced to wire truth.
 *
 * Why a separate node rather than more hints on `IROperation`:
 *
 *   - **A processor's parameter surface does not exist anywhere today.** It is
 *     `pathParameters ∪ queryParameters ∪ headerParameters ∪ body-projection`,
 *     currently unioned inside the C# generator. Client transforms need something
 *     to annotate, and that something has to be a single list.
 *   - **A command is not 1:1 with an operation.** A read-before-write update is one
 *     command over a GET and a PUT.
 *   - **Naming and collision resolution happen once, here.** Downstream may reorder
 *     or regroup commands, but must never re-derive a name — the same rule that
 *     keeps `ModelHints.flattened` authoritative for flattening.
 *
 * Commands live flat at the IR root. **Array order is not meaningful**; group with
 * `id.group` / `id.family`, not with position.
 *
 * The name is deliberate: `Core.Abstractions` — the client-neutral runtime layer both
 * the CLI and the PowerShell modules sit on — already calls this a command
 * (`CommandInvocation`, `ICommandEngine`). A client that is *not* command-shaped
 * should get its own projection off `IROperation` rather than a diluted version of
 * this node.
 */

import { z } from "zod";
import { IRIdSchema, type IRId } from "./id.js";
import { IRTypeRefSchema, type IRTypeRef } from "./type-ref.js";
import { IRLiteralValueSchema, type IRLiteralValue } from "./literal.js";
import { AccessKinds, FormatKinds, type FormatKind, type IRAccess } from "./primitives.js";
import {
  CliCommandDialectSchema,
  CliParameterDialectSchema,
  PsCommandDialectSchema,
  PsParameterDialectSchema,
  type CliCommandDialect,
  type CliParameterDialect,
  type PsCommandDialect,
  type PsParameterDialect,
} from "./dialect.js";
import {
  CollectionFormats,
  HttpMethods,
  IRLongRunningSemanticsSchema,
  IRMultipartBodySchema,
  IRResponseHeaderSchema,
  IRStatusCodeSchema,
  type CollectionFormat,
  type HttpMethod,
  type IRLongRunningSemantics,
  type IRMultipartBody,
  type IRResponseHeader,
  type IRStatusCode,
} from "./operation.js";

// ─── Identity ───────────────────────────────────────────────────────────

/**
 * Identity for a command. Four axes, which is why this is not an `IRId`: that has
 * three usable slots (`module`, `interface`, `name`), and two sibling commands over
 * the same action collide on all three.
 *
 * Unlike an `IRId`, this is **synthesized** rather than read from a declaration
 * site — a command has no declaration site. The transform that creates commands owns
 * their uniqueness.
 *
 * Deliberately **config-independent**: the resolved output namespace, project name,
 * and directory layout are placement, not identity, and live in `IRTarget`. Keeping
 * them out means changing the root namespace cannot churn every command's identity
 * (and with it every captured-IR diff).
 *
 * OPEN — is `action` unique document-wide or only within `group`? Two groups may both
 * declare `Get`. Currently scoped to `group`, so `(module, group, action, variant)`
 * is the unique tuple.
 */
export type IRCommandId = {
  /** Source id (`IRSource.id`) of the spec the primary operation came from. */
  module: string;
  /**
   * The containing scope — the operation's interface, or a derived group for
   * namespace-level operations. One group holds many commands.
   */
  group: string;
  /**
   * What the command does (`Get`, `Create`, `Update`). **Siblings share everything
   * but `variant`**, so finding them is an id comparison ignoring that one field,
   * never string-parsing a name.
   *
   * Not simply the operation's name: a `getPut` command projects a GET and a PUT
   * into a single `Update`, which is neither operand's name.
   */
  action: string;
  /**
   * Which input surface within the action. Absent is the base variant.
   *
   * A variant is a **surface** axis, not a behavioral one: sibling commands share
   * their internals (a base variant may keep, say, a resource-id string internal)
   * and differ only in what they expose publicly. Sequencing is `template`'s job.
   */
  variant?: string | undefined;
};

export const IRCommandIdSchema: z.ZodType<IRCommandId> = z
  .object({
    module: z.string().min(1),
    group: z.string().min(1),
    action: z.string().min(1),
    variant: z.string().min(1).optional(),
  })
  .strict();

/**
 * Key for identity-based lookup, mirroring `idKey` for `IRId`: structural, so two
 * commands share a key only when every axis matches.
 */
export function commandKey(id: IRCommandId): string {
  return JSON.stringify([id.module, id.group, id.action, id.variant ?? null]);
}

/** Key for a set of sibling commands — every variant of one action shares it. */
export function commandActionKey(id: IRCommandId): string {
  return JSON.stringify([id.module, id.group, id.action]);
}

// ─── Templates ──────────────────────────────────────────────────────────

/**
 * How a command drives its steps — a processor template, and a property of the
 * **command**, not of any one operation. Operations are solely operations; the
 * template is what knows what to do with the list.
 *
 *   - `atomic` — one call.
 *   - `getPut` — read, then write: the GET's response seeds the PUT's body, so a
 *     caller supplies only what it wants to change.
 *
 * More will follow. A new template adds a member here, an arity rule below, and its
 * handling in the generator — it never changes the shape of a step.
 */
export const CommandTemplates = ["atomic", "getPut"] as const;

export type IRCommandTemplate = (typeof CommandTemplates)[number];

/**
 * Steps a template requires, in order. The template defines the positional contract
 * (`getPut` is `[read, write]`), so it is validated rather than left as an unwritten
 * rule the generator would have to assume.
 */
const TEMPLATE_STEPS: Readonly<Record<IRCommandTemplate, number>> = {
  atomic: 1,
  getPut: 2,
};

// ─── Parameters ─────────────────────────────────────────────────────────

/**
 * Where a command parameter's value goes on the wire, and what it was projected
 * from. Resolving a parameter back to its declaration is what lets a client
 * transform read facts it must not rewrite — enum values, docs, visibility.
 *
 * `body` carries a property-id path into the request-body model rather than a
 * single id, because a projected member may cross nesting hops. Identical in shape
 * and meaning to `FlattenedParameter.path`.
 *
 * There is no `apiVersion` source: the API version is not a parameter. The IR was
 * emitted *at* a version (the compile flag projects `@versioned`), so the models
 * correspond to it — a runtime override would silently mismatch model and wire. It
 * is a fixed value on the request instead.
 */
export type IRCommandParameterSource =
  | { kind: "path"; parameter: IRId }
  | { kind: "query"; parameter: IRId }
  | { kind: "header"; parameter: IRId }
  | { kind: "body"; path: IRId[] };

export const IRCommandParameterSourceSchema: z.ZodType<IRCommandParameterSource> =
  z.discriminatedUnion("kind", [
    z.object({ kind: z.literal("path"), parameter: IRIdSchema }).strict(),
    z.object({ kind: z.literal("query"), parameter: IRIdSchema }).strict(),
    z.object({ kind: z.literal("header"), parameter: IRIdSchema }).strict(),
    z.object({ kind: z.literal("body"), path: z.array(IRIdSchema).min(1) }).strict(),
  ]);

/**
 * One member of the processor's parameter surface — the single annotation target
 * for client transforms.
 *
 * `type` is carried rather than resolved through `source` so the generator can emit
 * the member without a lookup; that redundancy is the point of a projection. It is
 * written once, here, from the declaration `source` names.
 */
export type IRCommandParameter = {
  /**
   * Final C#-facing member name, unique within the command. Resolved here, and the
   * text the request interpolates — downstream may rename for its own dialect, but
   * must not re-derive this one.
   */
  name: string;
  source: IRCommandParameterSource;
  type: IRTypeRef;
  /** Whether *this command* requires a value — narrower than the declaration's optionality. */
  required: boolean;
  nullable: boolean;
  defaultValue?: IRLiteralValue | undefined;
  /**
   * How multiple values serialize, for an array-typed query or header parameter.
   * Carried from `IRParameter` because the request cannot be built without it.
   */
  collectionFormat?: CollectionFormat | undefined;
  /** Wire format for a primitive-shaped parameter (e.g. `date-time`). */
  format?: FormatKind | undefined;
  /**
   * Generated-code visibility. Omitted when `public` (the default).
   *
   * A body-sourced parameter inherits it from the property it projects
   * (`IRProperty.access`); path, query, and header parameters have no declared
   * access and are public unless a transform decides otherwise.
   */
  access?: IRAccess | undefined;
  docs?: string | undefined;
  summary?: string | undefined;
  /**
   * Per-client presentation. Absent means every client renders the parameter from
   * the fields above.
   */
  dialect?: IRCommandParameterDialect | undefined;
};

// ─── Request ────────────────────────────────────────────────────────────

/**
 * One piece of the request path. Declarative so the generator only spells it: today
 * the path is rebuilt by string-replacing `{wireName}` with a C# identifier, which
 * leaks identifier rules into path handling.
 */
export type IRCommandUriSegment =
  | { kind: "literal"; text: string }
  /** A command parameter's `name`, already unique within the command. */
  | { kind: "parameter"; parameter: string };

export const IRCommandUriSegmentSchema: z.ZodType<IRCommandUriSegment> =
  z.discriminatedUnion("kind", [
    z.object({ kind: z.literal("literal"), text: z.string().min(1) }).strict(),
    z.object({ kind: z.literal("parameter"), parameter: z.string().min(1) }).strict(),
  ]);

/**
 * The value behind a query or header entry: a command parameter, or a constant the
 * spec pinned.
 *
 * The constant case is what carries the API version — a fixed value rather than an
 * input — and generalizes to any spec-declared constant query or header.
 */
export type IRCommandValue =
  | { kind: "parameter"; parameter: string }
  | { kind: "literal"; text: string };

export const IRCommandValueSchema: z.ZodType<IRCommandValue> = z.discriminatedUnion("kind", [
  z.object({ kind: z.literal("parameter"), parameter: z.string().min(1) }).strict(),
  z.object({ kind: z.literal("literal"), text: z.string().min(1) }).strict(),
]);

/** One `key=value` pair on the request URI, or one request header. */
export type IRCommandWireEntry = {
  /** Query key or header name on the wire. */
  wireName: string;
  value: IRCommandValue;
};

export const IRCommandWireEntrySchema: z.ZodType<IRCommandWireEntry> = z
  .object({
    wireName: z.string().min(1),
    value: IRCommandValueSchema,
  })
  .strict();

/**
 * The payload one step sends. The endpoint is deliberately absent: it belongs to the
 * command's `server`, and the generated base owns it so one override can redirect a
 * whole extension.
 */
export type IRCommandRequestBody = {
  /**
   * Absent for a multipart tuple, whose whole shape is in `multipart.parts` —
   * mirroring `IRRequestBody`, which is the only way a tuple body can be carried
   * at all.
   */
  type?: IRTypeRef | undefined;
  contentType?: string | undefined;
  multipart?: IRMultipartBody | undefined;
  /**
   * True when the command's `parameters` fully cover this body, so the payload member
   * can leave the public surface instead of duplicating every flat parameter. Decided
   * here rather than inferred by the generator from "did any flat member resolve".
   */
  projected: boolean;
  docs?: string | undefined;
  summary?: string | undefined;
};

export const IRCommandRequestBodySchema: z.ZodType<IRCommandRequestBody> = z
  .object({
    type: IRTypeRefSchema.optional(),
    contentType: z.string().min(1).optional(),
    multipart: IRMultipartBodySchema.optional(),
    projected: z.boolean(),
    docs: z.string().optional(),
    summary: z.string().optional(),
  })
  .strict()
  .superRefine((body, ctx) => {
    // Same rule as IRRequestBody: only a multipart tuple may omit its type.
    if (body.type === undefined && body.multipart?.kind !== "tuple") {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "A request body requires a type unless it is a multipart tuple.",
        path: ["type"],
      });
    }
  });

/** What one step sends. */
export type IRCommandRequest = {
  method: HttpMethod;
  /** Path after the endpoint, in order. */
  path: IRCommandUriSegment[];
  query: IRCommandWireEntry[];
  headers: IRCommandWireEntry[];
  body?: IRCommandRequestBody | undefined;
};

export const IRCommandRequestSchema: z.ZodType<IRCommandRequest> = z
  .object({
    method: z.enum(HttpMethods),
    path: z.array(IRCommandUriSegmentSchema),
    query: z.array(IRCommandWireEntrySchema),
    headers: z.array(IRCommandWireEntrySchema),
    body: IRCommandRequestBodySchema.optional(),
  })
  .strict();

// ─── Response ───────────────────────────────────────────────────────────

/**
 * One declared response of one step. A list, not a single type: 200, 202, 204, and
 * 400 can each carry a different shape, and today the generator keeps only the first
 * non-error one and silently drops the rest — errors included.
 *
 * Error entries need no representation beyond `isError` + `type`: which model to
 * deserialize is chosen at runtime from the status code, so it is a
 * `status -> type` map rather than anything the processor's type signature carries.
 */
export type IRCommandResponse = {
  statusCodes: IRStatusCode[];
  /** Absent for no content (204), an untyped body, or a multipart tuple. */
  type?: IRTypeRef | undefined;
  contentType?: string | undefined;
  multipart?: IRMultipartBody | undefined;
  headers?: IRResponseHeader[] | undefined;
  isError: boolean;
  docs?: string | undefined;
};

export const IRCommandResponseSchema: z.ZodType<IRCommandResponse> = z
  .object({
    statusCodes: z.array(IRStatusCodeSchema).min(1),
    type: IRTypeRefSchema.optional(),
    contentType: z.string().min(1).optional(),
    multipart: IRMultipartBodySchema.optional(),
    headers: z.array(IRResponseHeaderSchema).optional(),
    isError: z.boolean(),
    docs: z.string().optional(),
  })
  .strict();

// ─── Pagination ─────────────────────────────────────────────────────────

/**
 * Resolved pagination for one step: member-id paths into the response model, not
 * wire segments.
 *
 * `IRPageableSemantics` records the declared roles by wire path, which leaves the
 * generator PascalCasing each segment and hoping it matches an emitted member —
 * explicitly a heuristic, and a fragile one now that flattening renames members.
 * These paths are resolved once, here, the same way `FlattenedMember.path` is.
 */
export type IRCommandPagination = {
  itemType: IRTypeRef;
  /** Property ids from the response model down to the page items. */
  itemsPath: IRId[];
  /** Property ids to the continuation link, when the spec declares one. */
  nextLinkPath?: IRId[] | undefined;
  nextLinkVerb?: "GET" | "POST" | undefined;
};

export const IRCommandPaginationSchema: z.ZodType<IRCommandPagination> = z
  .object({
    itemType: IRTypeRefSchema,
    itemsPath: z.array(IRIdSchema).min(1),
    nextLinkPath: z.array(IRIdSchema).min(1).optional(),
    nextLinkVerb: z.enum(["GET", "POST"]).optional(),
  })
  .strict();

// ─── Steps ──────────────────────────────────────────────────────────────

/**
 * One HTTP call the command makes.
 *
 * A multi-operation command is a **sequence**, not one request with extra responses:
 * each call has its own request and its own responses, so a read's 404 and a write's
 * 400 stay distinct. `longRunning` and `pagination` are per step for the same reason
 * — in a `getPut` the write may be long-running while the read is not.
 *
 * `atomic` is simply `steps.length === 1`; the shape does not special-case it.
 */
export type IRCommandStep = {
  /** The `IROperation.id` this step calls. */
  operation: IRId;
  request: IRCommandRequest;
  /**
   * Every declared response, success and error. At least one, matching
   * `IROperation.responses` — an empty projection would mean the operation's
   * responses were silently dropped.
   */
  responses: IRCommandResponse[];
  /** Reused verbatim: already declarative, and carries no C# expressions. */
  longRunning?: IRLongRunningSemantics | undefined;
  pagination?: IRCommandPagination | undefined;
};

export const IRCommandStepSchema: z.ZodType<IRCommandStep> = z
  .object({
    operation: IRIdSchema,
    request: IRCommandRequestSchema,
    responses: z.array(IRCommandResponseSchema).min(1),
    longRunning: IRLongRunningSemanticsSchema.optional(),
    pagination: IRCommandPaginationSchema.optional(),
  })
  .strict();

// ─── Surface ────────────────────────────────────────────────────────────

/**
 * What the *caller* supplies and receives — the processor's generic surface, as
 * opposed to what any single step puts on the wire.
 *
 * The division is the point: **a step holds behavior, this holds the surface.** The
 * two coincide for `atomic`; a template defines its own rule otherwise — a `getPut`
 * sends the write's body but constructs it from the read's response, so the surfaced
 * input is not the wire body type. Likewise an LRO's steps declare a 202 while the
 * surfaced `response` is what the processor ultimately returns.
 *
 * Error types are deliberately absent: the error model is selected at runtime from
 * the status code, so a type parameter could only document what might be thrown.
 */
export type IRCommandSurface = {
  /** Surfaced request payload type. Absent when the command sends no body. */
  request?: IRTypeRef | undefined;
  /** Surfaced success type — what the processor deserializes into. */
  response?: IRTypeRef | undefined;
  /**
   * What the command writes to its client's output, when that differs from
   * `response` — a paged command emits items, not the envelope that carries them.
   */
  output?: IRTypeRef | undefined;
  /**
   * Whether the processor pages its results, and so emits the pagination override.
   *
   * Stated rather than derived: the generator would otherwise have to scan steps to
   * answer a question about the surface. *How* to page stays on the step that does
   * it (`IRCommandStep.pagination`); this only says that it happens.
   */
  paginated: boolean;
};

export const IRCommandSurfaceSchema: z.ZodType<IRCommandSurface> = z
  .object({
    request: IRTypeRefSchema.optional(),
    response: IRTypeRefSchema.optional(),
    output: IRTypeRefSchema.optional(),
    paginated: z.boolean(),
  })
  .strict();

// ─── Dialects ───────────────────────────────────────────────────────────

/**
 * Per-client presentation — the "dialect" a command is spoken in. The shapes live
 * in `dialect.ts`, which is the contract projected into the generator's POCOs and
 * into the generated extension's runtime metadata.
 *
 * Dialects live on commands and their parameters, and nowhere else. Models are
 * client-neutral: they are output shapes that serialize themselves, so no client
 * dialect belongs on them. Hints on models and properties shape the emitted **C#
 * type** (`flatten`, `flattened`); dialects shape the **client surface**. Keeping the
 * two apart bounds growth at `clients x {command, parameter}` rather than
 * `clients x node kinds`.
 *
 * Client attributes are emitted on processors only — never projected back onto a
 * model member, which would drag client concerns into model generation.
 *
 * The members are a **closed set**: adding a client means adding a field here plus
 * its schema and transform, which that client needs regardless. A closed shape also
 * generates far better C# POCOs than an open map would.
 *
 * **Absent until a client transform runs.** The command transform writes no
 * dialects — it knows nothing about clients — so a freshly projected command has no
 * `dialect` at all, rather than an empty container.
 */
export type IRCommandDialect = {
  ps?: PsCommandDialect | undefined;
  cli?: CliCommandDialect | undefined;
};

export const IRCommandDialectSchema: z.ZodType<IRCommandDialect> = z
  .object({
    ps: PsCommandDialectSchema.optional(),
    cli: CliCommandDialectSchema.optional(),
  })
  .strict();

/** Parameter-level dialects, keyed by client. Same closed-set rule as above. */
export type IRCommandParameterDialect = {
  ps?: PsParameterDialect | undefined;
  cli?: CliParameterDialect | undefined;
};

export const IRCommandParameterDialectSchema: z.ZodType<IRCommandParameterDialect> = z
  .object({
    ps: PsParameterDialectSchema.optional(),
    cli: CliParameterDialectSchema.optional(),
  })
  .strict();

export const IRCommandParameterSchema: z.ZodType<IRCommandParameter> = z
  .object({
    name: z.string().min(1),
    source: IRCommandParameterSourceSchema,
    type: IRTypeRefSchema,
    required: z.boolean(),
    nullable: z.boolean(),
    defaultValue: IRLiteralValueSchema.optional(),
    collectionFormat: z.enum(CollectionFormats).optional(),
    format: z.enum(FormatKinds).optional(),
    access: z.enum(AccessKinds).optional(),
    docs: z.string().optional(),
    summary: z.string().optional(),
    dialect: IRCommandParameterDialectSchema.optional(),
  })
  .strict();

// ─── The command ────────────────────────────────────────────────────────

export type IRCommand = {
  id: IRCommandId;
  /**
   * Resolved C#-facing type stem, unique across the document. Resolved here, never
   * re-derived downstream.
   *
   * OPEN — `id` already carries the axes this is built from. Either this is the
   * renameable display form and `id.name` the frozen original, or one of the two is
   * redundant; decide before they drift into meaning the same thing.
   */
  name: string;
  template: IRCommandTemplate;
  /** Server this command talks to, and therefore which generated base it derives from. */
  server: IRId;
  /** The calls, in order. The template defines what each position means. */
  steps: IRCommandStep[];
  /** The whole processor parameter surface, in emission order. */
  parameters: IRCommandParameter[];
  surface: IRCommandSurface;
  /**
   * Generated-code visibility, carried from the primary operation's
   * `IROperation.access`. Omitted when `public` (the default).
   */
  access?: IRAccess | undefined;
  docs?: string | undefined;
  summary?: string | undefined;
  dialect?: IRCommandDialect | undefined;
};

export const IRCommandSchema: z.ZodType<IRCommand> = z
  .object({
    id: IRCommandIdSchema,
    name: z.string().min(1),
    template: z.enum(CommandTemplates),
    server: IRIdSchema,
    steps: z.array(IRCommandStepSchema).min(1),
    parameters: z.array(IRCommandParameterSchema),
    surface: IRCommandSurfaceSchema,
    access: z.enum(AccessKinds).optional(),
    docs: z.string().optional(),
    summary: z.string().optional(),
    dialect: IRCommandDialectSchema.optional(),
  })
  .strict()
  .superRefine((command, ctx) => {
    // The template defines a positional contract over the steps, so arity is checked
    // rather than assumed by whatever reads it.
    const expected = TEMPLATE_STEPS[command.template];
    if (command.steps.length !== expected) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message:
          `Command '${command.name}' uses template '${command.template}', which requires ` +
          `${expected} step(s), but declares ${command.steps.length}.`,
        path: ["steps"],
      });
    }

    // Parameter names are the surface's identity: requests interpolate them, and
    // client dialects key off them.
    const declared = new Set<string>();
    command.parameters.forEach((parameter, index) => {
      if (declared.has(parameter.name)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `Command '${command.name}' declares parameter '${parameter.name}' more than once.`,
          path: ["parameters", index, "name"],
        });
      }
      declared.add(parameter.name);
    });

    // A path or query hole with no parameter behind it would emit uncompilable code.
    const requireDeclared = (name: string, path: (string | number)[]): void => {
      if (!declared.has(name)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `Command '${command.name}' references parameter '${name}', which it does not declare.`,
          path,
        });
      }
    };

    command.steps.forEach((step, stepIndex) => {
      step.request.path.forEach((segment, index) => {
        if (segment.kind === "parameter") {
          requireDeclared(segment.parameter, ["steps", stepIndex, "request", "path", index]);
        }
      });
      const checkEntries = (entries: IRCommandWireEntry[], field: "query" | "headers"): void => {
        entries.forEach((entry, index) => {
          if (entry.value.kind === "parameter") {
            requireDeclared(entry.value.parameter, [
              "steps",
              stepIndex,
              "request",
              field,
              index,
              "value",
              "parameter",
            ]);
          }
        });
      };
      checkEntries(step.request.query, "query");
      checkEntries(step.request.headers, "headers");
    });

    // A parameter set exists because parameters declare membership in it, so a
    // default naming one nothing declares would leave PowerShell with no set to
    // fall back to.
    const defaultSet = command.dialect?.ps?.defaultParameterSet;
    if (defaultSet !== undefined) {
      const declaredSets = new Set(
        command.parameters.flatMap((parameter) =>
          (parameter.dialect?.ps?.parameterSets ?? []).flatMap((membership) =>
            membership.set !== undefined ? [membership.set] : [],
          ),
        ),
      );
      if (!declaredSets.has(defaultSet)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message:
            `Command '${command.name}' names default parameter set '${defaultSet}', ` +
            "which none of its parameters declares membership in.",
          path: ["dialect", "ps", "defaultParameterSet"],
        });
      }
    }

    // The surface says the processor pages; some step has to say how.
    if (command.surface.paginated && !command.steps.some((step) => step.pagination !== undefined)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message:
          `Command '${command.name}' is marked paginated, but none of its steps carries ` +
          "pagination metadata.",
        path: ["surface", "paginated"],
      });
    }
  });
