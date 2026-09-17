import type { IRLiteralValue } from "@azure-client-engine/ir";

export function literalFromValue(value: string | number | boolean | bigint | null): IRLiteralValue {
  switch (typeof value) {
    case "string":
      return { kind: "string", value };
    case "boolean":
      return { kind: "boolean", value };
    case "number":
      return { kind: "number", value };
    case "bigint":
      return { kind: "bigint", value: value.toString(10) };
    default:
      return { kind: "null" };
  }
}
