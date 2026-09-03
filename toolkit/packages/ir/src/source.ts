/**
 * Source: one TypeSpec entrypoint (i.e. one resource provider) in a
 * multi-spec emit. Operations and models are tagged with `source`
 * pointing at one of these by id.
 *
 * Per-source defaults (apiVersion, server) are denormalized onto each
 * operation by a transform early in the pipeline; the source entries
 * remain the source of truth.
 */

import { z } from "zod";
import { IRServerSchema, IRApiVersionStampSchema, type IRServer, type IRApiVersionStamp } from "./operation.js";
import { IRAuthSchema, type IRAuth } from "./auth.js";

export type IRSource = {
  /** Stable identifier — e.g. "Microsoft.Storage". Doubles as IRId.module for types declared in this source. */
  id: string;
  /** Path to the source's TypeSpec entrypoint, for diagnostics. */
  entrypoint: string;
  /** Default api-version stamp for this source's operations. */
  apiVersion: IRApiVersionStamp;
  /** Resolved server for this source. */
  server: IRServer;
  /** TypeSpec root namespace (segments) owned by this source. Used to attribute models/ops to a source. */
  rootNamespace: string[];
  /** Service authentication requirement, if the service declares one. */
  auth?: IRAuth | undefined;
};

export const IRSourceSchema: z.ZodType<IRSource> = z
  .object({
    id: z.string().min(1),
    entrypoint: z.string().min(1),
    apiVersion: IRApiVersionStampSchema,
    server: IRServerSchema,
    rootNamespace: z.array(z.string().min(1)),
    auth: IRAuthSchema.optional(),
  })
  .strict();
