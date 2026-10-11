/**
 * Compiler-owned stage catalog and planner.
 *
 * Orchestration lives here, not in a host. The compiler declares *what* stages
 * exist, *in what order they run*, and *whether each is on by default*. A host's
 * only verb is to flip a stage's enablement — it never appends, inserts,
 * reorders, or names a pipeline.
 *
 * The workflow is a **single linear sequence**. One IR leaves the emit stage and
 * flows through every enabled transform in catalog order; each generate stage
 * renders the IR as it stands when its turn comes. There are no branches, so
 * there is exactly one IR per run and the compiler never copies it.
 *
 * Two consequences matter when authoring a stage:
 *
 *   - **Order is catalog array position.** Disabling a stage simply removes it
 *     from the sequence — there is no tree to reattach, so position stays
 *     intrinsic for free.
 *   - **Position no longer implies dependency.** Every stage sees the
 *     accumulated work of every enabled stage before it, so ordering alone
 *     cannot express "I am wrong without that stage". That is what `requires`
 *     is for.
 *
 * ## Stage discipline (convention, deliberately not enforced)
 *
 * Because a generate stage renders whatever the IR looks like at its position,
 * the order of `STAGE_CATALOG` carries a rule that no code checks:
 *
 *   1. **Stages that alter existing IR go before the first generate stage**, so
 *      every backend renders the same canonical IR.
 *   2. **Stages after the first generate stage are purely additive** — they
 *      write new, backend-namespaced fields only, so one backend's annotations
 *      are inert to another's instead of corrupting it.
 *
 * This is upheld by review rather than by a mechanism: the catalog below is the
 * single place ordering is decided, and no host can insert into it.
 */

import type { CompilerDiagnostic } from "./types.js";

export type StageKind = "emit" | "transform" | "generate";

/**
 * Whether a stage runs when the config says nothing.
 *   - `mandatory` — always runs; a config cannot disable it.
 *   - `on` / `off` — runs unless/until the config flips it.
 */
export type StageDefault = "mandatory" | "on" | "off";

export interface StageDescriptor {
  readonly id: string;
  readonly kind: StageKind;
  readonly default: StageDefault;
  /**
   * Stage ids this one depends on having run.
   *
   * In a linear sequence every stage already runs after every earlier stage, so
   * ordering carries no dependency information — `requires` is the only way to
   * say "my output is wrong without that stage". Disabling a required stage is
   * therefore an error rather than a silent degradation.
   *
   * Each id must name an earlier, IR-producing stage.
   */
  readonly requires?: readonly string[];
}

export const TSP_EMIT_STAGE = "tsp-emit";
export const TWEAK_IR_STAGE = "tweak-ir";
export const FLATTEN_MODEL_STAGE = "flatten-model";
export const CREATE_COMMAND_STAGE = "create-command";
export const PS_DERIVE_STAGE = "ps.derive";
export const CSHARP_STAGE = "csharp";

/**
 * The workflow, in execution order. Array position *is* the order — see the
 * stage-discipline note above for what that order is required to express.
 */
export const STAGE_CATALOG: readonly StageDescriptor[] = [
  { id: TSP_EMIT_STAGE, kind: "emit", default: "mandatory" },
  // The one place IR-level renaming happens, and therefore first among the
  // transforms: both stages below copy names into structures of their own
  // (`ModelHints.flattened`, `IRCommand`), so a rename after either would leave a
  // stale copy behind.
  { id: TWEAK_IR_STAGE, kind: "transform", default: "on" },
  { id: FLATTEN_MODEL_STAGE, kind: "transform", default: "on" },
  // Projects operations into commands, producing the `TransformedIR` every
  // generator consumes. The document phase changes here: stages before this one
  // see `IR`, stages after it see commands and a resolved target.
  { id: CREATE_COMMAND_STAGE, kind: "transform", default: "on" },
  { id: PS_DERIVE_STAGE, kind: "transform", default: "off", requires: [CREATE_COMMAND_STAGE] },
  // Declared rather than assumed: without commands the generator has nothing to
  // emit, so disabling the projection is a config error instead of a crash.
  { id: CSHARP_STAGE, kind: "generate", default: "on", requires: [CREATE_COMMAND_STAGE] },
];

/** One stage of the resolved sequence. */
export interface PlannedStage {
  readonly descriptor: StageDescriptor;
  /**
   * Position in the enabled sequence; 0 is the emit stage. Generate stages take
   * a position but produce no IR, so captured-IR indices can have gaps.
   */
  readonly index: number;
}

export interface StagePlan {
  /** The emit stage, always `stages[0]`. */
  readonly emit: PlannedStage;
  /** Every enabled stage, in execution order. */
  readonly stages: readonly PlannedStage[];
}

const DIAGNOSTIC_PREFIX = "@azure-client-engine/compiler";

function error(code: string, message: string): CompilerDiagnostic {
  return { severity: "error", code: `${DIAGNOSTIC_PREFIX}/${code}`, message };
}

function warning(code: string, message: string): CompilerDiagnostic {
  return { severity: "warning", code: `${DIAGNOSTIC_PREFIX}/${code}`, message };
}

/** True when a stage runs, given the catalog default and the config's toggles. */
export function isStageEnabled(
  descriptor: StageDescriptor,
  toggles: Readonly<Record<string, boolean>>,
): boolean {
  if (descriptor.default === "mandatory") return true;
  return toggles[descriptor.id] ?? descriptor.default === "on";
}

/**
 * Resolve `catalog defaults ⊕ config toggles` into the sequence to run.
 *
 * Returns `undefined` (after reporting) when the toggles or the catalog cannot
 * produce a valid sequence. A run with no generate stage is reported as a
 * warning, not an error: it still runs, but it writes no files.
 */
export function planStages(
  toggles: Readonly<Record<string, boolean>>,
  report: (diagnostic: CompilerDiagnostic) => void,
  catalog: readonly StageDescriptor[] = STAGE_CATALOG,
): StagePlan | undefined {
  const positionOf = new Map<string, number>();
  for (const [position, descriptor] of catalog.entries()) {
    if (positionOf.has(descriptor.id)) {
      report(
        error("duplicate-stage", `The stage catalog declares '${descriptor.id}' more than once.`),
      );
      return undefined;
    }
    positionOf.set(descriptor.id, position);
  }

  // Everything downstream needs IR, so the sequence has to open with emit — and
  // that emit stage must be unconditional, or a run could start with no IR.
  const emits = catalog.filter((descriptor) => descriptor.kind === "emit");
  if (emits.length !== 1 || catalog[0]?.kind !== "emit" || catalog[0].default !== "mandatory") {
    report(
      error(
        "invalid-stage-catalog",
        "The stage catalog must begin with exactly one 'emit' stage, declared mandatory.",
      ),
    );
    return undefined;
  }

  for (const [position, descriptor] of catalog.entries()) {
    for (const required of descriptor.requires ?? []) {
      const at = positionOf.get(required);
      if (at === undefined) {
        report(
          error(
            "invalid-stage-catalog",
            `Stage '${descriptor.id}' requires '${required}', which is not in the catalog.`,
          ),
        );
        return undefined;
      }
      // A requirement that runs later can never be satisfied.
      if (at >= position) {
        report(
          error(
            "invalid-stage-catalog",
            `Stage '${descriptor.id}' requires '${required}', which does not run earlier in the catalog.`,
          ),
        );
        return undefined;
      }
      // A generator produces files, not IR, so nothing can depend on its work.
      if (catalog[at]!.kind === "generate") {
        report(
          error(
            "invalid-stage-catalog",
            `Stage '${descriptor.id}' cannot require '${required}': a generate stage produces no IR.`,
          ),
        );
        return undefined;
      }
    }
  }

  // Toggles name stages, so an unknown or non-optional id is a config error, not
  // something to silently ignore.
  for (const [id, value] of Object.entries(toggles)) {
    const position = positionOf.get(id);
    if (position === undefined) {
      report(error("unknown-stage", `No stage named '${id}' exists in the compiler's catalog.`));
      return undefined;
    }
    if (catalog[position]!.default === "mandatory" && value === false) {
      report(error("stage-not-optional", `The '${id}' stage is mandatory and cannot be disabled.`));
      return undefined;
    }
  }

  const enabled = catalog.filter((descriptor) => isStageEnabled(descriptor, toggles));
  const enabledIds = new Set(enabled.map((descriptor) => descriptor.id));

  // Disabling a stage another one depends on would otherwise produce quietly
  // wrong output, which is the failure this field exists to prevent.
  for (const descriptor of enabled) {
    for (const required of descriptor.requires ?? []) {
      if (!enabledIds.has(required)) {
        report(
          error(
            "missing-required-stage",
            `Stage '${descriptor.id}' requires '${required}', which is disabled. ` +
              `Enable '${required}', or disable '${descriptor.id}' too.`,
          ),
        );
        return undefined;
      }
    }
  }

  const stages: PlannedStage[] = enabled.map((descriptor, index) => ({ descriptor, index }));

  if (!enabled.some((descriptor) => descriptor.kind === "generate")) {
    report(
      warning(
        "no-generator",
        "No generate stage is enabled, so this run produces no files. " +
          "Enable a generate stage, or use the run only to capture IR.",
      ),
    );
  }

  return { emit: stages[0]!, stages };
}
