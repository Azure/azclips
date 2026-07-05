import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["test/**/*.test.ts"],
    // The backbone compiles real TypeSpec libraries through the emitter; give
    // each compile room (mirrors the emitter's own e2e budget).
    testTimeout: 60_000,
    hookTimeout: 60_000,
  },
});
