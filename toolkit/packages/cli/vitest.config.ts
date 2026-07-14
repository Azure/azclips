import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["test/**/*.test.ts"],
    // The happy-path test drives a real TypeSpec compile through the backbone
    // and emitter; give it the same budget the backbone's own e2e uses.
    testTimeout: 60_000,
    hookTimeout: 60_000,
  },
});
