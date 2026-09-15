/**
 * Fulfilment registry: implementations for the compiler's declared stage ids.
 *
 * This is **not** a place to declare a pipeline. The compiler's `STAGE_CATALOG`
 * owns which stages exist, in what order they run, and what runs by default; a
 * host only supplies the code behind ids it can serve. The two built-in external
 * stages (`tsp-emit`, `csharp`) need no entry — they run through the host
 * `Runtime` — but a registry entry overrides them when one is present.
 *
 * Ids are the catalog's, constrained to a filename-safe charset so a stage id
 * maps 1:1 to a captured-IR filename (`ir/<index>-<stage>.json`) with no escaping.
 */

import type { EmitProducer } from "./emit-producer.js";
import type { TransformStep } from "./transform.js";
import type { Generator } from "./generate.js";

/** Builds an emit producer from the stage's configured options. */
export type EmitProducerFactory = (options: Record<string, unknown>) => EmitProducer;

/** Builds a transform step from the stage's configured options. */
export type TransformStepFactory = (options: Record<string, unknown>) => TransformStep;

/** Builds a generator from the stage's configured options. */
export type GeneratorFactory = (options: Record<string, unknown>) => Generator;

export interface CompilerRegistry {
  emitters: Map<string, EmitProducerFactory>;
  transforms: Map<string, TransformStepFactory>;
  generators: Map<string, GeneratorFactory>;
}

/** Registered ids must be filename-safe (used verbatim as snapshot filenames). */
const ID_PATTERN = /^[a-z0-9._-]+$/;

/** True if `id` is a valid, filename-safe stage id. */
export function isValidStageId(id: string): boolean {
  return ID_PATTERN.test(id);
}

/**
 * Build a registry from plain records. Throws on an invalid id so registration
 * mistakes surface at startup rather than as a broken filename later.
 */
export function createRegistry(init?: {
  emitters?: Record<string, EmitProducerFactory>;
  transforms?: Record<string, TransformStepFactory>;
  generators?: Record<string, GeneratorFactory>;
}): CompilerRegistry {
  return {
    emitters: toMap("emitter", init?.emitters),
    transforms: toMap("transform", init?.transforms),
    generators: toMap("generator", init?.generators),
  };
}

function toMap<T>(label: string, entries: Record<string, T> | undefined): Map<string, T> {
  const map = new Map<string, T>();
  for (const [id, factory] of Object.entries(entries ?? {})) {
    if (!isValidStageId(id)) {
      throw new Error(`Invalid ${label} stage id '${id}': must match ${ID_PATTERN}.`);
    }
    map.set(id, factory);
  }
  return map;
}
