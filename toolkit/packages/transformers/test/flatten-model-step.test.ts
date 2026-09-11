import { describe, expect, it, vi } from "vitest";

import {
  compile,
  createRegistry,
  createTransformPipeline,
  noopGenerator,
  type CompilerDiagnostic,
} from "@azure-client-engine/compiler";
import {
  IR_SCHEMA_VERSION,
  parseIR,
  type IR,
  type IRId,
  type IRModel,
  type IROperation,
  type IRProperty,
  type IRVisibility,
  type HttpMethod,
} from "@azure-client-engine/ir";

import { flattenModelStep } from "../src/index.js";

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
  const diagnostics: CompilerDiagnostic[] = [];
  const out = flattenModelStep(options).run(parseIR(ir), {
    config: {},
    reportDiagnostic: (d) => diagnostics.push(d),
  });
  return { ir: out as IR, diagnostics };
}

const flattenedOf = (ir: IR, name: string) =>
  ir.models.find((m) => m.name === name)?.rendering?.flattened ?? [];

const leafPath = (path: IRId[]) => path.map((p) => p.name);

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

  it("prefixes deeper hops with the parent name and records the full id path", () => {
    const { ir } = run(irOf(armModels()));
    const skuName = flattenedOf(ir, "Thing").find((m) => m.name === "SkuName");
    expect(skuName).toBeDefined();
    expect(leafPath(skuName!.path)).toEqual([
      "Thing.properties",
      "ThingProperties.sku",
      "Sku.name",
    ]);
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

  it("does not mutate the input IR and re-validates against the schema", () => {
    const input = parseIR(irOf(armModels()));
    const snapshot = structuredClone(input);
    const out = flattenModelStep().run(input, { config: {}, reportDiagnostic: vi.fn() });
    expect(input).toEqual(snapshot);
    expect(() => parseIR(out)).not.toThrow();
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

  it("keeps the whole id path so the generator can assign through it", () => {
    const { ir } = run(irOf(armModels(), [operation("create", "PUT", "Thing")]));
    const params = ir.operations[0]!.requestBody!.rendering!.parameters!;
    expect(leafPath(params.find((p) => p.name === "SkuName")!.path)).toEqual([
      "Thing.properties",
      "ThingProperties.sku",
      "Sku.name",
    ]);
    // A top-level body property is promoted as-is: a single-hop path.
    expect(leafPath(params.find((p) => p.name === "Tags")!.path)).toEqual(["Thing.tags"]);
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
    expect(leafPath(params[0]!.path)).toEqual(["Body.thingName"]);
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

describe("flatten-model: pipeline integration", () => {
  it.each(["injected", "registered"] as const)("runs through compile() as an %s step", async (mode) => {
    const step = flattenModelStep();
    const source = parseIR(irOf(armModels(), [operation("create", "PUT", "Thing")]));

    const result = await compile(
      {
        entrypoint: "main.tsp",
        ...(mode === "registered" ? { config: { pipeline: [{ use: step.name }] } } : {}),
      },
      {
        emit: { name: "stub", produce: () => ({ ir: structuredClone(source) }) },
        generator: noopGenerator,
        captureIR: true,
        ...(mode === "injected"
          ? { transform: createTransformPipeline("flatten", [step]) }
          : { registry: createRegistry({ transforms: { [step.name]: flattenModelStep } }) }),
      },
    );

    expect(result.diagnostics).toEqual([]);
    expect(result.success).toBe(true);
    expect(result.snapshots?.map((s) => s.step)).toEqual(["emit", "flatten-model"]);
    // The emit snapshot is untouched; the step's output carries the flattening.
    expect(result.snapshots?.[0]?.ir.models.find((m) => m.name === "Thing")?.rendering).toBeUndefined();
    expect(result.ir?.models.find((m) => m.name === "Thing")?.rendering?.flattened?.length).toBeGreaterThan(0);
  });
});
