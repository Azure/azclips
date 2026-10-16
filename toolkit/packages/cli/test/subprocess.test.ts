/**
 * Running tools as child processes (`subprocess.ts`): a reply, a child that exits
 * without one, the hang guard, cancellation, and the timeout setting.
 */

import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { runWorker, spawnStream, timeoutFromEnv } from "../src/subprocess.js";

const WORKER = join(dirname(fileURLToPath(import.meta.url)), "fixtures", "echo-worker.mjs");
const OPTIONS = { label: "test worker", timeoutMs: 30_000 };

describe("runWorker", () => {
  it("resolves with the worker's reply", async () => {
    await expect(runWorker(WORKER, { mode: "echo", value: 42 }, OPTIONS)).resolves.toEqual({ echoed: 42 });
  });

  it("rejects when the worker exits without replying", async () => {
    await expect(runWorker(WORKER, { mode: "exit" }, OPTIONS)).rejects.toThrow(
      "test worker exited (code 3) without a result.",
    );
  });

  it("kills a worker that runs past the hang guard", async () => {
    await expect(runWorker(WORKER, { mode: "hang" }, { ...OPTIONS, timeoutMs: 200 })).rejects.toThrow(
      "test worker timed out after 200ms and was terminated.",
    );
  });

  it("rejects with an AbortError when cancelled, before or after it starts", async () => {
    await expect(
      runWorker(WORKER, { mode: "echo" }, { ...OPTIONS, signal: AbortSignal.abort() }),
    ).rejects.toMatchObject({ name: "AbortError" });

    const controller = new AbortController();
    const running = runWorker(WORKER, { mode: "hang" }, { ...OPTIONS, signal: controller.signal });
    controller.abort();
    await expect(running).rejects.toMatchObject({ name: "AbortError" });
  });
});

describe("spawnStream", () => {
  it("hands each stdout line to the consumer, then reports the exit", async () => {
    const lines: string[] = [];
    const result = await spawnStream(
      process.execPath,
      ["-e", "process.stdin.pipe(process.stdout)"],
      "one\ntwo\n",
      (line) => {
        lines.push(line);
      },
      OPTIONS,
    );
    expect(lines).toEqual(["one", "two"]);
    expect(result.code).toBe(0);
  });

  it("rejects with an AbortError when cancelled", async () => {
    await expect(
      spawnStream(process.execPath, ["-e", ""], "", () => {}, { ...OPTIONS, signal: AbortSignal.abort() }),
    ).rejects.toMatchObject({ name: "AbortError" });
  });
});

describe("timeoutFromEnv", () => {
  it("reads a positive number of milliseconds, else falls back", () => {
    const name = "ACE_TEST_TIMEOUT_MS";
    try {
      process.env[name] = "1500";
      expect(timeoutFromEnv(name, 99)).toBe(1500);
      process.env[name] = "0";
      expect(timeoutFromEnv(name, 99)).toBe(99);
      process.env[name] = "soon";
      expect(timeoutFromEnv(name, 99)).toBe(99);
      delete process.env[name];
      expect(timeoutFromEnv(name, 99)).toBe(99);
    } finally {
      delete process.env[name];
    }
  });
});