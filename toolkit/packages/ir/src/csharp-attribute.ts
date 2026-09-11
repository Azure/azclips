import { z } from "zod";

export type CSharpAttributeTarget = "Assembly" | "Class" | "Property" | "Field";
export type CSharpAttributeValueType = "string" | "boolean" | "integer" | "string[]";
export type CSharpAttributeType = CSharpAttributeValueType | { clr: string };

export type CSharpAttributeProjection = {
  name: string;
  targets: CSharpAttributeTarget[];
  allowMultiple: boolean;
  inherited: boolean;
  enums: Array<{ name: string; members: readonly string[] }>;
  constructor: Array<
    | { name: string; type: CSharpAttributeValueType }
    | { name: string; source: string }
  >;
  properties: Array<
    | { name: string; source: string; type?: CSharpAttributeType | undefined; default?: string | undefined; required?: boolean | undefined }
    | { name: string; type: CSharpAttributeType; default?: string | undefined; required?: boolean | undefined }
  >;
};

type AttributeField =
  | { kind: "constructor"; name?: string | undefined }
  | {
      kind: "property";
      name?: string | undefined;
      type?: CSharpAttributeType | undefined;
      default?: string | undefined;
      required?: boolean | undefined;
    }
  | { kind: "omit" };

type ProjectedField = {
  schema: z.ZodTypeAny;
  attribute: AttributeField;
};

type ZodShape<TFields extends Record<string, ProjectedField>> = {
  [TKey in keyof TFields]: TFields[TKey]["schema"];
};

export function defineCSharpAttribute<const TFields extends Record<string, ProjectedField>>(
  name: string,
  fields: TFields,
  options: {
    targets: CSharpAttributeTarget[];
    allowMultiple?: boolean | undefined;
    inherited?: boolean | undefined;
    context?: Array<{ name: string; type: CSharpAttributeValueType }> | undefined;
    enums?: Array<{ name: string; members: readonly string[] }> | undefined;
    properties?: Array<{
      name: string;
      type: CSharpAttributeType;
      default?: string | undefined;
      required?: boolean | undefined;
    }> | undefined;
  },
): { schema: z.ZodObject<ZodShape<TFields>>; projection: CSharpAttributeProjection } {
  const shape = Object.fromEntries(Object.entries(fields).map(([fieldName, field]) => [fieldName, field.schema])) as ZodShape<TFields>;
  const constructor: CSharpAttributeProjection["constructor"] = [...(options.context ?? [])];
  const properties: CSharpAttributeProjection["properties"] = [...(options.properties ?? [])];

  for (const [source, field] of Object.entries(fields)) {
    switch (field.attribute.kind) {
      case "constructor":
        constructor.push({ name: field.attribute.name ?? source, source });
        break;
      case "property":
        properties.push({
          name: field.attribute.name ?? pascalCase(source),
          source,
          ...(field.attribute.type === undefined ? {} : { type: field.attribute.type }),
          ...(field.attribute.default === undefined ? {} : { default: field.attribute.default }),
          ...(field.attribute.required === undefined ? {} : { required: field.attribute.required }),
        });
        break;
    }
  }

  return {
    schema: z.object(shape),
    projection: {
      name,
      targets: options.targets,
      allowMultiple: options.allowMultiple ?? false,
      inherited: options.inherited ?? false,
      enums: options.enums ?? [],
      constructor,
      properties,
    },
  };
}

function pascalCase(value: string): string {
  return value.length === 0 ? value : value.charAt(0).toUpperCase() + value.slice(1);
}