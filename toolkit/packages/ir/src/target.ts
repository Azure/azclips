/**
 * Generation target: where the generated code lands and what it is called.
 *
 * Resolved once, at transform time, from generation-time configuration — not by the
 * generator. A namespace or a project name is a *decision*, and decisions belong in
 * the IR where they can be inspected in a captured document; the generator only
 * spells them.
 *
 * Deliberately separate from `IRCommandId`. Identity is config-independent, so
 * changing the root namespace relocates the output without churning every command's
 * identity — and with it every captured-IR diff.
 *
 * Shared by everything the generator emits: models, the processor base, and the
 * project file all need placement, which is why this is a document-level node rather
 * than a field on commands. Anything that genuinely varies *per command* stays on the
 * command.
 */

import { z } from "zod";

/** Relative directories the generated artifacts are written to. */
export type IRTargetDirectories = {
  /** Model types (`Models`). */
  models: string;
  /** Processors and their shared base (`Processors`). */
  processors: string;
};

export const IRTargetDirectoriesSchema: z.ZodType<IRTargetDirectories> = z
  .object({
    models: z.string().min(1),
    processors: z.string().min(1),
  })
  .strict();

export type IRTarget = {
  /**
   * Root namespace of the generated code, in segments
   * (`["Microsoft", "Azure", "Azclips", "Extensions", "ElasticSan"]`). Per-artifact
   * namespaces extend it — models add the `Models` segment, processors `Processors`.
   */
  rootNamespace: string[];
  /**
   * Name of the generated project. Free to differ from the namespace: the assembly
   * and `.csproj` are named from this, and `CommandMetadata.AssemblyName` resolves
   * against it.
   */
  projectName: string;
  directories: IRTargetDirectories;
};

export const IRTargetSchema: z.ZodType<IRTarget> = z
  .object({
    rootNamespace: z.array(z.string().min(1)).min(1),
    projectName: z.string().min(1),
    directories: IRTargetDirectoriesSchema,
  })
  .strict();
