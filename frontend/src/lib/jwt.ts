/**
 * Reads the `role` claim out of a JWT's payload, without verifying the signature.
 *
 * This is safe only because the token itself is never trusted for authorization here — the
 * backend re-validates the signature on every request and is the actual source of truth
 * (`[Authorize(Policy = "Admin")]` on `UsersController`). This decode exists purely so the
 * frontend can decide, synchronously and without a round-trip, whether to show admin-only UI.
 * Getting it wrong only ever hides or shows a link; it can never grant real access.
 */
export function decodeJwtRole(token: string): string | null {
  try {
    const payloadSegment = token.split(".")[1];
    if (payloadSegment === undefined) return null;

    // base64url -> base64
    const base64 = payloadSegment.replaceAll("-", "+").replaceAll("_", "/");
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=");

    const json = atob(padded);
    const payload: unknown = JSON.parse(json);

    if (typeof payload !== "object" || payload === null || !("role" in payload)) {
      return null;
    }

    const role: unknown = Reflect.get(payload, "role");
    return typeof role === "string" ? role : null;
  } catch {
    // A malformed or unexpected token shape is treated as "no role" rather than thrown.
    return null;
  }
}
