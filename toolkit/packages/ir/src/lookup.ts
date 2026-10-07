/**
 * Identity lookup over a parsed IR document.
 *
 * The IR is flat arrays plus `IRId` references, so every consumer that resolves a
 * reference would otherwise scan — and several already hand-roll the same map. This
 * builds those maps once, keyed by {@link idKey} so lookup agrees with `idEquals`
 * exactly (a `serializeId`-keyed map does not: it flattens namespace segments).
 *
 * **A snapshot, not a live view.** Transforms mutate the IR in place, so an index
 * built before a stage adds or removes a node is stale. Build it inside the stage,
 * after any structural change, and do not cache it on the document.
 */

import { idKey, type IRId } from "./id.js";
import type { IRModel, IRProperty } from "./model.js";
import type { IREnum } from "./enum.js";
import type { IROperation } from "./operation.js";
import type { IRServerDefinition } from "./server.js";
import type { IRDiscriminatedUnion } from "./discriminated-union.js";
import type { IR } from "./ir.js";

/** Resolvers over one IR document. Every lookup returns `undefined` when unresolved. */
export interface IRIndex {
  model(id: IRId): IRModel | undefined;
  enum(id: IRId): IREnum | undefined;
  operation(id: IRId): IROperation | undefined;
  server(id: IRId): IRServerDefinition | undefined;
  discriminatedUnion(id: IRId): IRDiscriminatedUnion | undefined;
  /**
   * Property by id, across every model. Properties are not a top-level collection,
   * so this is the only way to resolve one — which flattening paths need, since
   * they are property ids.
   */
  property(id: IRId): IRProperty | undefined;
  /** The model that declares a property, for walking a flattened path back to its owner. */
  ownerOf(propertyId: IRId): IRModel | undefined;
}

/** Build an {@link IRIndex} over `ir`. Linear in the document; call once per stage. */
export function indexIR(ir: IR): IRIndex {
  const models = new Map<string, IRModel>();
  const enums = new Map<string, IREnum>();
  const operations = new Map<string, IROperation>();
  const servers = new Map<string, IRServerDefinition>();
  const unions = new Map<string, IRDiscriminatedUnion>();
  const properties = new Map<string, IRProperty>();
  const owners = new Map<string, IRModel>();

  for (const model of ir.models) {
    models.set(idKey(model.id), model);
    for (const property of model.properties) {
      const key = idKey(property.id);
      // First declaration wins: inherited properties are denormalized onto every
      // subtype, so the same id legitimately appears on several models.
      if (!properties.has(key)) {
        properties.set(key, property);
        owners.set(key, model);
      }
    }
  }
  for (const value of ir.enums) enums.set(idKey(value.id), value);
  for (const operation of ir.operations) operations.set(idKey(operation.id), operation);
  for (const server of ir.servers) servers.set(idKey(server.id), server);
  for (const union of ir.discriminatedUnions ?? []) unions.set(idKey(union.id), union);

  return {
    model: (id) => models.get(idKey(id)),
    enum: (id) => enums.get(idKey(id)),
    operation: (id) => operations.get(idKey(id)),
    server: (id) => servers.get(idKey(id)),
    discriminatedUnion: (id) => unions.get(idKey(id)),
    property: (id) => properties.get(idKey(id)),
    ownerOf: (id) => owners.get(idKey(id)),
  };
}
