/**
 * Generation target: what the generated code is called and — for now — which
 * extension it is.
 *
 * Resolved once, at transform time, from generation-time configuration — not by the
 * generator. A namespace or a project name is a *decision*, and decisions belong in
 * the IR where they can be inspected in a captured document; the generator only
 * spells them. Where files land inside the project (`Models`, `Processors`) is not a
 * decision: it is the generator's own convention, so it is not here.
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
  /**
   * The service the extension is generated for (`ElasticSan`).
   *
   * Identity facts — this and `description` — live on the target only for now. As
   * more facts the spec cannot provide arrive, they may move to a node of their own.
   * The extension's version is not one: it is decided at release, not at generation.
   */
  serviceName?: string | undefined;
  /** Description of the generated extension. */
  description?: string | undefined;
};

export const IRTargetSchema: z.ZodType<IRTarget> = z
  .object({
    rootNamespace: z.array(z.string().min(1)).min(1),
    projectName: z.string().min(1),
    serviceName: z.string().min(1).optional(),
    description: z.string().optional(),
  })
  .strict();