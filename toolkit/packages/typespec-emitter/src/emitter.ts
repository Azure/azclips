/**
 * `$onEmit` orchestrator.
 *
 * Pipeline (matches the plan):
 *   1. createSdkContext  → TCGC-built type universe
 *   2. createBuildContext → caches, diag sink
 *   3. listAllServiceNamespaces → pick first, warn if >1
 *   4. buildSource        → IRSource + shared server definition + apiVersion
 *   5. operationsFrom     → IROperation[] (also discovers HTTP-part types)
 *   6. modelsFrom         → IRModel[]
 *   7. enumsFrom          → IREnum[]
 *   8. parseIR            → integrity check (throws on bug)
 *   9. writeIR            → js-yaml / JSON serialize + emitFile
 *  10. surface diagnostics; honour `diagnosticsAsErrors`
 */

import type { EmitContext } from "@typespec/compiler";
import { getNamespaceFullName, NoTarget } from "@typespec/compiler";
import {
  createSdkContext,
  listAllServiceNamespaces,
} from "@azure-tools/typespec-client-generator-core";
import type {
  SdkHttpOperation,
} from "@azure-tools/typespec-client-generator-core";
import {
  IR_SCHEMA_VERSION,
  parseIR,
  type IR,
} from "@azure-client-engine/ir";

import { resolveOptions, type EmitterOptions } from "./options.js";
import { createBuildContext, type CanonicalSourceObserver } from "./context.js";
import { modelsFrom } from "./models.js";
import { enumsFrom } from "./enums.js";
import { operationsFrom } from "./operations.js";
import { buildSource } from "./source.js";
import { writeIR } from "./writer.js";
import { $lib, reportDiagnostic } from "./lib.js";

export async function $onEmit(context: EmitContext<EmitterOptions>): Promise<void> {
  await emitWithSources(context);
}

/** Build-time observation requires a complete selected-version projection; ordinary IR is unchanged. */
export async function emitWithSources(
  context: EmitContext<EmitterOptions>,
  observeSource?: CanonicalSourceObserver,
): Promise<IR | undefined> {
  const options = resolveOptions(context.options);
  const selector = context.options["api-version"];
  const projectionError = (detail: string): void =>
    reportDiagnostic(context.program, { code: "source-projection-incomplete", format: { detail }, target: NoTarget });
  if (observeSource &&
      (!selector || !selector.trim() || ["latest", "all", "unknown"].includes(selector.toLowerCase()))) {
    projectionError("Source observation requires an explicit, concrete api-version; latest/all/default selection is not supported");
    return;
  }

  const sdkContext = await createSdkContext<Record<string, any>, SdkHttpOperation>(
    context as unknown as EmitContext<Record<string, any>>,
    options.emitterName,
    { exportTCGCoutput: false },
  );

  const services = listAllServiceNamespaces(sdkContext);
  if (services.length === 0) {
    // Nothing to emit; bail with a diagnostic.
    reportDiagnostic(context.program, {
      code: "cannot-resolve-endpoint",
      format: { source: "<no service>" },
      target: NoTarget,
    });
    return;
  }
  const serviceNs = services[0]!;
  if (services.length > 1) {
    if (observeSource) {
      projectionError("Multiple services are not supported by the current canonical IR projection");
      return;
    }
    reportDiagnostic(context.program, {
      code: "multiple-services-found",
      format: { count: String(services.length), chosen: serviceNs.name ?? "<unknown>" },
      target: NoTarget,
    });
  }

  // Build context. sourceId defaults to the service namespace FQN.
  const fqn = getNamespaceFullName(serviceNs);
  const buildCtx = createBuildContext(sdkContext, {
    sourceId: options.sourceId ?? fqn,
    diagnosticsAsErrors: options.diagnosticsAsErrors,
  }, observeSource);

  const { source, apiVersion, server } = buildSource(buildCtx, serviceNs);
  if (observeSource && (apiVersion.value !== selector || sdkContext.apiVersion !== selector)) {
    projectionError(`Requested version '${selector}' did not resolve to the same SDK/operation version ('${sdkContext.apiVersion}'/'${apiVersion.value}')`);
    return;
  }

  const operations = operationsFrom(buildCtx, apiVersion, server.id);
  const models = modelsFrom(buildCtx);
  const enums = enumsFrom(buildCtx);

  const ir: IR = {
    schemaVersion: IR_SCHEMA_VERSION,
    sources: [source],
    servers: [server],
    models,
    enums,
    discriminatedUnions: [...buildCtx.discriminatedUnions.values()],
    operations,
  };
  if (observeSource) {
    const lossyCodes = new Set([
      "unsupported-type-kind", "union-non-mappable", "unsupported-enum-value-type",
      "unsupported-multi-content-body", "cookie-parameter-dropped", "streaming-body-dropped", "override-method-skipped",
    ]);
    const prefix = `${$lib.name}/`;
    const failures = [
      ...sdkContext.diagnostics.filter((diagnostic) => diagnostic.severity === "error"),
      ...context.program.diagnostics.filter((diagnostic) =>
        diagnostic.severity === "error" || (diagnostic.code.startsWith(prefix) && lossyCodes.has(diagnostic.code.slice(prefix.length)))),
    ];
    if (failures.length > 0) {
      projectionError(`The source projection is incomplete: ${failures.map((diagnostic) => `${diagnostic.code}: ${diagnostic.message}`).join("; ")}`);
      return;
    }
  }

  // Integrity check. Re-throwing here surfaces emitter bugs immediately.
  try {
    parseIR(ir);
  } catch (err) {
    const detail = err instanceof Error ? err.message : String(err);
    reportDiagnostic(context.program, {
      code: "ir-validation-failed",
      format: { detail },
      target: NoTarget,
    });
    if (observeSource) return;
    // Still write the (broken) IR for diagnosis — the diagnostic surfaces the bug.
  }

  await writeIR(context.program, ir, {
    outputDir: context.emitterOutputDir,
    outputFile: options.outputFile,
    format: options.format,
    pretty: options.pretty,
  });

  // diagnosticsAsErrors: if requested, count the warning-severity diagnostics
  // *this emitter* produced (prefixed with our library name so we don't count
  // unrelated compiler/library warnings) and fail the emit if any fired.
  if (options.diagnosticsAsErrors) {
    const prefix = `${$lib.name}/`;
    const warningCount = context.program.diagnostics.filter(
      (d) => d.severity === "warning" && d.code.startsWith(prefix),
    ).length;
    if (warningCount > 0) {
      reportDiagnostic(context.program, {
        code: "diagnostics-as-errors",
        format: { count: String(warningCount) },
        target: NoTarget,
      });
    }
  }
  return context.program.hasError() ? undefined : ir;
}