import type { ReactNode } from "react";
import { PermissionRoute } from "./PermissionRoute";

/**
 * Story 03: authorization moved from a single hard-coded admin flag to the permissions model.
 * `AdminRoute` is kept only so `/users/*` routes (App.tsx) don't need touching, and delegates to
 * `PermissionRoute` gated on `users.view` — the closest equivalent of "can see the admin area" now
 * that "admin" isn't a real concept on the frontend.
 *
 * TODO(roles-permissions follow-up): callers should migrate to `<PermissionRoute required="...">`
 * directly with the permission each route actually needs, and this wrapper can then be removed.
 */
export function AdminRoute({ children }: { children: ReactNode }) {
  return <PermissionRoute required="users.view">{children}</PermissionRoute>;
}
