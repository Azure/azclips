/**
 * Library definition for the Azure Client Engine TypeSpec emitter.
 *
 * Houses:
 *   - The declared diagnostic codes (mapped to `reportDiagnostic` / `createDiagnostic`).
 *   - The JSON-Schema for `EmitterOptions` (consumed by `tsp compile` for
 *     `tspconfig.yaml` validation BEFORE `$onEmit` runs).
 *
 * # Explicit deferrals (the IR doesn't carry these yet; transformers / IR
 *   bumps will pick them up later):
 *
 *   - Examples (`@opExample`): IR has no `IRExample` node.
 *   - Primitive constraints (`@minLength`, `@pattern`, etc.): dropped
 *     silently; the transformer layer (or a future IR bump) is the right
 *     home for these.
 *   - Cookies, tuples, streaming bodies: each diagnosed and skipped.
 *   - Multi-content-type bodies: first content-type wins; rest warned.
 *   - Override methods (`SdkServiceMethod.isOverride`): skipped.
 */

import {
  createTypeSpecLibrary,
  paramMessage,
  type JSONSchemaType,
} from "@typespec/compiler";
import type { EmitterOptions } from "./options.js";

const EmitterOptionsSchema: JSONSchemaType<EmitterOptions> = {
  type: "object",
  additionalProperties: false,
  properties: {
    outputFile: { type: "string", nullable: true },
    format: { type: "string", enum: ["yaml", "json"], nullable: true },
    pretty: { type: "boolean", nullable: true },
    emitterName: { type: "string", nullable: true },
    sourceId: { type: "string", nullable: true },
    "api-version": { type: "string", nullable: true },
    diagnosticsAsErrors: { type: "boolean", nullable: true },
  },
  required: [],
};

export const $lib = createTypeSpecLibrary({
  name: "@azure-client-engine/typespec-emitter",
  diagnostics: {
    "paging-property-unbound": {
      severity: "warning",
      messages: {
        default: paramMessage`Paging property '${"property"}' on operation '${"operation"}' has no resolved HTTP binding; its source metadata is retained.`,
      },
    },
    "paging-items-not-array": {
      severity: "error",
      messages: {
        default: paramMessage`Paging items on operation '${"operation"}' must resolve to an array.`,
      },
    },
    "unsupported-type-kind": {
      severity: "warning",
      messages: {
        default: paramMessage`Unsupported SDK type kind '${"kind"}' at ${"where"}; substituting 'any'.`,
      },
    },
    "unsupported-multi-content-body": {
      severity: "warning",
      messages: {
        default: paramMessage`Operation '${"operation"}' body has multiple content-types (${"contentTypes"}). First will be used; others dropped.`,
      },
    },
    "missing-api-version": {
      severity: "warning",
      messages: {
        default: paramMessage`No api-version parameter found for source '${"source"}'; emitting a placeholder stamp.`,
      },
    },
    "inconsistent-api-version-location": {
      severity: "warning",
      messages: {
        default: paramMessage`Operations in source '${"source"}' disagree on api-version location/name; using '${"chosen"}'.`,
      },
    },
    "ambiguous-polling-strategy": {
      severity: "warning",
      messages: {
        default: paramMessage`LRO operation '${"operation"}' has an ambiguous statusMonitor step; falling back to 'custom' with path '${"path"}'.`,
      },
    },
    "unknown-final-state-via": {
      severity: "warning",
      messages: {
        default: paramMessage`LRO operation '${"operation"}' reports finalStateVia='${"value"}', which is outside the IR enum; omitting.`,
      },
    },
    "union-non-mappable": {
      severity: "warning",
      messages: {
        default: paramMessage`Union type '${"name"}' is heterogeneous and not discriminated/enum-like; substituting 'any'.`,
      },
    },
    "cannot-resolve-endpoint": {
      severity: "error",
      messages: {
        default: paramMessage`Could not resolve any @server endpoint for source '${"source"}'.`,
      },
    },
    "cookie-parameter-dropped": {
      severity: "warning",
      messages: {
        default: paramMessage`Cookie parameter '${"name"}' on operation '${"operation"}' dropped (IR has no cookie location).`,
      },
    },
    "streaming-body-dropped": {
      severity: "warning",
      messages: {
        default: paramMessage`Operation '${"operation"}' has a streaming body; operation skipped.`,
      },
    },
    "multiple-services-found": {
      severity: "warning",
      messages: {
        default: paramMessage`Compilation contains ${"count"} service namespaces; only '${"chosen"}' will be emitted.`,
      },
    },
    "anonymous-model-no-name": {
      severity: "warning",
      messages: {
        default: paramMessage`Anonymous model encountered at ${"where"}; using synthesized name '${"name"}'.`,
      },
    },
    "discriminator-property-not-found": {
      severity: "error",
      messages: {
        default: paramMessage`Model '${"model"}' declares discriminatedSubtypes but the discriminator property could not be resolved.`,
      },
    },
    "inheritance-chain-cycle": {
      severity: "error",
      messages: {
        default: paramMessage`Inheritance chain cycle detected involving model '${"model"}'.`,
      },
    },
    "override-method-skipped": {
      severity: "warning",
      messages: {
        default: `Override methods (SdkServiceMethod.isOverride) are not emitted; skipping.`,
      },
    },
    "open-enum-as-closed": {
      severity: "warning",
      messages: {
        default: paramMessage`Extensible enum '${"enum"}' was emitted as a regular IR enum with isFixed=false; consumers must preserve its known values while accepting unknown values.`,
      },
    },
    "ir-validation-failed": {
      severity: "error",
      messages: {
        default: paramMessage`Constructed IR failed schema validation: ${"detail"}`,
      },
    },
    "diagnostics-as-errors": {
      severity: "error",
      messages: {
        default: paramMessage`diagnosticsAsErrors is set and ${"count"} warning-severity diagnostic(s) were reported by the emitter; failing the emit.`,
      },
    },
  },
  emitter: {
    options: EmitterOptionsSchema,
  },
});

export const { reportDiagnostic, createDiagnostic } = $lib;
