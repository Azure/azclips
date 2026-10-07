import { describe, expect, it, vi } from "vitest";

import {
  IR_SCHEMA_VERSION,
  parseIR,
  type IR,
  type IRId,
  type IRModel,
  type IROperation,
  type IRProperty,
  type IRPropertyRef,
  type IRVisibility,
  type HttpMethod,
} from "@azure-client-engine/ir";

import { flattenModelStep, type TransformContext } from "../src/index.js";

type Diagnostic = Parameters<TransformContext["reportDiagnostic"]>[0];

// ─── Fixture builders ───────────────────────────────────────────────────

const MODULE = "M";
const id = (name: string): IRId => ({ module: MODULE, namespace: ["S"], name });
const lowerFirst = (s: string): string => s.charAt(0).toLowerCase() + s.slice(1);

interface PropOptions {
  optional?: boolean;
  visibility?: IRVisibility[];
  access?: "public" | "internal";
  flatten?: "self" | "never";
}

function base(owner: string, name: string, o: PropOptions = {}) {
  return {
    id: id(`${owner}.${lowerFirst(name)}`),
    name,
    wireName: lowerFirst(name),
    optional: o.optional ?? true,
    nullable: false,
    ...(o.visibility !== undefined ? { visibility: o.visibility } : {}),
    ...(o.access !== undefined ? { access: o.access } : {}),
  };
}

const prim = (owner: string, name: string, o: PropOptions = {}): IRProperty => ({
  ...base(owner, name, o),
  typeKind: "primitive",
  type: { kind: "primitive", name: "string" },
});

const obj = (owner: string, name: string, target: string, o: PropOptions = {}): IRProperty => ({
  ...base(owner, name, o),
  typeKind: "object",
  type: { kind: "model", id: id(target) },
  ...(o.flatten !== undefined ? { rendering: { flatten: { into: o.flatten } } } : {}),
});

const dict = (owner: string, name: string, o: PropOptions = {}): IRProperty => ({
  ...base(owner, name, o),
  typeKind: "object",
  type: { kind: "dictionary", value: { kind: "primitive", name: "string" } },
});

const model = (name: string, properties: IRProperty[]): IRModel => ({
  id: id(name),
  name,
  source: MODULE,
  properties,
});

const SERVER_ID = id("server");

function operation(name: string, method: HttpMethod, bodyModel: string): IROperation {
  return {
    id: id(name),
    name,
    source: MODULE,
    method,
    pathTemplate: "/things/{thingName}",
    pathParameters: [
      {
        id: id(`${name}.thingName`),
        name: "ThingName",
        wireName: "thingName",
        type: { kind: "primitive", name: "string" },
        optional: false,
        nullable: false,
      },
    ],
    queryParameters: [],
    headerParameters: [],
    requestBody: { type: { kind: "model", id: id(bodyModel) }, contentType: "application/json" },
    responses: [{ statusCode: { kind: "exact", code: 200 }, isError: false }],
    apiVersion: { value: "2026-01-01", location: "query", parameterName: "api-version" },
    server: SERVER_ID,
  };
}

function irOf(models: IRModel[], operations: IROperation[] = []): IR {
  return {
    schemaVersion: IR_SCHEMA_VERSION,
    sources: [
      {
        id: MODULE,
        entrypoint: "main.tsp",
        apiVersion: { value: "2026-01-01", location: "query", parameterName: "api-version" },
        server: SERVER_ID,
        rootNamespace: ["S"],
      },
    ],
    servers: [{ id: SERVER_ID, endpoint: { kind: "literal", url: "https://example.com" } }],
    models,
    enums: [],
    operations,
  };
}

function run(ir: IR, options: Record<string, unknown> = {}) {
  const diagnostics: Diagnostic[] = [];
  const out = flattenModelStep(options).run(parseIR(ir), {
    reportDiagnostic: (d) => diagnostics.push(d),
  });
  return { ir: out as IR, diagnostics };
}

const flattenedOf = (ir: IR, name: string) =>
  ir.models.find((m) => m.name === name)?.rendering?.flattened ?? [];

/** A property reference by id names, to compare without spelling out whole ids. */
const hops = ({ property, via }: IRPropertyRef) => ({
  property: property.name,
  via: via.map((p) => p.name),
});

/** Deep copy with every `rendering` namespace removed, to isolate what a stage added. */
function stripRendering(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(stripRendering);
  if (value !== null && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>)
        .filter(([key]) => key !== "rendering")
        .map(([key, nested]) => [key, stripRendering(nested)]),
    );
  }
  return value;
}

// ─── A small ARM-shaped spec reused across cases ────────────────────────

/** Thing { properties -> ThingProperties { name, sku -> Sku { name, tier } }, tags } */
const armModels = (): IRModel[] => [
  model("Thing", [
    obj("Thing", "Properties", "ThingProperties", { optional: false }),
    dict("Thing", "Tags"),
  ]),
  model("ThingProperties", [
    prim("ThingProperties", "State", { visibility: ["read"] }),
    prim("ThingProperties", "Size", { optional: false, visibility: ["read", "create"] }),
    obj("ThingProperties", "Sku", "Sku"),
  ]),
  model("Sku", [prim("Sku", "Name", { optional: false }), prim("Sku", "Tier")]),
];

// ─── Tests ──────────────────────────────────────────────────────────────

describe("flatten-model: models", () => {
  it("hoists the ARM properties envelope without a prefix and marks the container", () => {
    const { ir, diagnostics } = run(irOf(armModels()));
    expect(diagnostics).toEqual([]);

    const names = flattenedOf(ir, "Thing").map((m) => m.name);
    expect(names).toContain("State");
    expect(names).toContain("Size");
    expect(names).not.toContain("PropertyState");

    const container = ir.models
      .find((m) => m.name === "Thing")!
      .properties.find((p) => p.name === "Properties")!;
    expect(container.typeKind === "object" && container.rendering?.flatten?.into).toBe("self");
  });

  it("prefixes deeper hops with the parent name and records every container walked", () => {
    const { ir } = run(irOf(armModels()));
    const skuName = flattenedOf(ir, "Thing").find((m) => m.name === "SkuName");
    expect(skuName).toBeDefined();
    expect(hops(skuName!)).toEqual({
      property: "Sku.name",
      via: ["Thing.properties", "ThingProperties.sku"],
    });
    // "Properties" is transparent, so it contributes no prefix; "Sku" does.
    expect(flattenedOf(ir, "ThingProperties").map((m) => m.name)).toEqual(["SkuName", "SkuTier"]);
  });

  it("uses a plural container's name verbatim — singularization is not this step's job", () => {
    const { ir } = run(
      irOf([
        model("Owner", [obj("Owner", "ScaleUpProperties", "ScaleUp", { optional: false })]),
        model("ScaleUp", [prim("ScaleUp", "UnusedSizeTiB")]),
      ]),
    );
    expect(flattenedOf(ir, "Owner").map((m) => m.name)).toEqual(["ScaleUpPropertiesUnusedSizeTiB"]);
  });

  it("never flattens a dictionary and leaves wire truth untouched", () => {
    const input = irOf(armModels());
    const { ir } = run(input);

    const thing = ir.models.find((m) => m.name === "Thing")!;
    expect(thing.properties.map((p) => p.name)).toEqual(["Properties", "Tags"]);
    expect(flattenedOf(ir, "Thing").some((m) => m.name.includes("Tag"))).toBe(false);
    // Every hop still carries its own wireName, so the wire path is recoverable.
    expect(thing.properties.find((p) => p.name === "Properties")!.wireName).toBe("properties");
  });

  it("is additive: it adds rendering hints in place and changes nothing else", () => {
    const input = parseIR(irOf(armModels(), [operation("create", "PUT", "Thing")]));
    const before = structuredClone(input);
    const out = flattenModelStep().run(input, { reportDiagnostic: vi.fn() });

    // Written in place — the catalog declares this stage `additive`, so the
    // orchestrator hands it an IR it owns rather than paying for a copy.
    expect(out).toBe(input);
    expect(() => parseIR(out)).not.toThrow();

    // The contract that makes sharing safe: strip every `rendering` namespace and
    // what remains must be byte-for-byte the IR the emitter produced.
    expect(stripRendering(out)).toEqual(stripRendering(before));
  });

  it("intersects visibility along the path and omits it when unconstrained", () => {
    const { ir } = run(irOf(armModels()));
    const members = flattenedOf(ir, "Thing");
    // read ∩ (all) = read
    expect(members.find((m) => m.name === "State")!.visibility).toEqual(["read"]);
    expect(members.find((m) => m.name === "Size")!.visibility).toEqual(["read", "create"]);
    // Unconstrained the whole way down stays omitted, not spelled out.
    expect("visibility" in members.find((m) => m.name === "SkuTier")!).toBe(false);
  });

  it("marks a member optional when any hop is optional", () => {
    const { ir } = run(irOf(armModels()));
    const members = flattenedOf(ir, "Thing");
    // Properties is required and Size is required → reachable without a null hop.
    expect(members.find((m) => m.name === "Size")!.optional).toBe(false);
    // Sku is optional, so Sku.name (itself required) becomes optional.
    expect(members.find((m) => m.name === "SkuName")!.optional).toBe(true);
  });
});

describe("flatten-model: opt-outs and limits", () => {
  it("honours the inlining threshold but always flattens a properties envelope", () => {
    const { ir } = run(irOf(armModels()), { inliningThreshold: 1 });
    // ThingProperties has 3 members > threshold, but it is the ARM envelope.
    expect(flattenedOf(ir, "Thing").length).toBeGreaterThan(0);
    // Sku (2 members) is over the threshold and is an ordinary container.
    expect(flattenedOf(ir, "ThingProperties")).toEqual([]);
  });

  it("flattens below a default threshold of 100, counting members already flattened in", () => {
    const scalars = (owner: string, count: number) =>
      Array.from({ length: count }, (_, i) => prim(owner, `P${i}`));
    const { ir } = run(
      irOf([
        model("Owner", [obj("Owner", "Small", "Small"), obj("Owner", "Large", "Large")]),
        model("Small", scalars("Small", 99)),
        // 99 declared properties, but Inner flattens in first: 98 + 2 = 100.
        model("Large", [...scalars("Large", 98), obj("Large", "Inner", "Inner")]),
        model("Inner", scalars("Inner", 2)),
      ]),
    );

    const owner = ir.models.find((m) => m.name === "Owner")!;
    const into = (name: string) => {
      const container = owner.properties.find((p) => p.name === name)!;
      return container.typeKind === "object" ? container.rendering?.flatten?.into : undefined;
    };
    expect(into("Small")).toBe("self");
    expect(into("Large")).toBeUndefined();
    expect(flattenedOf(ir, "Large").map((m) => m.name)).toEqual(["InnerP0", "InnerP1"]);
  });

  it("respects noInline and an explicit flatten.into = never", () => {
    const byOption = run(irOf(armModels()), { noInline: ["Sku"] }).ir;
    const names = flattenedOf(byOption, "Thing").map((m) => m.name);
    // Sku's members are not hoisted, but Sku itself still rides up through the
    // transparent `properties` envelope as a single object-typed member.
    expect(names).not.toContain("SkuName");
    expect(names).not.toContain("SkuTier");
    expect(names).toContain("Sku");
    expect(names).toContain("State");

    const models = armModels();
    models[1]!.properties[2] = obj("ThingProperties", "Sku", "Sku", { flatten: "never" });
    const byHint = run(irOf(models)).ir;
    expect(flattenedOf(byHint, "ThingProperties")).toEqual([]);
  });

  it("reports a circular reference, terminates, and still produces valid IR", () => {
    const { ir, diagnostics } = run(
      irOf([
        model("Node", [obj("Node", "Child", "Leaf", { optional: false })]),
        model("Leaf", [prim("Leaf", "Value"), obj("Leaf", "Parent", "Node")]),
      ]),
    );
    const circular = diagnostics.filter((d) => d.code.endsWith("/circular-reference"));
    expect(circular).toHaveLength(1);
    expect(circular[0]!.severity).toBe("warning");
    expect(circular[0]!.message).toContain("Node");
    expect(() => parseIR(ir)).not.toThrow();
  });

  it("warns and leaves the property alone when the target model is missing", () => {
    const { ir, diagnostics } = run(irOf([model("Thing", [obj("Thing", "Ghost", "Missing")])]));
    expect(diagnostics.map((d) => d.code)).toContain(
      "@azure-client-engine/transformers/flatten-model/unresolved-model",
    );
    expect(flattenedOf(ir, "Thing")).toEqual([]);
  });

  it("resolves name collisions between hoisted members", () => {
    const { ir } = run(
      irOf([
        model("Outer", [
          obj("Outer", "Left", "Left", { optional: false }),
          obj("Outer", "Right", "Right", { optional: false }),
        ]),
        model("Left", [prim("Left", "Name")]),
        model("Right", [prim("Right", "Name")]),
      ]),
    );
    const names = flattenedOf(ir, "Outer").map((m) => m.name);
    expect(names).toEqual(["LeftName", "RightName"]);
    expect(new Set(names).size).toBe(names.length);
  });
});

describe("flatten-model: request-body projection", () => {
  it("expands a body and filters by the operation's lifecycle phase", () => {
    const { ir } = run(irOf(armModels(), [operation("create", "PUT", "Thing")]));
    const body = ir.operations[0]!.requestBody!;
    expect(body.rendering?.flatten).toEqual({ into: "self" });

    const params = body.rendering?.parameters ?? [];
    const names = params.map((p) => p.name);
    // PUT writes the `create` phase: read-only State is dropped, Size is kept.
    expect(names).not.toContain("State");
    expect(names).toContain("Size");
    expect(names).toContain("Tags");
    expect(params.find((p) => p.name === "Size")!.required).toBe(true);
    expect(params.find((p) => p.name === "SkuTier")!.required).toBe(false);
  });

  it("drops create-only members on PATCH", () => {
    const { ir } = run(irOf(armModels(), [operation("update", "PATCH", "Thing")]));
    const names = (ir.operations[0]!.requestBody!.rendering?.parameters ?? []).map((p) => p.name);
    expect(names).not.toContain("Size"); // [read, create] excludes update
    expect(names).not.toContain("State");
    expect(names).toContain("SkuName");
  });

  it("references the property and its containers so the generator can assign through them", () => {
    const { ir } = run(irOf(armModels(), [operation("create", "PUT", "Thing")]));
    const params = ir.operations[0]!.requestBody!.rendering!.parameters!;
    expect(hops(params.find((p) => p.name === "SkuName")!)).toEqual({
      property: "Sku.name",
      via: ["Thing.properties", "ThingProperties.sku"],
    });
    // A top-level body property is promoted as-is: no containers to walk.
    expect(hops(params.find((p) => p.name === "Tags")!)).toEqual({ property: "Thing.tags", via: [] });
  });

  it("gives the operation's own parameters first claim on a name", () => {
    const models = [
      model("Body", [prim("Body", "ThingName", { optional: false })]),
    ];
    const { ir } = run(irOf(models, [operation("create", "PUT", "Body")]));
    const params = ir.operations[0]!.requestBody!.rendering!.parameters!;
    // `ThingName` is taken by the path parameter, so the body member yields.
    expect(params).toHaveLength(1);
    expect(params[0]!.name).not.toBe("ThingName");
    expect(hops(params[0]!)).toEqual({ property: "Body.thingName", via: [] });
  });

  it("skips internal members and bodies opted out of flattening", () => {
    const models = [model("Body", [prim("Body", "Hidden", { access: "internal" })])];
    expect(run(irOf(models, [operation("create", "PUT", "Body")])).ir.operations[0]!.requestBody!
      .rendering?.parameters).toBeUndefined();

    const optedOut = irOf(armModels(), [operation("create", "PUT", "Thing")]);
    optedOut.operations[0]!.requestBody!.rendering = { flatten: { into: "never" } };
    const { ir } = run(optedOut);
    expect(ir.operations[0]!.requestBody!.rendering?.parameters).toBeUndefined();
  });
});
