/**
 * Registry of transform-step and generator implementations.
 *
 * This is the "what's available" half of the configurable pipeline: step and
 * generator *implementations* are registered here (code), while *which* run, in
 * *what* order, with *what* options is declared as data in
 * `CompileConfig.pipeline` / `CompileConfig.generator`. The backbone resolves
 * the declared ids against this registry.
 *
 * Ids are constrained to a filename-safe charset so a step name maps 1:1 to a
 * captured-IR filename (`ir/<index>-<step>.json`) with no escaping.
 */

import type { TransformStep } from "./transform.js";
import type { Generator } from "./generate.js";

/** Builds a transform step from app-provided options. */
export type TransformStepFactory = (options: Record<string, unknown>) => TransformStep;

/** Builds a generator from app-provided options. */
export type GeneratorFactory = (options: Record<string, unknown>) => Generator;

export interface CompilerRegistry {
  transforms: Map<string, TransformStepFactory>;
  generators: Map<string, GeneratorFactory>;
}

/** Registered ids must be filename-safe (used verbatim as snapshot filenames). */
const ID_PATTERN = /^[a-z0-9._-]+$/;

/** True if `id` is a valid, filename-safe registry id. */
export function isValidStepId(id: string): boolean {
  return ID_PATTERN.test(id);
}

/**
 * Build a registry from plain records. Throws on an invalid id so registration
 * mistakes surface at startup rather than as a broken filename later.
 */
export function createRegistry(init?: {
  transforms?: Record<string, TransformStepFactory>;
  generators?: Record<string, GeneratorFactory>;
}): CompilerRegistry {
  const transforms = new Map<string, TransformStepFactory>();
  const generators = new Map<string, GeneratorFactory>();

  for (const [id, factory] of Object.entries(init?.transforms ?? {})) {
    if (!isValidStepId(id)) {
      throw new Error(`Invalid transform step id '${id}': must match ${ID_PATTERN}.`);
    }
    transforms.set(id, factory);
  }
  for (const [id, factory] of Object.entries(init?.generators ?? {})) {
    if (!isValidStepId(id)) {
      throw new Error(`Invalid generator id '${id}': must match ${ID_PATTERN}.`);
    }
    generators.set(id, factory);
  }

  return { transforms, generators };
}
