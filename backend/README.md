# Customer Support CRM — Backend

ASP.NET Core 10 Web API. Story 01 covers **authentication and login only** — see
`.squad/plans/foundation-and-administration/01-story-authentication-login.md`.

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | **10.x** | `dotnet --version` |
| Node.js | **20.19+** | required by Vite 7; the frontend uses pnpm |
| `dotnet-ef` | **10.x** | `dotnet tool install --global dotnet-ef --version 10.*` |
| SQL Server | LocalDB or any reachable instance | see below |

### SQL Server

Local development expects **SQL Server LocalDB** at `(localdb)\MSSQLLocalDB`, which ships with the
Visual Studio *Data storage and processing* workload or the standalone *SQL Server Express LocalDB*
installer.

```
sqllocaldb info MSSQLLocalDB      # check
sqllocaldb start MSSQLLocalDB     # start
```

> **If the API fails to start with a `SqlException`, check this first.** `EnableRetryOnFailure`
> covers transient connection drops at request time — it does not start a stopped instance.

LocalDB is **Windows-only**. On macOS/Linux run SQL Server in Docker
(`mcr.microsoft.com/mssql/server:2022-latest`) and change `ConnectionStrings:Default` accordingly;
nothing else differs.

## Running

```
dotnet run --project src/CustomerSupportCrm.Api
```

Listens on `http://localhost:5080` (must match the frontend's Vite proxy target).

In **Development** the app applies migrations (`MigrateAsync`) and seeds one administrator on
startup. No other environment does either.

### Dev seed credentials — local development only

```
admin@local.test / Admin!23
```

`.test` is reserved by RFC 6761: it can never be registered and never resolves, so it is
unambiguous in logs and safe to commit. **Rotate or remove this account before any shared
environment.** Production admin provisioning is out of scope for Story 01 — until Story 02 ships
user management, create the first user manually (`dotnet ef database update`, then an INSERT with a
hash produced by the same `PasswordHasher<User>`).

## Database

Development uses `CustomerSupportCrmDev` on `(localdb)\MSSQLLocalDB`.

```
# clean slate
dotnet ef database drop --force --project src/CustomerSupportCrm.Infrastructure \
                                --startup-project src/CustomerSupportCrm.Api
# or
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "DROP DATABASE CustomerSupportCrmDev"
```

The next Development start recreates and reseeds it.

Migrations are applied automatically **only** in Development. Every other environment runs them
explicitly before the app starts:

```
dotnet ef database update --project src/CustomerSupportCrm.Infrastructure \
                          --startup-project src/CustomerSupportCrm.Api
```

## Configuration

| Setting | Where it comes from in production |
|---------|-----------------------------------|
| `Jwt:SigningKey` | environment variable `Jwt__SigningKey` or a secrets manager — **never** `appsettings.json` |
| `ConnectionStrings:Default` | environment variable `ConnectionStrings__Default` or a secrets manager |
| `Cors:AllowedOrigins` | per-deployment configuration; `appsettings.json` ships an empty array so a misconfigured deployment allows no cross-origin caller rather than the wrong one |

- `Jwt:SigningKey` must be **at least 32 UTF-8 bytes** (256-bit, HMAC-SHA256). It is validated at
  startup with `ValidateOnStart()`, so a missing or short key stops the host before it serves a
  request. **Rotating it invalidates every outstanding token** — every user must log in again.
  There is no refresh token and no dual-key grace window in this story, so announce rotations
  before deploying.
- The database login should be least-privilege (or a Managed Identity against Azure SQL) — never
  `sa`, never a committed connection string, and never `TrustServerCertificate=True` outside local
  development.
- Preferred local alternative to the committed dev key:

  ```
  dotnet user-secrets set "Jwt:SigningKey" "<random 32+ bytes>" --project src/CustomerSupportCrm.Api
  ```

## Endpoints

| Method | Route | Auth | Response |
|--------|-------|------|----------|
| `POST` | `/api/auth/login` | anonymous | `200 { accessToken, expiresAt, user }` · `401 { "error": "invalid_credentials" }` · `400 { "error": "invalid_request" }` |
| `GET` | `/api/auth/me` | bearer | `200 { id, email, displayName }` · `401 { "error": "unauthorized" }` |
| `GET` | `/api/diagnostics/ping` | anonymous | `200 { "status": "ok" }` |
| `GET` | `/api/diagnostics/ping/secure` | bearer | `200 { "status": "ok", "userId": … }` |

Unknown e-mail, wrong password, and a deactivated account all return a **byte-identical** `401`, so
the endpoint is not an account-enumeration oracle.

The `/api/diagnostics` endpoints are smoke tests for the auth pipeline, not product functionality.
No frontend feature code calls them.

Authorization is **secure by default**: a fallback policy requires an authenticated user on any
endpoint carrying no authorization metadata, so a controller added by a later story is protected
unless it explicitly opts out with `[AllowAnonymous]`.

## Public Web Form (Story 19)

`POST /api/public/web-forms/tickets` is one of a handful of anonymous, unauthenticated,
internet-facing endpoints in this application — every other route sits behind the fallback auth
policy above. Every one of them represents a *customer* submitting something, never a staff member
signing in, so none of them require a login: this Web Form, the Live Chat widget (Story 21), and the
Email/WhatsApp/SMS ingest endpoints below (correction — see their own sections; these were originally
staff-gated on the theory that a real provider webhook would authenticate differently, but that
distinction doesn't change who the caller represents).

The frontend ships a public page at **`/support`** (`frontend/src/features/public/SupportRequestPage.tsx`)
that posts to it — no login, no `AppLayout` sidebar, same standalone-page pattern as `/login`. Link
it from an external site, or point an existing contact form at the endpoint directly:

```
curl -X POST http://localhost:5080/api/public/web-forms/tickets \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Ali Hassan",
    "email": "ali@example.com",
    "subject": "Feature request",
    "description": "Add dark mode",
    "phone": null
  }'
```

Request schema (`WebFormSubmissionRequest`):

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | string | yes | max 128 chars; split on the first space into first/last name for the created `Customer` |
| `email` | string | yes | max 256 chars; must be a valid email address (`400 { "error": "invalid_email" }` otherwise) |
| `subject` | string | yes | max 200 chars — becomes the ticket subject |
| `description` | string | yes | max 4000 chars — becomes the ticket description |
| `phone` | string \| null | no | max 64 chars |
| `website` | string \| null | no | **honeypot** — leave this field out of the real form entirely, or render it hidden via CSS. A bot that fills every field trips it; a real browser never does. A filled value returns `202 Accepted` with no body and creates nothing — indistinguishable from a real submission to whatever filled it in. |

Responses:

- `201 { ticketId, customerId }` — a `Customer` (found by email, or newly created) and a `Ticket`
  (`sourceChannel: "WebForm"`) were created, plus one `CustomerInteraction` (`web_form`) linking
  them. Attributed to a seeded, deactivated "System (Automated)" account — there is no authenticated
  agent to credit an anonymous submission to.
- `202` — honeypot triggered; nothing was written.
- `400 { "error": "invalid_request" }` — a required field was missing/blank (standard ASP.NET model
  validation) or `email` failed format validation (`{ "error": "invalid_email" }`).
- `429` — rate-limited. **5 requests per minute per client IP** (`"public-channel"` fixed-window
  policy in `Program.cs`, shared with every other anonymous channel endpoint — Live Chat, Email,
  WhatsApp, SMS), counted across every request regardless of outcome (a validation failure or a
  tripped honeypot still counts against the limit).

### Email channel

Inbound email ingestion and outbound replies share the same `Customer` → `Ticket` →
`CustomerInteraction` domain (`email_inbound` / `email_outbound` interaction types, threaded via each
message's provider id). This story ships the abstraction
(`IEmailSender`/`Communications/Email/`) and a `NullEmailSender` that logs and reports success —
**no real mailbox or SMTP provider is wired up**. Configure the `"Email"` section
(`FromAddress`, `Provider`) in `appsettings.json` when a concrete provider is added; until then it
only affects the log line `NullEmailSender` writes.

Because there is no real inbound mailbox, `POST /api/public/email/ingest` exists to manually
replay/test an inbound message end to end:

```
curl -X POST http://localhost:5080/api/public/email/ingest \
  -H "Content-Type: application/json" \
  -d '{
    "from": "jane@example.com",
    "to": "support@crm.test",
    "subject": "Cannot log in",
    "bodyText": "Password reset fails",
    "externalMessageId": "m-1"
  }'
```

Anonymous and rate-limited via the same `"public-channel"` policy as the Web Form above (correction —
originally gated on `system.update`, on the theory that a real mail-server-to-CRM webhook would
authenticate with a shared secret rather than a staff login; but this represents a customer's message
arriving, not a staff action, so it needs no login either). The created ticket is attributed to the
same seeded "System (Automated)" account the Web Form uses.

Re-posting the same `externalMessageId` returns the same `ticketId` (`alreadyProcessed: true`) and
writes nothing new. An `inReplyToMessageId` matching an earlier message's id (inbound or outbound)
on this system links the new inbound interaction to that same ticket instead of opening a new one.

An agent replies to an email-sourced ticket via `POST /api/tickets/{id}/email-replies`
(`tickets.email.reply` permission — Agent and Manager). It 400s if the ticket's `sourceChannel` isn't
`"Email"` or its customer has no email on file, and 502s (persisting nothing) if the configured
`IEmailSender` reports failure.
