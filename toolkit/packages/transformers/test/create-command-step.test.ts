/**
 * Command projection.
 *
 * These pin the decisions the step makes so the generator does not have to: the
 * unified parameter surface, the declarative path, pinned api-version, resolved
 * pagination references, and the surface types the processor's generics come from.
 */

import { describe, expect, it } from "vitest";

import {
  commandActionKey,
  parseIR,
  parseTransformedIR,
  type IR,
  type IRCommand,
  type IROperation,
  type TransformedIR,
} from "@azure-client-engine/ir";

import { createCommandStep } from "../src/create-command-step.js";

const MODULE = "M";
const SERVER = { module: MODULE, namespace: ["N"], name: "server" };

const id = (name: string, iface?: string): Record<string, unknown> => ({
  module: MODULE,
  namespace: ["N"],
  ...(iface !== undefined ? { interface: iface } : {}),
  name,
});

const API_VERSION = { value: "2026-01-01", location: "query", parameterName: "api-version" };

/** The target `seed-ir` resolves before this step runs. */
const TARGET = {
  rootNamespace: ["Contoso", "Widgets"],
  projectName: "Contoso.Widgets",
  directories: { models: "Models", processors: "Processors" },
};

const param = (name: string, over: Record<string, unknown> = {}): Record<string, unknown> => ({
  id: id(`p.${name}`),
  name,
  wireName: name,
  type: { kind: "primitive", name: "string" },
  optional: false,
  nullable: false,
  ...over,
});

const operation = (over: Record<string, unknown> = {}): Record<string, unknown> => ({
  id: id("get", "Widgets"),
  name: "get",
  source: MODULE,
  method: "GET",
  pathTemplate: "/widgets",
  pathParameters: [],
  queryParameters: [],
  headerParameters: [],
  responses: [{ statusCode: { kind: "exact", code: 200 }, isError: false }],
  apiVersion: API_VERSION,
  server: SERVER,
  ...over,
});

function document(operations: Record<string, unknown>[], models: unknown[] = []): IR {
  return parseIR({
    schemaVersion: "0.12.0",
    sources: [
      {
        id: MODULE,
        entrypoint: "main.tsp",
        apiVersion: API_VERSION,
        server: SERVER,
        rootNamespace: ["Contoso", "Widgets"],
      },
    ],
    servers: [{ id: SERVER, endpoint: { kind: "literal", url: "https://example.com" } }],
    models,
    enums: [],
    operations,
    target: TARGET,
  });
}

/** Run the step, asserting it produced a valid transformed document. */
function run(ir: IR): {
  out: TransformedIR;
  diagnostics: { severity: string; code: string; message: string }[];
} {
  const diagnostics: { severity: string; code: string; message: string }[] = [];
  const result = createCommandStep.run(ir, {
    reportDiagnostic: (d) => diagnostics.push(d),
    config: {},
  }) as TransformedIR;
  parseTransformedIR(result);
  return { out: result, diagnostics };
}

const only = (ir: IR): IRCommand => {
  const { out } = run(ir);
  return out.commands[0]!;
};

/** The request path as text, parameters shown as `{Name}`. */
const uri = (command: IRCommand): string =>
  command.steps[0]!.request.path
    .map((segment) => (segment.kind === "literal" ? segment.text : `{${segment.parameter}}`))
    .join("");

describe("command projection", () => {
  it("projects one command per operation, keeping the operations intact", () => {
    const ir = document([operation(), operation({ id: id("list", "Widgets"), name: "list" })]);
    const { out, diagnostics } = run(ir);

    expect(diagnostics).toEqual([]);
    expect(out.commands).toHaveLength(2);
    // A projection, not a replacement — wire truth stays reachable.
    expect(out.operations).toHaveLength(2);
    expect(out.commands[0]!.steps[0]!.operation).toEqual(out.operations[0]!.id);
  });

  it("derives identity from the operation's subject and name", () => {
    const command = only(document([operation()]));

    expect(command.id).toEqual({ module: MODULE, subject: "Widgets", action: "Get", variant: "Get" });
    expect(command.name).toBe("GetWidgets");
    // Always populated, so no consumer has to branch on an absent variant. With no
    // `by` qualifier this is the action's only surface, so it is named after it.
    expect(command.id.variant).toBe("Get");
    expect(command.template).toBe("atomic");
  });

  it("takes the subject from the operation, not the interface that declared it", () => {
    // A brownfield interface is a file container: the spec marks the real group with
    // `@tag`, which the IR drops, but TCGC keeps it as the declared name's prefix
    // (swagger `ExpressRouteCrossConnectionPeerings_Get` -> `...PeeringsGet`, client
    // name `Get`). Without recovering it both operations collapse onto one identity.
    const ir = document([
      operation({ id: id("get", "CrossConnections"), name: "get" }),
      operation({ id: id("crossConnectionPeeringsGet", "CrossConnections"), name: "Get" }),
    ]);
    const { out, diagnostics } = run(ir);

    expect(diagnostics).toEqual([]);
    expect(out.commands.map((command) => command.id.subject)).toEqual([
      "CrossConnections",
      "CrossConnectionPeerings",
    ]);
    expect(out.commands.map((command) => command.name)).toEqual([
      "GetCrossConnections",
      "GetCrossConnectionPeerings",
    ]);
  });

  it("prefers an explicit client location over the inferred prefix", () => {
    // `@clientLocation` is the author stating the grouping; the prefix only infers
    // it. Where both exist they agree in practice, so this pins which one is
    // authoritative — and the decorator also covers operations the prefix cannot see.
    const ir = document([
      operation({
        id: id("get", "CrossConnections"),
        name: "get",
        clientLocation: "Peerings",
      }),
    ]);
    const { out, diagnostics } = run(ir);

    expect(diagnostics).toEqual([]);
    expect(out.commands[0]!.id.subject).toBe("Peerings");
    // The declaration site is untouched — id records where it was declared.
    expect(out.operations[0]!.id.interface).toBe("CrossConnections");
  });

  it("resolves a clash the declared names cannot, when the spec relocates", () => {
    // The deployments shape: a rename leaves two operations sharing a client name in
    // one interface, and only `@clientLocation` tells them apart.
    const ir = document([
      operation({ id: id("list", "Tenant"), name: "listAtScope", clientLocation: "Deployments" }),
      operation({
        id: id("listAtScope", "Tenant"),
        name: "listAtScope",
        clientLocation: "DeploymentOperations",
      }),
    ]);
    const { out, diagnostics } = run(ir);

    expect(diagnostics).toEqual([]);
    expect(out.commands.map((c) => c.id.subject)).toEqual(["Deployments", "DeploymentOperations"]);
  });

  it("matches the action word-wise so it is not sliced out of a longer word", () => {
    // `blocklist` ends with the letters of `list` but is one word, so the prefix is
    // empty and the subject falls back to the declaring interface.
    const command = only(document([operation({ id: id("blocklist", "Widgets"), name: "list" })]));

    expect(command.id.subject).toBe("Widgets");
    expect(command.id.action).toBe("List");
  });

  it("splits a `by` qualifier into sibling surfaces of one action", () => {
    // `List` and `ListByResourceGroup` are the same action differing only in what
    // the caller supplies, so they are siblings: one action, two variants.
    const ir = document([
      operation({ id: id("list", "Widgets"), name: "list" }),
      operation({ id: id("listByResourceGroup", "Widgets"), name: "listByResourceGroup" }),
    ]);
    const { out, diagnostics } = run(ir);

    expect(diagnostics).toEqual([]);
    expect(out.commands.map((c) => [c.id.action, c.id.variant])).toEqual([
      ["List", "List"],
      ["List", "ByResourceGroup"],
    ]);
    // Same action key, so a later pass finds them by id comparison, not by name.
    expect(commandActionKey(out.commands[0]!.id)).toBe(commandActionKey(out.commands[1]!.id));
    expect(out.commands.map((c) => c.name)).toEqual(["ListWidgets", "ListByResourceGroupWidgets"]);
  });

  it("does not split a leading or trailing `by`", () => {
    // A name that is only a qualifier has no action left, and `...By` qualifies
    // nothing — both stay whole rather than producing an empty axis.
    const ir = document([
      operation({ id: id("byPolicy", "Widgets"), name: "byPolicy" }),
      operation({ id: id("orderBy", "Widgets"), name: "orderBy" }),
    ]);
    const { out } = run(ir);

    expect(out.commands.map((c) => [c.id.action, c.id.variant])).toEqual([
      ["ByPolicy", "ByPolicy"],
      ["OrderBy", "OrderBy"],
    ]);
  });

  it("does not singularize to collapse a subject its variant already named", () => {
    // `ListByElasticSan` over subject `ElasticSans` doubles the noun. Removing it
    // needs English singularization — a PowerShell cmdlet-naming rule, and a client
    // dialect's job. This step concatenates and leaves the wording alone.
    const command = only(
      document([operation({ id: id("listByElasticSan", "ElasticSans"), name: "listByElasticSan" })]),
    );

    expect(command.id).toEqual({
      module: MODULE,
      subject: "ElasticSans",
      action: "List",
      variant: "ByElasticSan",
    });
    expect(command.name).toBe("ListByElasticSanElasticSans");
  });

  it("falls back to the namespace tail when an operation declares no interface", () => {
    const command = only(document([operation({ id: id("ping"), name: "ping" })]));

    expect(command.id.subject).toBe("N");
  });

  it("names both operations when two derive one identity", () => {
    // Same subject and action under two namespaces derives one `(module, subject,
    // action)` — which is an invalid document. The message has to identify both
    // sides: the incoming operation alone never shows what it collided with.
    const ir = document([
      operation(),
      operation({ id: { ...id("get", "Widgets"), namespace: ["N", "Other"] } }),
    ]);
    const { out, diagnostics } = run(ir);

    expect(diagnostics.map((d) => [d.severity, d.code.split("/").pop()])).toEqual([
      ["error", "duplicate-command-identity"],
    ]);
    expect(out.commands).toHaveLength(1);

    const message = diagnostics[0]!.message;
    // Both declared ids, distinguished by their namespaces...
    expect(message).toContain("[M] N::Widgets.get");
    expect(message).toContain("[M] N.Other::Widgets.get");
    // ...the identity they share, and which one survived.
    expect(message).toContain('["M","Widgets","Get","Get"]');
    expect(message).toContain("Only the first is emitted");
  });

  it("qualifies a colliding name rather than emitting two types under one", () => {
    // Distinct identities that concatenate to the same stem:
    // (AWidgets + Get) and (Widgets + GetA) both read `GetAWidgets`.
    const ir = document([
      operation({ id: id("get", "AWidgets"), name: "get" }),
      operation({ id: id("getA", "Widgets"), name: "getA" }),
    ]);
    const { out } = run(ir);

    expect(out.commands.map((command) => command.id.subject)).toEqual(["AWidgets", "Widgets"]);
    expect(out.commands.map((command) => command.name)).toEqual(["GetAWidgets", "GetAWidgets2"]);
  });
});

describe("parameter surface", () => {
  it("unifies path, query, and header parameters into one ordered list", () => {
    const command = only(
      document([
        operation({
          pathTemplate: "/widgets/{widgetName}",
          pathParameters: [param("widgetName")],
          queryParameters: [param("top", { optional: true })],
          headerParameters: [param("ifMatch", { wireName: "If-Match", optional: true })],
        }),
      ]),
    );

    expect(command.parameters.map((p) => [p.name, p.source.kind, p.required])).toEqual([
      ["WidgetName", "path", true],
      ["Top", "query", false],
      ["IfMatch", "header", false],
    ]);
  });

  it("marks protocol headers internal so no client surfaces them", () => {
    const command = only(
      document([
        operation({
          headerParameters: [
            param("contentType", { wireName: "Content-Type" }),
            param("accept", { wireName: "Accept" }),
            param("ifMatch", { wireName: "If-Match" }),
          ],
        }),
      ]),
    );

    const access = Object.fromEntries(command.parameters.map((p) => [p.name, p.access]));
    expect(access).toEqual({ ContentType: "internal", Accept: "internal", IfMatch: undefined });
    // Still sent: they are carried, not dropped.
    expect(command.steps[0]!.request.headers.map((h) => h.wireName)).toContain("Content-Type");
  });

  it("carries the serialization facts a request cannot be built without", () => {
    const command = only(
      document([
        operation({
          queryParameters: [
            param("tags", {
              type: { kind: "array", element: { kind: "primitive", name: "string" } },
              collectionFormat: "csv",
              optional: true,
            }),
            param("since", {
              type: { kind: "primitive", name: "string" },
              format: "date-time",
              optional: true,
            }),
          ],
        }),
      ]),
    );

    expect(command.parameters[0]!.collectionFormat).toBe("csv");
    expect(command.parameters[1]!.format).toBe("date-time");
  });
});

describe("request", () => {
  it("splits the path template into literal and parameter segments", () => {
    const command = only(
      document([
        operation({
          pathTemplate: "/subscriptions/{subscriptionId}/widgets/{widgetName}",
          pathParameters: [param("subscriptionId"), param("widgetName")],
        }),
      ]),
    );

    expect(uri(command)).toBe("/subscriptions/{SubscriptionId}/widgets/{WidgetName}");
    expect(command.steps[0]!.request.path.filter((s) => s.kind === "parameter")).toHaveLength(2);
  });

  it("warns rather than shipping an unbound placeholder in the URI", () => {
    const { out, diagnostics } = run(
      document([operation({ pathTemplate: "/widgets/{ghost}", pathParameters: [] })]),
    );

    expect(diagnostics.map((d) => d.code.split("/").pop())).toEqual(["unbound-path-placeholder"]);
    // Degrades to a literal rather than silently binding to nothing.
    expect(uri(out.commands[0]!)).toBe("/widgets/{ghost}");
  });

  it("pins the api-version as a literal rather than a parameter", () => {
    const command = only(document([operation()]));

    // The IR was emitted *at* a version, so a caller override would mismatch
    // model and wire — it is not an input.
    expect(command.steps[0]!.request.query).toEqual([
      { wireName: "api-version", value: { kind: "literal", text: "2026-01-01" } },
    ]);
    expect(command.parameters.map((p) => p.name)).not.toContain("ApiVersion");
  });

  it("puts a header-located api-version in the headers instead", () => {
    const command = only(
      document([
        operation({
          apiVersion: { value: "2026-01-01", location: "header", parameterName: "x-ms-version" },
        }),
      ]),
    );

    expect(command.steps[0]!.request.query).toEqual([]);
    expect(command.steps[0]!.request.headers[0]!.wireName).toBe("x-ms-version");
  });
});

describe("body", () => {
  const widget = {
    id: id("Widget"),
    name: "Widget",
    source: MODULE,
    properties: [
      {
        id: id("Widget.size"),
        name: "Size",
        wireName: "size",
        optional: true,
        nullable: false,
        typeKind: "primitive",
        type: { kind: "primitive", name: "int64" },
      },
    ],
  };

  const withBody = (rendering?: unknown): Record<string, unknown> =>
    operation({
      id: id("create", "Widgets"),
      name: "create",
      method: "PUT",
      requestBody: {
        type: { kind: "model", id: id("Widget") },
        contentType: "application/json",
        ...(rendering !== undefined ? { rendering } : {}),
      },
    });

  it("expands a projected body into flat members and marks the payload covered", () => {
    const command = only(
      document(
        [
          withBody({
            flatten: { into: "self" },
            parameters: [{ name: "Size", property: id("Widget.size"), via: [], required: false }],
          }),
        ],
        [widget],
      ),
    );

    const size = command.parameters.find((p) => p.name === "Size")!;
    expect(size.source).toEqual({ kind: "body", property: id("Widget.size"), via: [] });
    // Type comes from the property it projects, so the generator needs no lookup.
    expect(size.type).toEqual({ kind: "primitive", name: "int64" });
    expect(command.steps[0]!.request.body?.projected).toBe(true);
    expect(command.surface.request).toEqual({ kind: "model", id: id("Widget") });
  });

  it("leaves the body as a single payload when nothing projected it", () => {
    const command = only(document([withBody()], [widget]));

    expect(command.parameters.filter((p) => p.source.kind === "body")).toEqual([]);
    expect(command.steps[0]!.request.body?.projected).toBe(false);
  });

  it("warns and keeps the payload when a projected property does not resolve", () => {
    const { out, diagnostics } = run(
      document(
        [
          withBody({
            flatten: { into: "self" },
            parameters: [{ name: "Ghost", property: id("Widget.missing"), via: [], required: false }],
          }),
        ],
        [widget],
      ),
    );

    expect(diagnostics.map((d) => d.code.split("/").pop())).toEqual(["unresolved-body-parameter"]);
    expect(out.commands[0]!.steps[0]!.request.body?.projected).toBe(false);
  });
});

describe("responses and surface", () => {
  it("keeps every declared response, errors included", () => {
    const command = only(
      document([
        operation({
          responses: [
            { statusCode: { kind: "exact", code: 200 }, type: { kind: "primitive", name: "string" }, isError: false },
            { statusCode: { kind: "exact", code: 202 }, isError: false },
            { statusCode: { kind: "default" }, isError: true },
          ],
        }),
      ]),
    );

    // Not collapsed to the first success: the runtime picks an error model by status.
    expect(command.steps[0]!.responses).toHaveLength(3);
    expect(command.steps[0]!.responses.filter((r) => r.isError)).toHaveLength(1);
    // The surface takes the first typed success — the processor's TResponse.
    expect(command.surface.response).toEqual({ kind: "primitive", name: "string" });
  });

  it("carries long-running semantics onto the step that performs the call", () => {
    const command = only(
      document([
        operation({
          semantics: { longRunning: { pollingStrategy: "location", finalStateVia: "location" } },
        }),
      ]),
    );

    expect(command.steps[0]!.longRunning?.finalStateVia).toBe("location");
  });
});

describe("pagination", () => {
  const page = {
    id: id("WidgetPage"),
    name: "WidgetPage",
    source: MODULE,
    properties: [
      {
        id: id("WidgetPage.value"),
        name: "Value",
        wireName: "value",
        optional: false,
        nullable: false,
        typeKind: "collection",
        type: { kind: "array", element: { kind: "primitive", name: "string" } },
      },
      {
        id: id("WidgetPage.nextLink"),
        name: "NextLink",
        wireName: "nextLink",
        optional: true,
        nullable: false,
        typeKind: "primitive",
        type: { kind: "primitive", name: "string" },
      },
    ],
  };

  const pagingProperty = (wireName: string, segment: string) => ({
    sourcePath: [{ id: id(segment), name: wireName }],
    type: { kind: "primitive", name: "string" },
    optional: false,
    nullable: false,
    bindings: [
      { location: "body", path: [wireName], response: { statusCode: { kind: "exact", code: 200 } } },
    ],
  });

  const listing = (over: Record<string, unknown> = {}) =>
    operation({
      id: id("list", "Widgets"),
      name: "list",
      responses: [
        { statusCode: { kind: "exact", code: 200 }, type: { kind: "model", id: id("WidgetPage") }, isError: false },
      ],
      semantics: {
        pageable: {
          isList: true,
          metadataSource: "compiler",
          itemType: { kind: "primitive", name: "string" },
          input: {},
          output: {
            pageItems: pagingProperty("value", "WidgetPage.value"),
            nextLink: pagingProperty("nextLink", "WidgetPage.nextLink"),
          },
          sdk: {
            disabled: false,
            forced: false,
            nextLinkVerb: "GET",
            nextLinkReinjectedParameters: [],
          },
          ...over,
        },
      },
    });

  it("resolves wire paths to property references instead of leaving the generator to guess", () => {
    const command = only(document([listing()], [page]));

    // Ids, not PascalCased wire segments — so a later rename cannot break the reference.
    expect(command.steps[0]!.pagination?.items).toEqual({ property: id("WidgetPage.value"), via: [] });
    expect(command.steps[0]!.pagination?.nextLink).toEqual({ property: id("WidgetPage.nextLink"), via: [] });
  });

  it("separates a nested role's property from the containers walked to reach it", () => {
    const envelope = {
      id: id("WidgetEnvelope"),
      name: "WidgetEnvelope",
      source: MODULE,
      properties: [
        {
          id: id("WidgetEnvelope.result"),
          name: "Result",
          wireName: "result",
          optional: false,
          nullable: false,
          typeKind: "object",
          type: { kind: "model", id: id("WidgetPage") },
        },
      ],
    };
    const nestedProperty = (wireName: string, segment: string) => ({
      ...pagingProperty(wireName, segment),
      bindings: [
        { location: "body", path: ["result", wireName], response: { statusCode: { kind: "exact", code: 200 } } },
      ],
    });
    const nestedListing = listing({
      output: {
        pageItems: nestedProperty("value", "WidgetPage.value"),
        nextLink: nestedProperty("nextLink", "WidgetPage.nextLink"),
      },
    });
    nestedListing["responses"] = [
      { statusCode: { kind: "exact", code: 200 }, type: { kind: "model", id: id("WidgetEnvelope") }, isError: false },
    ];

    const pagination = only(document([nestedListing], [envelope, page])).steps[0]!.pagination;

    expect(pagination?.items).toEqual({ property: id("WidgetPage.value"), via: [id("WidgetEnvelope.result")] });
    expect(pagination?.nextLink).toEqual({
      property: id("WidgetPage.nextLink"),
      via: [id("WidgetEnvelope.result")],
    });
  });

  it("states pagination on the surface so the generator need not scan steps", () => {
    const command = only(document([listing()], [page]));

    expect(command.surface.paginated).toBe(true);
    // A paged command writes items, not the envelope carrying them.
    expect(command.surface.output).toEqual({ kind: "primitive", name: "string" });
    expect(command.surface.response).toEqual({ kind: "model", id: id("WidgetPage") });
  });

  it("degrades to a plain call when a paging path does not resolve", () => {
    const broken = listing();
    const semantics = broken["semantics"] as { pageable: { output: Record<string, unknown> } };
    semantics.pageable.output["pageItems"] = pagingProperty("ghost", "WidgetPage.ghost");

    const { out, diagnostics } = run(document([broken], [page]));

    expect(diagnostics.map((d) => d.code.split("/").pop())).toEqual(["paging-binding-unresolved"]);
    expect(out.commands[0]!.surface.paginated).toBe(false);
    expect(out.commands[0]!.steps[0]!.pagination).toBeUndefined();
  });
});

describe("target", () => {
  it("carries the target seed-ir resolved", () => {
    const { out } = run(document([operation()]));

    expect(out.target).toEqual(TARGET);
  });

  it("fails on a document no stage resolved a target for", () => {
    const ir = document([operation()]);
    delete ir.target;

    expect(() => createCommandStep.run(ir, { reportDiagnostic: () => {}, config: {} })).toThrow(/seed-ir/);
  });
});

describe("options", () => {
  it("takes none, so it warns about any it is given, such as the retired rootNamespace", () => {
    const diagnostics: { code: string; message: string }[] = [];
    createCommandStep.run(document([operation()]), {
      reportDiagnostic: (d) => diagnostics.push(d),
      config: { rootNamespace: "Fabrikam" },
    });

    expect(
      diagnostics.filter((d) => d.code.endsWith("/unknown-option")).map((d) => d.message),
    ).toEqual(["'rootNamespace' is not a known option and is ignored. This stage takes no options."]);
  });
});

describe("degradation", () => {
  it("skips an unnamed operation instead of emitting an unusable command", () => {
    // `IROperationSchema` requires a name, so the document is built unvalidated —
    // the point is what the step does with input it cannot name.
    const ir = document([operation()]);
    (ir.operations[0] as unknown as IROperation).name = "";

    const diagnostics: { severity: string; code: string; message: string }[] = [];
    const out = createCommandStep.run(ir, {
      reportDiagnostic: (d) => diagnostics.push(d),
      config: {},
    }) as TransformedIR;

    expect(diagnostics.map((d) => d.code.split("/").pop())).toEqual(["empty-operation-name"]);
    expect(out.commands).toEqual([]);
  });
});
