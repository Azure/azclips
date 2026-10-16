/**
 * Structured identity for every named IR entity (models, enums, properties,
 * operations, parameters). Identities are computed at emit time from the
 * declaration site's *original* names and are invariant under renaming
 * transforms; renames produce a new display `name` but leave the `id` untouched.
 *
 * Format:
 *   module     The originating TypeSpec module. `<root>` for the user's spec,
 *              `@scope/pkg-name` for library types, or a custom source id.
 *   namespace  The TypeSpec namespace path, split into segments.
 *   interface  The effective containing TypeSpec interface's original name,
 *              when present. SDK client names do not participate in identity.
 *   name       The terminal name. May contain `#` for synthesized inline names
 *              (e.g. `create#request.body` for an inline anonymous body type).
 *   typeArgs   For template instantiations (`Page<User>` => name: "Page",
 *              typeArgs: [<User's id>]). Recurses for nested instantiations.
 */

import { z } from "zod";

export type IRId = {
  module: string;
  namespace: string[];
  interface?: string | undefined;
  name: string;
  typeArgs?: IRId[] | undefined;
};

export const IRIdSchema: z.ZodType<IRId> = z.lazy(() =>
  z
    .object({
      module: z.string().min(1),
      namespace: z.array(z.string().min(1)),
      interface: z.string().min(1).optional(),
      name: z.string().min(1),
      typeArgs: z.array(IRIdSchema).optional(),
    })
    .strict(),
);

/**
 * Canonical string form, suitable for display. **Not** a lookup key: namespace
 * segments are joined with `.`, so `["A","B"]` and `["A.B"]` render identically
 * even though {@link idEquals} distinguishes them. Use {@link idKey} to index.
 *
 * NOT safe to use as a filesystem path; the `<`, `>`, `,`, `[`, `]`
 * characters appear for template instantiations and module brackets.
 * An interface scope uses `::` to distinguish it from a namespace segment.
 */
export function serializeId(id: IRId): string {
  const scope = id.namespace.join(".") + (id.interface !== undefined ? `::${id.interface}` : "");
  const path = scope.length ? `${scope}.${id.name}` : id.name;
  const args =
    id.typeArgs && id.typeArgs.length
      ? "<" + id.typeArgs.map(serializeId).join(", ") + ">"
      : "";
  return `[${id.module}] ${path}${args}`;
}

/**
 * Key for identity-based lookup tables.
 *
 * Deliberately separate from {@link serializeId}, which is for display and joins
 * namespace segments with `.` — so `["A","B"]` and `["A.B"]` collapse to the same
 * string even though {@link idEquals} treats them as different ids. A lookup key
 * has to agree with `idEquals` exactly, so this encodes the structure instead of
 * flattening it: two ids share a key if and only if `idEquals` is true.
 */
export function idKey(id: IRId): string {
  return JSON.stringify(idTuple(id));
}

/** Canonical array form: structural, so `JSON.stringify` of it is injective. */
type IdTuple = [string, string[], string | null, string, IdTuple[] | null];

function idTuple(id: IRId): IdTuple {
  return [
    id.module,
    id.namespace,
    // `interface` is `.min(1)`, so null is unambiguously "absent".
    id.interface ?? null,
    id.name,
    // An absent `typeArgs` is not the same id as an empty one, matching `idEquals`.
    id.typeArgs === undefined ? null : id.typeArgs.map(idTuple),
  ];
}

/**
 * Structural equality. Avoids the string allocations of `serializeId`
 * and short-circuits on the cheap module/name checks first, since most
 * non-matching ids differ in one of these.
 */
export function idEquals(a: IRId, b: IRId): boolean {
  if (a.module !== b.module || a.interface !== b.interface || a.name !== b.name) return false;
  if (a.namespace.length !== b.namespace.length) return false;
  for (let i = 0; i < a.namespace.length; i++) {
    if (a.namespace[i] !== b.namespace[i]) return false;
  }
  const at = a.typeArgs;
  const bt = b.typeArgs;
  if (at === undefined && bt === undefined) return true;
  if (at === undefined || bt === undefined) return false;
  if (at.length !== bt.length) return false;
  for (let i = 0; i < at.length; i++) {
    // safe: bounds checked
    if (!idEquals(at[i]!, bt[i]!)) return false;
  }
  return true;
}
