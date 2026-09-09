import "@typespec/http";
import "@azure-tools/typespec-client-generator-core";

using TypeSpec.Http;
using Azure.ClientGenerator.Core;

@service
@server("https://discriminators.example.test", "Discriminator service")
@clientNamespace("Generated.Placement")
namespace DiscriminatorService;

@discriminator("kind")
model Animal {
  @clientName("animalKind")
  @encodedName("application/json", "@kind")
  kind: string;
  label: string;
}
model Cat extends Animal {
  @clientName("animalKind")
  @encodedName("application/json", "@kind")
  kind: "cat";
  lives: int32;
}
model Dog extends Animal {
  @clientName("animalKind")
  @encodedName("application/json", "@kind")
  kind: "dog";
}
@route("/animal") @get op animal(): Animal;

@discriminator("kind") model Root { kind: string; }
model Middle extends Root { middle: string; }
model Leaf extends Middle { kind: "leaf"; value: int32; }
@route("/root") @get op root(): Root;

@discriminator("kind") model NoVariants {}
@route("/empty") @get op empty(): NoVariants;

@discriminator("kind") model ImplicitBase { label: string; }
model ImplicitChild extends ImplicitBase { kind: "implicit"; }
@route("/implicit") @get op implicit(): ImplicitBase;

@discriminator("kind") model AliasedBase { kind: string; }
model AliasedChild extends AliasedBase { kind: "first" | "second"; }
@route("/aliases") @get op aliases(): AliasedBase;

enum AnimalTag { cat: "kitten", empty: "" }
@discriminator("kind") model EnumBase { kind: AnimalTag; }
model EnumChild extends EnumBase { kind: AnimalTag.cat; }
model EmptyTagChild extends EnumBase { kind: AnimalTag.empty; }
@route("/enum") @get op enumTags(): EnumBase;

@discriminator("kind") model Event { kind: string; }
@discriminator("category") model Categorized extends Event { kind: "categorized"; category: string; }
model Audit extends Categorized { category: "audit"; }
@route("/events") @get op events(): Event;

model CatData { lives: int32; }
model DogData { bark: string; }
model Fallback { extra?: string; }

@doc("An enveloped choice.")
@discriminated(#{ discriminatorPropertyName: "type", envelopePropertyName: "data" })
union ObjectChoice {
  @doc("The cat case.") cat: CatData,
  Fallback,
  dog: DogData,
}

@discriminated(#{ envelope: "none", discriminatorPropertyName: "tag" })
union FlatChoice { cat: CatData, "": DogData, Fallback }

@discriminated
union SingleChoice { only: CatData }

@discriminated
union NullableChoice { cat: CatData, none: null }

@discriminated
union NullOnlyChoice { none: null }

@discriminated
union LiteralChoice { up: "UP", down: "DOWN" }

@discriminated
union AliasChoice { picked: LiteralChoice.up, other: Fallback }

@discriminated
union ConstantChoice { yes: true, count: 42, tag: AnimalTag.cat, Fallback }

@discriminated
union NumericChoice { large: 9007199254740993, fraction: 1.5 }

@discriminated
union NullablePayloadChoice { maybe: CatData | null, dog: DogData }

@discriminated
union NullDefaultChoice { cat: CatData, null }

@discriminated
union NestedChoice { wrapped: ObjectChoice, leaf: CatData }

@discriminator("kind") model LazyBase { kind: string; }
model LazyChild extends LazyBase { kind: "lazy-a" | "lazy-b"; }

@discriminated
union RecursiveChoice { next: RecursiveChoice, end: CatData, lazy: LazyBase }

@discriminated
union TreeChoice { branch: Branch, leaf: CatData }
model Branch { children: TreeChoice[]; }
union UntaggedRecursive { next: UntaggedRecursive, leaf: CatData }

namespace Nested {
  @discriminated
  union ObjectChoice { other: DogData }
}

model Holder {
  noVariants: NoVariants;
  objectChoice: ObjectChoice;
  repeated: ObjectChoice[];
  flat: FlatChoice;
  single: SingleChoice;
  nullableCase: NullableChoice;
  nullableEnvelope: ObjectChoice | null;
  nullOnly: NullOnlyChoice;
  literal: LiteralChoice;
  variantReference: LiteralChoice.up;
  aliasChoice: AliasChoice;
  constant: ConstantChoice;
  numeric: NumericChoice;
  nullablePayload: NullablePayloadChoice;
  defaultNull: NullDefaultChoice;
  nested: NestedChoice;
  recursive: RecursiveChoice;
  tree: TreeChoice;
  scoped: Nested.ObjectChoice;
  ordinaryUnion: CatData | DogData;
  ordinaryNullable: CatData | null;
}

@route("/holder") @post op holder(@body body: Holder): Holder;
@route("/direct") @post op direct(@body body: NullableChoice): ObjectChoice;

model ChoiceForm { choice: HttpPart<NullableChoice>; }
@route("/multipart") @post op multipart(@multipartBody body: ChoiceForm): void;
