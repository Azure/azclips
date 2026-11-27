import assert from "node:assert/strict";
import { readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { ObjectTypePropertyFlags } from "@azure/bicep-types";

function identifier(value) {
  assert.ok(typeof value === "string" && /^[a-zA-Z_][a-zA-Z0-9_]*$/.test(value),
    `Unsupported Bicep identifier: ${value}`);
  return value;
}

export function generateTemplates(bridge) {
  assert.equal(bridge.schemaVersion, "1.0.0", "Unsupported binding schema");
  assert.equal(bridge.resourceType, "Microsoft.Network/publicIPAddresses@2026-03-01", "Unexpected pilot resource/version");
  const root = new Map();
  const bindings = [];
  const names = new Set(["publicip"]);
  let resourceNames = 0;
  for (const binding of bridge.bindings) {
    const { role, eligibility, deploymentParameter: name, bicepPath: path } = binding;
    if (role === "field" && eligibility === "excluded" ||
        ["computedIdentity", "readOnly"].includes(role) && eligibility === "reject" ||
        ["scope", "protocol"].includes(role) && eligibility === "structural") {
      assert.equal(name, undefined, "Excluded or transport inputs must not have template parameters");
      continue;
    }
    assert.ok(role === "field" && eligibility === "eligible" || role === "resourceName" && eligibility === "structural",
      `Unaccounted binding: ${binding.parameter}`);
    identifier(name);
    const key = name.toLowerCase();
    assert.ok(!names.has(key), `Duplicate or reserved deployment parameter: ${name}`);
    names.add(key);
    assert.equal(binding.nullable, false, "Nullable source inputs cannot use null as an omission sentinel");
    assert.equal(typeof binding.required, "boolean", "Binding requiredness is missing");
    assert.ok(Array.isArray(path) && path.length > 0, "A template binding path is missing");
    path.forEach(identifier);
    assert.equal(binding.bicepField.name, path.at(-1), "Stale Bicep field path");
    assert.equal(binding.bicepField.flags & ObjectTypePropertyFlags.ReadOnly, 0, "Read-only fields cannot be assigned");
    if (role === "resourceName") {
      resourceNames++;
      assert.equal(binding.required, true, "Resource name must be required");
      assert.deepEqual(path, ["name"], "Only one root resource name is supported");
    } else {
      assert.equal(typeof binding.normalizedRequired, "boolean", "Normalized input requiredness is missing");
      assert.ok(!binding.required || binding.normalizedRequired, "Required source inputs cannot become optional");
    }
    assert.ok(Object.keys(binding.constraints ?? {}).every((key) => ["target", "typePath"].includes(key)),
      "Additional source constraints require explicit template support");

    let node = root;
    for (const segment of path) {
      assert.ok(!node.binding, `Overlapping template binding path: ${path.join(".")}`);
      if (!node.has(segment)) node.set(segment, new Map());
      node = node.get(segment);
    }
    assert.ok(node.size === 0 && !node.binding, `Duplicate or overlapping template binding path: ${path.join(".")}`);
    node.binding = binding;
    bindings.push(binding);
  }
  assert.equal(resourceNames, 1, "Exactly one resource name binding is required");
  assert.ok(bindings.length > 1, "No eligible resource fields were bound");

  const required = (binding) => binding.role === "resourceName" || binding.normalizedRequired;
  const presence = (node) => {
    if (node.binding) return required(node.binding) ? "true" : `${node.binding.deploymentParameter} != null`;
    const children = [...node.values()].map(presence);
    return children.includes("true") ? "true" : children.map((condition) => `(${condition})`).join(" || ");
  };
  const optionalRoot = bindings.filter((binding) => binding.bicepPath.length === 1 && !required(binding));
  assert.equal(optionalRoot.length, 1, "The pilot requires exactly one optional root field for its two variants");
  for (const child of root.values()) {
    assert.ok(child.binding || presence(child) === "true", "Optional root containers require another variant design");
  }
  const render = (node, depth, includeRootField) => {
    const lines = [];
    const indent = "  ".repeat(depth);
    for (const [field, child] of node) {
      if (depth === 1 && child.binding === optionalRoot[0] && !includeRootField) continue;
      const condition = presence(child);
      const conditional = depth > 1 && condition !== "true";
      if (conditional) lines.push(`${indent}...((${condition}) ? {`);
      const prefix = conditional ? indent + "  " : indent;
      if (child.binding) lines.push(`${prefix}${field}: ${child.binding.deploymentParameter}`);
      else lines.push(`${prefix}${field}: {`, ...render(child, depth + (conditional ? 2 : 1), includeRootField), `${prefix}}`);
      if (conditional) lines.push(`${indent}} : {})`);
    }
    return lines;
  };
  return Object.fromEntries([["TagsAbsent", false], ["TagsPresent", true]].map(([variant, includeRootField]) => {
    const parameters = [];
    for (const binding of bindings) {
      if (binding === optionalRoot[0] && !includeRootField) continue;
      if (binding.type.kind === "primitive" && binding.type.name === "int32")
        parameters.push("@minValue(-2147483648)", "@maxValue(2147483647)");
      // Source defaults belong to customization; null only represents an omitted non-nullable nested field.
      parameters.push(`param ${binding.deploymentParameter} resourceInput<'${bridge.resourceType}'>.${binding.bicepPath.join(".")}${
        required(binding) || binding === optionalRoot[0] ? "" : "?"}`, "");
    }
    return [`PublicIpBicepCreate.${variant}.bicep`, [
      "// Generated from the source-aware Public IP bridge; do not edit.",
      "targetScope = 'resourceGroup'", "",
      ...parameters,
      `resource publicIp '${bridge.resourceType}' = {`,
      ...render(root, 1, includeRootField),
      "}", "",
    ].join("\n")];
  }));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [input, output, ...rest] = process.argv.slice(2);
  assert.ok(input && output && rest.length === 0, "Use generate-template.mjs <bindings.json> <existing-output-directory>");
  const templates = generateTemplates(JSON.parse(readFileSync(resolve(input), "utf8")));
  for (const [name, source] of Object.entries(templates)) {
    const path = resolve(output, name);
    writeFileSync(path, source);
    console.log(`Generated bridge-derived Public IP template: ${path}`);
  }
}
