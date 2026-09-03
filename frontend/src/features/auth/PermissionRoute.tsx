import { Box, CircularProgress } from "@mui/material";
import { Navigate, useLocation } from "react-router-dom";
import type { ReactNode } from "react";
import { AppLayout } from "../../components/AppLayout";
import { useAuth } from "./useAuth";

/**
 * Modeled on `ProtectedRoute`/`AdminRoute` (Story 01/02): the anonymous/loading handling is
 * identical, with one addition — an authenticated caller missing the required permission(s) is
 * redirected to Home instead of rendering the guarded page. A string `required` checks a single
 * permission; an array checks "has at least one of these" (`hasAnyPermission`).
 */
export function PermissionRoute({ required, children }: { required: string | string[]; children: ReactNode }) {
  const { status, hasPermission, hasAnyPermission } = useAuth();
  const location = useLocation();

  if (status === "loading") {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", mt: 10 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (status === "anonymous") {
    return (
      <Navigate
        to="/login"
        replace
        state={{ from: location.pathname + location.search }}
      />
    );
  }

  const allowed = Array.isArray(required) ? hasAnyPermission(required) : hasPermission(required);
  if (!allowed) {
    return <Navigate to="/" replace />;
  }

  return <AppLayout>{children}</AppLayout>;
}
