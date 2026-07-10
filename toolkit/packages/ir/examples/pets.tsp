// Sample TypeSpec spec used to demonstrate how the IR schema maps real
// inputs into nodes. Paired with `pets.ir.json` in this folder. Intent:
// touch every interesting variant in the IR (polymorphism with multi-level
// inheritance + discriminator override, collections, pageable semantics,
// LRO semantics, response headers, body-flatten hint) in one small spec.

import "@typespec/http";
import "@typespec/rest";
import "@typespec/versioning";
import "@azure-tools/typespec-azure-core";

using TypeSpec.Http;
using TypeSpec.Rest;
using TypeSpec.Versioning;
using Azure.Core;

@service(#{ title: "Pets" })
@server("https://pets.example.com", "Pets endpoint")
@versioned(Versions)
namespace PetStore;

enum Versions {
  v2025_01_01: "2025-01-01",
}

@discriminator("kind")
model Pet {
  @key id: string;
  name: string;
  kind: string;
}

model Dog extends Pet {
  kind: "dog";
  breed: string;
}

model Cat extends Pet {
  kind: "cat";
  indoor: boolean;
}

model PetPage {
  @pageItems items: Pet[];
  @nextLink nextLink?: url;
}

model RenameRequest {
  newName: string;
}

// Terminal-state-bearing status monitor so `@pollingOperation` resolves to a
// valid LRO (Azure.Core requires the polled operation to return a model with a
// `status` field carrying terminal success/failure values).
enum OperationState {
  NotStarted,
  Running,
  Succeeded,
  Failed,
  Canceled,
}

model PetOperationStatus {
  @key operationId: string;
  status: OperationState;
}

@route("/pets")
interface Pets {
  // Pageable: maps to IROperationSemantics { kind: "pageable" }.
  @get list(@query tag?: string): PetPage;

  // Plain read.
  @get get(@path petId: string): Pet;

  // Plain create.
  @post create(@body pet: Pet): Pet;

  // Status monitor polled by the LRO below.
  @route("/operations/{operationId}")
  @get
  getStatus(@path operationId: string): PetOperationStatus;

  // LRO: 202 + operation-location header, polled via Pets.getStatus for final
  // state. Maps to IROperationSemantics { kind: "longRunning" }.
  @route("{petId}:rename")
  @post
  @pollingOperation(Pets.getStatus)
  rename(@path petId: string, @body body: RenameRequest): {
    @statusCode _: 202;
    @header("operation-location") opLoc: string;
  };
}
