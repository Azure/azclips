/**
 * BuildContext: per-emit-run state passed to the per-kind converter modules.
 *
 * Holds:
 *   - The TCGC `SdkContext` (entry to the entire SDK type universe).
 *   - The resolved emitter options.
 *   - The id interner (`Map<SdkType, IRId>`): keeps referential identity
 *     across the IR so that any two pointers to the same underlying type
 *     compare equal via `idEquals`.
 *   - A property-id cache for `SdkModelPropertyType` so child property
 *     overrides can point at the parent property's id without recomputing.
 *   - A diagnostics sink and a small set of one-shot flags so we don't spam
 *     the user with the same "this is policy elsewhere" message per
 *     occurrence.
 */

import type {
  SdkContext,
  SdkHttpOperation,
  SdkModelPropertyType,
  SdkType,
} from "@azure-tools/typespec-client-generator-core";
import type { Program } from "@typespec/compiler";
import type { IRId } from "@azure-client-engine/ir";

export type BuildContextOptions = {
  /** Source id used as the default module string in IRIds. */
  sourceId: string;
  /** True if any warning should be elevated to an error at exit. */
  diagnosticsAsErrors: boolean;
};

export type BuildContext = {
  sdkContext: SdkContext<Record<string, any>, SdkHttpOperation>;
  program: Program;
  options: BuildContextOptions;

  /** Map from any SdkType the converter has seen to the IRId we produced. */
  idCache: Map<SdkType, IRId>;
  /** Property id interning so `.overrides` pointers stay stable. */
  propertyIdCache: Map<SdkModelPropertyType, IRId>;

  /** One-shot diagnostic flags. */
  flags: {
    overrideMethodWarned: boolean;
    openEnumWarned: boolean;
  };
};

export function createBuildContext(
  sdkContext: SdkContext<Record<string, any>, SdkHttpOperation>,
  options: BuildContextOptions,
): BuildContext {
  return {
    sdkContext,
    program: sdkContext.program,
    options,
    idCache: new Map(),
    propertyIdCache: new Map(),
    flags: {
      overrideMethodWarned: false,
      openEnumWarned: false,
    },
  };
}
