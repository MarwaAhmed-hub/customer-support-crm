import { createTheme } from "@mui/material/styles";
import type { Theme } from "@mui/material/styles";

/**
 * One shared theme so every page gets the same colors, spacing, and control shapes for free.
 * Deliberately small — this is a look-and-feel pass only, not a design system.
 *
 * `buildTheme` (Story 06) takes the branding primary/secondary color over the hard-coded defaults
 * below, so `BrandingContext`'s fetched settings can drive the live MUI theme. Called with no
 * arguments it reproduces the exact pre-Story-06 theme.
 */
export function buildTheme(primaryColor?: string, secondaryColor?: string): Theme {
  return createTheme({
    palette: {
      mode: "light",
      primary: { main: primaryColor ?? "#2563eb" },
      ...(secondaryColor ? { secondary: { main: secondaryColor } } : {}),
      background: { default: "#f4f6f8", paper: "#ffffff" },
      text: { primary: "#111827", secondary: "#5b6472" },
    },
    shape: { borderRadius: 10 },
    typography: {
      fontFamily: [
        "system-ui",
        "-apple-system",
        "BlinkMacSystemFont",
        '"Segoe UI"',
        "Roboto",
        "sans-serif",
      ].join(","),
      h4: { fontWeight: 700 },
      h5: { fontWeight: 700 },
      h6: { fontWeight: 600 },
    },
    components: {
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: { root: { textTransform: "none", fontWeight: 600 } },
      },
      MuiPaper: {
        styleOverrides: { root: { backgroundImage: "none" } },
      },
      MuiAppBar: {
        styleOverrides: {
          root: { backgroundColor: "#ffffff", color: "#111827" },
        },
      },
      MuiTableCell: {
        styleOverrides: { head: { fontWeight: 700, color: "#374151" } },
      },
    },
  });
}

/** The pre-Story-06 default theme, kept for any call site that has no branding context available. */
export const theme = buildTheme();
