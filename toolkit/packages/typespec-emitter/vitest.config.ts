import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    include: ["test/**/*.test.ts"],
    // The emitter compiles real TypeSpec libraries in a virtual fs; give it room.
    testTimeout: 60_000,
    hookTimeout: 60_000,
  },
});
