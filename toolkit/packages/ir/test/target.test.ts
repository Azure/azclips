/**
 * Generation target.
 *
 * `target` is written by a transform, so it is optional on `IR` — whose required
 * fields are exactly what the emitter produces — and required on `TransformedIR`,
 * the document a generator receives.
 */

import { describe, expect, it } from "vitest";

import {
  emptyIR,
  parseIR,
  parseTransformedIR,
  safeParseIR,
  safeParseTransformedIR,
  type IR,
  type IRTarget,
} from "../src/index.js";

const SERVER = { module: "Service", namespace: ["Service"], name: "server" };

function document(): IR {
  return {
    ...emptyIR(),
    sources: [
      {
        id: "Service",
        entrypoint: "service.tsp",
        rootNamespace: ["Service"],
        apiVersion: { value: "2025-01-01", location: "query", parameterName: "api-version" },
        server: SERVER,
      },
    ],
    servers: [{ id: SERVER, endpoint: { kind: "literal", url: "https://service.example/" } }],
  };
}

const target: IRTarget = {
  rootNamespace: ["Contoso", "Service"],
  projectName: "Contoso.Service",
  directories: { models: "Models", processors: "Processors" },
};

describe("generation target", () => {
  it("is optional on IR, as the emitter cannot produce it", () => {
    expect(safeParseIR(document()).success).toBe(true);
  });

  it("can be carried on IR before any command exists", () => {
    expect(parseIR({ ...document(), target }).target).toEqual(target);
  });

  it("carries the extension's identity alongside its placement", () => {
    const identified: IRTarget = {
      ...target,
      serviceName: "Service",
      version: "0.1.0",
      description: "Service extension",
    };
    expect(parseIR({ ...document(), target: identified }).target).toEqual(identified);
  });

  it("rejects keys it does not declare", () => {
    expect(safeParseIR({ ...document(), target: { ...target, owner: "me" } }).success).toBe(false);
  });

  it("is still required on TransformedIR", () => {
    expect(safeParseTransformedIR({ ...document(), commands: [] }).success).toBe(false);
    expect(parseTransformedIR({ ...document(), commands: [], target }).target).toEqual(target);
  });
});
