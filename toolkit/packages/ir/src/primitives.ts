/**
 * Closed sets for primitive type kinds and serialization format hints.
 *
 * Closed by design: adding a new variant is a deliberate IR change, not
 * something the emitter can do via free-form strings. Generators switch
 * exhaustively on these, so additions ripple to downstream packages.
 */

export const PrimitiveKinds = [
  "string",
  "boolean",
  "int8",
  "int16",
  "int32",
  "int64",
  "uint8",
  "uint16",
  "uint32",
  "uint64",
  "float32",
  "float64",
  "decimal",
  "decimal128",
  "bytes",
  "any",
  "void",
] as const;

export type PrimitiveKind = (typeof PrimitiveKinds)[number];

/**
 * Wire-format hints attached to primitive properties, parameters, and
 * response headers. These tell the (de)serializer which lexical form to
 * use, independent of the underlying primitive kind.
 */
export const FormatKinds = [
  "date-time",
  "date",
  "time",
  "duration",
  "uri",
  "uuid",
  "ip-address",
  "hostname",
  "arm-id",
  "base64",
  "base64url",
  "byte-stream",
  "password",
] as const;

export type FormatKind = (typeof FormatKinds)[number];

/**
 * Generated-code visibility, from TCGC's resolved `.access` (core `@access`).
 * `public` = part of the client's public surface; `internal` = generated but
 * hidden. Omitted on IR nodes when `public` (the default).
 */
export const AccessKinds = ["public", "internal"] as const;

export type IRAccess = (typeof AccessKinds)[number];

/**
 * Data-flow usage flags, decoded from TCGC's numeric `UsageFlags` bitfield
 * (core/derived `@usage`). A type may carry several at once (e.g. a model used
 * in both a request and a response is `["input", "output", "json"]`). ace owns
 * these string tokens so the IR stays TypeSpec-free; the set mirrors TCGC's
 * `UsageFlags` members (except `None`, which is represented by omission).
 */
export const UsageKinds = [
  "input",
  "output",
  "apiVersionEnum",
  "jsonMergePatch",
  "multipartFormData",
  "spread",
  "json",
  "xml",
  "exception",
  "lroInitial",
  "lroPolling",
  "lroFinalEnvelope",
  "external",
] as const;

export type IRUsage = (typeof UsageKinds)[number];
