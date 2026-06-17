/**
 * Logical-name casing.
 *
 * The emitter applies C#-style PascalCase to the *logical* (`name`) field of
 * model properties and response headers, matching how a C# generator would
 * surface them. The `wireName` always preserves the original on-the-wire
 * spelling, so serialization is unaffected.
 *
 * Operation names and operation parameter names are intentionally left
 * untouched (they round-trip as-is in the IR).
 */

/**
 * Convert an identifier to PascalCase.
 *
 * - Splits on `_`, `-`, `.`, and whitespace separators.
 * - Preserves existing intra-word humps, so camelCase survives:
 *   `nextLink` → `NextLink`, `newName` → `NewName`.
 * - Uppercases the first letter of every segment: `id` → `Id`,
 *   `created_at` → `CreatedAt`.
 */
export function pascalCase(name: string): string {
  if (name.length === 0) return name;
  const segments = name.split(/[_\-.\s]+/).filter((s) => s.length > 0);
  if (segments.length === 0) return name;
  return segments.map((s) => s.charAt(0).toUpperCase() + s.slice(1)).join("");
}
