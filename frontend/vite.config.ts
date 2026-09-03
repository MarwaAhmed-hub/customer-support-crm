import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Makes /api same-origin in development, so the dev loop never exercises CORS.
    // The target port must match the backend's launchSettings.json.
    proxy: { "/api": { target: "http://localhost:5080", changeOrigin: true } },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
  },
});
