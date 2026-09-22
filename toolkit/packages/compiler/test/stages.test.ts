/**
 * Stage-planner tests.
 *
 * The planner turns `catalog defaults ⊕ config toggles` into one linear
 * sequence. These tests pin the resolution rules — enablement, what happens when
 * a middle stage is disabled, the `requires` dependency check, and the
 * validation that rejects a catalog or a toggle set that cannot produce a valid
 * sequence.
 */

import { describe, expect, it } from "vitest";

import {
  CSHARP_STAGE,
  TWEAK_IR_STAGE,
  CREATE_COMMAND_STAGE,
  FLATTEN_MODEL_STAGE,
  STAGE_CATALOG,
  TSP_EMIT_STAGE,
  isStageEnabled,
  planStages,
  type CompilerDiagnostic,
  type StageDescriptor,
} from "../src/index.js";

/** Plan against a catalog, returning the sequence plus whatever was reported. */
function plan(
  toggles: Record<string, boolean>,
  catalog?: readonly StageDescriptor[],
): { sequence: ReturnType<typeof planStages>; diagnostics: CompilerDiagnostic[] } {
  const diagnostics: CompilerDiagnostic[] = [];
  const sequence = planStages(toggles, (d) => diagnostics.push(d), catalog);
  return { sequence, diagnostics };
}

/** Resolved stage ids in execution order, so shape assertions stay readable. */
const order = (sequence: ReturnType<typeof planStages>): string[] =>
  sequence!.stages.map((s) => s.descriptor.id);

const codes = (diagnostics: CompilerDiagnostic[]): string[] =>
  diagnostics.map((d) => d.code.replace("@azure-client-engine/compiler/", ""));

describe("stage planner: the shipped catalog", () => {
  it("plans the whole shipped sequence with nothing configured", () => {
    const { sequence, diagnostics } = plan({});
    expect(diagnostics).toEqual([]);
    expect(order(sequence)).toEqual([TSP_EMIT_STAGE, TWEAK_IR_STAGE, FLATTEN_MODEL_STAGE, CREATE_COMMAND_STAGE, CSHARP_STAGE]);
    expect(sequence!.emit.descriptor.id).toBe(TSP_EMIT_STAGE);
  });

  it("closes the sequence when a middle stage is disabled", () => {
    const { sequence, diagnostics } = plan({ [FLATTEN_MODEL_STAGE]: false });
    expect(diagnostics).toEqual([]);
    expect(order(sequence)).toEqual([TSP_EMIT_STAGE, TWEAK_IR_STAGE, CREATE_COMMAND_STAGE, CSHARP_STAGE]);
  });

  it("numbers stages by their position in the enabled sequence", () => {
    const { sequence } = plan({});
    expect(sequence!.stages.map((s) => s.index)).toEqual([0, 1, 2, 3, 4]);
    // Disabling a stage renumbers what is left; it leaves no hole behind.
    const { sequence: trimmed } = plan({ [FLATTEN_MODEL_STAGE]: false });
    expect(trimmed!.stages.map((s) => s.index)).toEqual([0, 1, 2, 3]);
  });

  it("warns when no generate stage is enabled, since the run writes nothing", () => {
    const { sequence, diagnostics } = plan({ [CSHARP_STAGE]: false });
    expect(sequence).toBeDefined();
    expect(codes(diagnostics)).toEqual(["no-generator"]);
    expect(diagnostics[0]!.severity).toBe("warning");
  });

  it("rejects an unknown stage id instead of ignoring it", () => {
    const { sequence, diagnostics } = plan({ "no-such-stage": true });
    expect(sequence).toBeUndefined();
    expect(codes(diagnostics)).toEqual(["unknown-stage"]);
  });

  it("refuses to disable a mandatory stage", () => {
    const { sequence, diagnostics } = plan({ [TSP_EMIT_STAGE]: false });
    expect(sequence).toBeUndefined();
    expect(codes(diagnostics)).toEqual(["stage-not-optional"]);
  });

  it("applies defaults per stage, and a toggle overrides them", () => {
    const [emit, flatten] = [STAGE_CATALOG[0]!, STAGE_CATALOG[1]!];
    expect(isStageEnabled(emit, {})).toBe(true);
    expect(isStageEnabled(emit, { [emit.id]: false })).toBe(true); // mandatory wins
    expect(isStageEnabled(flatten, {})).toBe(true); // default: on
    expect(isStageEnabled(flatten, { [flatten.id]: false })).toBe(false);
  });

  it("keeps an optional stage out until the config turns it on", () => {
    const catalog: readonly StageDescriptor[] = [
      ...STAGE_CATALOG,
      { id: "extra", kind: "transform", default: "off" },
    ];
    expect(order(plan({}, catalog).sequence)).not.toContain("extra");
    expect(order(plan({ extra: true }, catalog).sequence)).toEqual([
      TSP_EMIT_STAGE,
      TWEAK_IR_STAGE,
      FLATTEN_MODEL_STAGE,
      CREATE_COMMAND_STAGE,
      CSHARP_STAGE,
      "extra",
    ]);
  });
});

describe("stage planner: requires", () => {
  /**
   * Ordering alone cannot express dependency in a linear sequence — every stage
   * runs after every earlier one — so `requires` is what makes "disabling that
   * stage breaks me" a config error rather than quietly wrong output.
   */
  const DEPENDENT: readonly StageDescriptor[] = [
    { id: "tsp-emit", kind: "emit", default: "mandatory" },
    { id: "flatten", kind: "transform", default: "on" },
    { id: "virtual-params", kind: "transform", default: "on", requires: ["flatten"] },
    { id: "csharp", kind: "generate", default: "on" },
  ];

  it("plans normally while the requirement is enabled", () => {
    const { sequence, diagnostics } = plan({}, DEPENDENT);
    expect(diagnostics).toEqual([]);
    expect(order(sequence)).toEqual(["tsp-emit", "flatten", "virtual-params", "csharp"]);
  });

  it("errors when a requirement is disabled, rather than degrading silently", () => {
    const { sequence, diagnostics } = plan({ flatten: false }, DEPENDENT);
    expect(sequence).toBeUndefined();
    expect(codes(diagnostics)).toEqual(["missing-required-stage"]);
    // The message names both ends, so the fix is obvious from the error alone.
    expect(diagnostics[0]!.message).toContain("virtual-params");
    expect(diagnostics[0]!.message).toContain("flatten");
  });

  it("ignores the requirement when the dependent stage is disabled too", () => {
    const { sequence, diagnostics } = plan(
      { flatten: false, "virtual-params": false },
      DEPENDENT,
    );
    expect(diagnostics).toEqual([]);
    expect(order(sequence)).toEqual(["tsp-emit", "csharp"]);
  });

  it("rejects a requirement that is not in the catalog", () => {
    const { sequence, diagnostics } = plan({}, [
      { id: "tsp-emit", kind: "emit", default: "mandatory" },
      { id: "a", kind: "transform", default: "on", requires: ["ghost"] },
    ]);
    expect(sequence).toBeUndefined();
    expect(codes(diagnostics)).toEqual(["invalid-stage-catalog"]);
  });

  it("rejects a requirement that runs later, which could never be satisfied", () => {
    const { sequence, diagnostics } = plan({}, [
      { id: "tsp-emit", kind: "emit", default: "mandatory" },
      { id: "a", kind: "transform", default: "on", requires: ["b"] },
      { id: "b", kind: "transform", default: "on" },
    ]);
    expect(sequence).toBeUndefined();
    expect(codes(diagnostics)).toEqual(["invalid-stage-catalog"]);
  });

  it("rejects requiring a generate stage, which produces files and not IR", () => {
    const { sequence, diagnostics } = plan({}, [
      { id: "tsp-emit", kind: "emit", default: "mandatory" },
      { id: "gen", kind: "generate", default: "on" },
      { id: "a", kind: "transform", default: "on", requires: ["gen"] },
    ]);
    expect(sequence).toBeUndefined();
    expect(codes(diagnostics)).toEqual(["invalid-stage-catalog"]);
  });
});

describe("stage planner: catalog validation", () => {
  it("requires the sequence to begin with an emit stage", () => {
    const { sequence, diagnostics } = plan({}, [
      { id: "a", kind: "transform", default: "mandatory" },
    ]);
    expect(sequence).toBeUndefined();
    expect(codes(diagnostics)).toEqual(["invalid-stage-catalog"]);
  });

  it("requires the emit stage to be mandatory, so a run always has IR", () => {
    const { sequence, diagnostics } = plan({}, [
      { id: "tsp-emit", kind: "emit", default: "on" },
      { id: "csharp", kind: "generate", default: "on" },
    ]);
    expect(sequence).toBeUndefined();
    expect(codes(diagnostics)).toEqual(["invalid-stage-catalog"]);
  });

  it("rejects a second emit stage", () => {
    const { sequence, diagnostics } = plan({}, [
      { id: "tsp-emit", kind: "emit", default: "mandatory" },
      { id: "other-emit", kind: "emit", default: "on" },
    ]);
    expect(sequence).toBeUndefined();
    expect(codes(diagnostics)).toEqual(["invalid-stage-catalog"]);
  });

  it("rejects a duplicate stage id", () => {
    const { sequence, diagnostics } = plan({}, [
      { id: "tsp-emit", kind: "emit", default: "mandatory" },
      { id: "dup", kind: "transform", default: "on" },
      { id: "dup", kind: "transform", default: "on" },
    ]);
    expect(sequence).toBeUndefined();
    expect(codes(diagnostics)).toEqual(["duplicate-stage"]);
  });
});
