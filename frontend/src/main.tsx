import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import "./index.css";

const container = document.getElementById("root");
if (container === null) {
  throw new Error("Root element #root is missing from index.html.");
}

// ThemeProvider/CssBaseline live inside App (ThemedApp) now, not here — Story 06's live branding
// theme needs BrandingContext, which itself needs AuthProvider, both of which App owns.
createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
