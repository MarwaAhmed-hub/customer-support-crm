# Story intake

## Feature

* **Feature name (display):** Foundation & Administration
* **Feature slug (folder under `plans/`):** `foundation-and-administration`

## Tracker (metadata only)

* **Tracker type:** `none`
* **Work item id:** ``
* **Work item type:** ``
* **Status:** ``
* **Assignee:** ``
* **Labels:** ``

## Title

```text
Roles & Permissions
```

## Description

```text
Implement role and permission management for the Customer Support CRM.

The system should allow administrators to define roles, assign permissions to roles, and assign roles to users. Authorization must be enforced consistently by the backend so users can only access functionality permitted by their assigned roles and permissions.

The implementation should support the CRM roles required by the system while keeping the authorization model simple and extensible.
```

## Acceptance criteria

* Authorized administrators can view the available roles.
* Authorized administrators can create and update roles.
* Authorized administrators can view the permissions available in the CRM.
* Authorized administrators can assign and remove permissions from roles.
* Authorized administrators can assign roles to users.
* Authorized administrators can remove roles from users.
* Users receive the permissions granted by their assigned roles after authentication.
* Backend endpoints enforce authorization based on the user's effective permissions.
* Unauthorized users receive `403 Forbidden` when attempting to access functionality for which they do not have permission.
* Users without the required permission cannot perform protected create, update, or delete operations even if they can access the corresponding UI.
* The frontend hides or disables administrative actions that the current user is not permitted to perform.
* The existing authentication and login flow continues to work with the new authorization model.
* Existing admin access remains functional after roles and permissions are introduced.

## Roles

The initial system should support the following roles:

* **Administrator** — full system administration and access to all CRM functionality.
* **Manager** — management-level access to CRM operations according to assigned permissions.
* **Agent** — customer support and ticket-management access according to assigned permissions.
* **Customer** — customer-facing access limited to the customer portal and functionality explicitly granted to the role.

Roles should be represented as data rather than hard-coded authorization rules wherever practical, so additional roles can be introduced later.

## Permissions

Permissions should represent specific capabilities rather than entire screens.

The permission model should be extensible to cover the CRM feature areas, including:

* User management
* Role management
* Permission management
* Department management
* Branch management
* Ticket management
* Customer management
* Knowledge base management
* Reporting/dashboard access
* System configuration
* Audit log access
* Customer portal access

Use a consistent permission naming convention, for example:

```text
users.view
users.create
users.update
users.delete

roles.view
roles.create
roles.update

permissions.view
permissions.assign

departments.view
departments.create
departments.update

branches.view
branches.create
branches.update
```

The exact permission set should be derived from the existing CRM requirements and existing feature stories rather than inventing unnecessary permissions.

## Authorization model

* A user can have one or more roles.
* A role can have one or more permissions.
* A user's effective permissions are the union of the permissions granted by their assigned roles.
* Authorization must be enforced on the backend.
* Frontend visibility checks are only a usability layer and must not be treated as the security boundary.
* Existing authentication/JWT infrastructure should be reused where possible.
* If permissions are included in JWT claims, changes to roles or permissions must not require unnecessary complexity; follow the existing token/refresh-token design.
* The implementation should avoid duplicating authorization logic across controllers.

## Dependencies

* **Blocked by / related ids:** Story 01 — Authentication & Login
* **Depends on code areas or other stories:** Authentication & Login, Users Management
* **Related:** Departments & Branches

Roles must integrate with the existing `User` entity and authentication system created by the previous stories.

## Technical hints

* Primary language: `C#`
* Backend: ASP.NET Core Web API
* Frontend: React + TypeScript
* Database: SQL Server with EF Core Code First migrations.
* Reuse the existing JWT authentication and authorization infrastructure.
* Follow the existing project architecture and conventions.
* Prefer policy/permission-based authorization over hard-coded role checks for feature-level access.
* Keep the implementation simple and suitable for the Customer Support CRM.
* Avoid introducing unnecessary authorization frameworks or external dependencies unless the existing project requires them.
* Role and permission changes should be persisted in the database.
* Follow existing DTO, controller, API client, form, routing, and testing patterns.

## Out of scope

* Authentication and login implementation
* Password reset or account recovery
* Departments and branches implementation
* Audit log implementation
* System configuration and branding
* Customer management implementation
* Ticket management implementation
* Customer portal implementation
* Fine-grained data-level authorization such as restricting a user to records belonging to a specific branch or department
* Multi-tenant authorization
* Row-level security
* Approval workflows
* Permission inheritance between roles

## Notes

* Do not assume that Administrator, Manager, Agent, and Customer are database-level roles unless the existing implementation supports this pattern.
* The authorization design should allow the initial roles to be seeded while still allowing administrators to manage role assignments.
* Avoid coupling permissions directly to UI routes.
* A user with no assigned role should not gain privileged permissions by default.
* Preserve the existing seeded administrator access during migration.
* Do not implement Department/Branch data scoping in this story; that is a separate concern.
