import {
  getDiscriminator, getDiscriminatedUnion, getDiscriminatedUnionFromInheritance,
  type Diagnostic, type Model, type Program,
} from "@typespec/compiler";

export function modelDiscriminator(program: Program, raw: Model) {
  const discriminator = getDiscriminator(program, raw);
  if (!discriminator) return undefined;
  const [resolved, diagnostics] = getDiscriminatedUnionFromInheritance(raw, discriminator);
  program.reportDiagnostics(diagnostics);
  return resolved;
}

export function reportSdkDiagnostics(program: Program, diagnostics: readonly Diagnostic[]): void {
  for (const diagnostic of diagnostics) {
    const target = diagnostic.target;
    if (typeof target === "object" && "kind" in target) {
      // These are SDK representation restrictions, not TypeSpec errors. The
      // emitter handles them from the compiler's validated discriminator map.
      if (diagnostic.code === "@azure-tools/typespec-client-generator-core/discriminator-not-constant"
        && target.kind === "Model") {
        let supported = false;
        for (let parent = target.baseModel; parent !== undefined; parent = parent.baseModel) {
          const discriminator = getDiscriminator(program, parent);
          if (!discriminator) continue;
          const [resolved, errors] = getDiscriminatedUnionFromInheritance(parent, discriminator);
          if (errors.length === 0 && [...resolved.variants.values()].includes(target)) supported = true;
        }
        if (supported) continue;
      }
      if ((diagnostic.code === "@azure-tools/typespec-client-generator-core/union-circular"
        || diagnostic.code === "@azure-tools/typespec-client-generator-core/union-null")
        && target.kind === "Union") {
        const [resolved, errors] = getDiscriminatedUnion(program, target);
        if (resolved && errors.length === 0) continue;
      }
    }
    program.reportDiagnostic(diagnostic);
  }
}
