import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./tests/unit",
  fullyParallel: false,
  workers: 1,
  reporter: "line",
});
