/**
 * Translate raw TypeSpec `Diagnostic`s into the backbone's structured,
 * serializable `CompilerDiagnostic`. This is the boundary that keeps
 * TypeSpec's types from leaking into the backbone's public API.
 *
 * Source location is best-effort: targets without a resolvable location
 * (e.g. `NoTarget`) simply omit `target`.
 */

import { NoTarget, getSourceLocation, type Diagnostic } from "@typespec/compiler";
import type { CompilerDiagnostic } from "./types.js";

export function toCompilerDiagnostic(diagnostic: Diagnostic): CompilerDiagnostic {
  const mapped: CompilerDiagnostic = {
    severity: diagnostic.severity,
    code: diagnostic.code,
    message: diagnostic.message,
  };

  const target = locationOf(diagnostic);
  if (target !== undefined) mapped.target = target;

  return mapped;
}

function locationOf(diagnostic: Diagnostic): CompilerDiagnostic["target"] {
  if (diagnostic.target === NoTarget) return undefined;

  try {
    const location = getSourceLocation(diagnostic.target);
    if (location === undefined) return undefined;
    const { line, character } = location.file.getLineAndCharacterOfPosition(location.pos);
    // TypeSpec reports 0-based line/character; expose 1-based for editors.
    return { file: location.file.path, line: line + 1, column: character + 1 };
  } catch {
    return undefined;
  }
}
