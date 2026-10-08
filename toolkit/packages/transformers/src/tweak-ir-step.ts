/**
 * Adjust the emitted IR so it can be rendered by the target, before anything
 * structural depends on it.
 *
 * This is the **only** place IR-level renaming happens. Two later steps
 * denormalize names — `flatten-model` composes member names out of property names
 * and a containing model's name, and `create-command` copies names into commands —
 * so a rename after either leaves a stale copy behind with no way to notice. Placing
 * every rename here keeps exactly one source of truth for what a thing is called.
 * (Renaming an `IRCommand` is a different matter: commands are created downstream and
 * nothing denormalizes them, so those steps may name their own output freely.)
 *
 * ## What it does today
 *
 *   1. `resolve-type-names` — give every model and enum a name that is unique across
 *      the emitted type universe.
 *
 * ## What it must not do
 *
 * It does not touch `id`. An `IRId` records where a declaration came from, and stays
 * true no matter what the rendered name becomes — that is what lets a consumer follow
 * a reference after a rename.
 */

import type { TransformContext, TransformStep } from "./transform.js";
import {
  serializeId,
  type IR,
  type IREnum,
  type IRModel,
} from "@azure-client-engine/ir";

const STEP_NAME = "tweak-ir";
const DIAGNOSTIC_PREFIX = `@azure-client-engine/transformers/${STEP_NAME}`;

// ─── Pass plumbing ──────────────────────────────────────────────────────

type Report = (severity: "error" | "warning", code: string, message: string) => void;

export function tweakIrStep(_options: Record<string, unknown> = {}): TransformStep {
  return {
    name: STEP_NAME,
    run(ir: IR, context: TransformContext): IR {
      const report: Report = (severity, code, message) =>
        context.reportDiagnostic({ severity, code: `${DIAGNOSTIC_PREFIX}/${code}`, message });

      resolveTypeNames(ir, report);
      return ir;
    },
  };
}

// ─── Type names ─────────────────────────────────────────────────────────

/** A model or an enum — everything that becomes a type in the generated project. */
type TypeDeclaration = IRModel | IREnum;

/**
 * Make every type name unique.
 *
 * Models and enums are emitted into one namespace, so their names share a single
 * space even though the IR keeps them in separate collections. Distinct declarations
 * collide there routinely: `ErrorResponse` exists both in a provider's own namespace
 * and in `Azure.ResourceManager.CommonTypes`. Left alone the generator emits one of
 * them and every reference to either resolves to it, so the other's shape is silently
 * lost — it compiles, and deserializes into the wrong type.
 *
 * Renaming rather than dropping keeps both shapes, which is the only outcome that
 * preserves what the spec described.
 */
function resolveTypeNames(ir: IR, report: Report): void {
  const declarations: TypeDeclaration[] = [...ir.models, ...ir.enums];
  const byName = new Map<string, TypeDeclaration[]>();
  for (const declaration of declarations) {
    const bucket = byName.get(declaration.name);
    if (bucket === undefined) byName.set(declaration.name, [declaration]);
    else bucket.push(declaration);
  }

  // Every name in play, so a qualified replacement cannot land on an unrelated type.
  const taken = new Set(byName.keys());

  for (const [name, bucket] of byName) {
    if (bucket.length < 2) continue;

    // The primary source keeps the plain name: it is the spec being generated, and a
    // library type is the one a reader expects to see qualified. Falling back to
    // declaration order keeps the choice deterministic when that does not decide it.
    const primary = ir.sources[0]?.id;
    const keeperIndex = Math.max(
      0,
      bucket.findIndex((declaration) => declaration.source === primary),
    );

    for (const [index, declaration] of bucket.entries()) {
      if (index === keeperIndex) continue;

      const renamed = qualify(declaration, taken);
      taken.add(renamed);

      report(
        "warning",
        "type-name-collision",
        `'${serializeId(declaration.id)}' renders as type '${name}', which ` +
          `'${serializeId(bucket[keeperIndex]!.id)}' already claims. Renamed to '${renamed}' so ` +
          "both shapes survive; references resolve by id and are unaffected.",
      );

      declaration.name = renamed;
    }
  }
}

/**
 * A unique name for a declaration that lost its plain one, built by prefixing
 * namespace segments from the inside out: `CommonTypes` before
 * `ResourceManagerCommonTypes`.
 *
 * Qualifying with the declaration's own namespace keeps the result meaningful — the
 * name still says where the type came from — where a counter would only say that
 * something collided. The counter exists solely so this always terminates.
 */
function qualify(declaration: TypeDeclaration, taken: ReadonlySet<string>): string {
  const segments = declaration.id.namespace;
  for (let take = 1; take <= segments.length; take++) {
    const candidate = segments.slice(segments.length - take).join("") + declaration.name;
    if (!taken.has(candidate)) return candidate;
  }

  for (let suffix = 2; ; suffix++) {
    const candidate = `${declaration.name}${suffix}`;
    if (!taken.has(candidate)) return candidate;
  }
}
