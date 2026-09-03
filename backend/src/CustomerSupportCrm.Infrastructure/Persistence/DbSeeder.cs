using CustomerSupportCrm.Domain.Branches;
using CustomerSupportCrm.Domain.Departments;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Domain.Roles;
using CustomerSupportCrm.Domain.Sla;
using CustomerSupportCrm.Domain.SystemSettings;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CustomerSupportCrm.Infrastructure.Persistence;

/// <summary>
/// Seeds the local-development administrator, the permission catalogue, the four system roles, and
/// the links between them, so the app is usable end-to-end on a fresh database.
/// </summary>
/// <remarks>
/// Runs in the <c>Development</c> environment only. Production provisioning is out of scope — see
/// <c>backend/README.md</c>. The seeder is idempotent and never throws: a missing or incomplete
/// configuration section logs a warning and continues seeding roles/permissions regardless, so the
/// app still starts even without a configured admin.
///
/// Unlike Story 01/02's seeder, this one does <b>not</b> short-circuit on "any user already exists".
/// Roles/permissions/the Administrator link must be (re-)synced on every startup — see the "Half-
/// applied state" and "Seeded admin without any UserRole row" notes in the story's edge cases — so
/// only the admin-*user* creation step is skipped once a user exists.
/// </remarks>
public static class DbSeeder
{
    public const string ConfigurationSection = "AuthSettings:SeedAdmin";

    /// <summary>Story 19: the seeded, deactivated "system" account — see <see cref="SeedSystemUserAsync"/>.</summary>
    public const string SystemUserEmail = "system@internal.local";

    private static readonly (string NormalizedName, string Name)[] SystemRoles =
    [
        ("ADMINISTRATOR", "Administrator"),
        ("MANAGER", "Manager"),
        ("AGENT", "Agent"),
        ("CUSTOMER", "Customer"),
    ];

    public static async Task SeedAsync(
        CrmDbContext db,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await SeedAdminUserAsync(db, passwordHasher, configuration, logger, cancellationToken);
        await SeedSystemUserAsync(db, passwordHasher, cancellationToken);
        await SeedPermissionsAsync(db, cancellationToken);
        var roles = await SeedSystemRolesAsync(db, cancellationToken);
        await SeedAdministratorPermissionsAsync(db, roles["ADMINISTRATOR"], cancellationToken);
        await SeedDefaultRolePermissionsAsync(db, roles, cancellationToken);
        await BackfillCustomersInteractionsReadAsync(db, roles, cancellationToken);
        await BackfillTicketCategoriesAndPrioritiesViewAsync(db, roles, cancellationToken);
        await BackfillTicketsEscalateAsync(db, roles, cancellationToken);
        await BackfillAgentTasksAsync(db, roles, cancellationToken);
        await BackfillQuickRepliesViewAsync(db, roles, cancellationToken);
        await BackfillQuickRepliesManageForAgentAsync(db, roles, cancellationToken);
        await BackfillTicketCollaborationAsync(db, roles, cancellationToken);
        await BackfillTicketEmailReplyAsync(db, roles, cancellationToken);
        await BackfillTicketChannelReplyAsync(db, roles, cancellationToken);
        await BackfillLiveChatAsync(db, roles, cancellationToken);
        await BackfillSlaEscalationsViewAsync(db, roles, cancellationToken);
        await BackfillNotificationsAsync(db, roles, cancellationToken);
        await BackfillKnowledgeBaseAsync(db, roles, cancellationToken);
        await BackfillKnowledgeBaseSolutionsAndGuidesAsync(db, roles, cancellationToken);
        await BackfillKnowledgeBaseSearchAsync(db, roles, cancellationToken);
        await LinkAdminUsersToAdministratorRoleAsync(db, roles["ADMINISTRATOR"], logger, cancellationToken);
        await SeedDefaultDepartmentAndBranchAsync(db, cancellationToken);
        await SeedDefaultTicketCategoriesAndPrioritiesAsync(db, cancellationToken);
        await SeedDefaultSlaPolicyAsync(db, cancellationToken);
        await SeedDefaultKnowledgeBaseCategoriesAsync(db, cancellationToken);
        await SeedSystemSettingsAsync(db, cancellationToken);
    }

    private static async Task SeedAdminUserAsync(
        CrmDbContext db,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Idempotent: never update or reset an existing account, and never create a second admin
        // just because the configured one differs from what's in the database.
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var section = configuration.GetSection(ConfigurationSection);
        var email = section["Email"];
        var password = section["Password"];
        var displayName = section["DisplayName"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(displayName))
        {
            logger.LogWarning("No seed admin configured; login is unavailable.");
            return;
        }

        var user = new User
        {
            Email = EmailNormalizer.Normalize(email),
            DisplayName = displayName,
            IsActive = true,
            // Kept for backward compatibility only (see the remarks on User.IsAdmin); the real grant
            // is the Administrator UserRole added by LinkAdminUsersToAdministratorRoleAsync below.
            IsAdmin = true,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        // The password is never logged.
        logger.LogInformation(
            "event={Event} userId={UserId} email={Email}",
            "seed_admin_created", user.Id, user.Email);
    }

    /// <summary>
    /// Story 19: the actor attributed to tickets/interactions created by the anonymous public Web
    /// Form — there is no authenticated agent to use, and <c>Ticket.CreatedByUserId</c> is a required
    /// FK. Deactivated (<c>IsActive = false</c>, so <c>AuthController</c> refuses it at login) with a
    /// random, nobody-knows-it password hash as defense in depth on top of that. Never assigned a
    /// role or shown any permission — it is a data-attribution placeholder, not an account anyone
    /// operates as.
    /// </summary>
    private static async Task SeedSystemUserAsync(CrmDbContext db, IPasswordHasher<User> passwordHasher, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Email == SystemUserEmail, cancellationToken))
        {
            return;
        }

        var user = new User
        {
            Email = SystemUserEmail,
            DisplayName = "System (Automated)",
            IsActive = false,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, Guid.NewGuid().ToString("N"));

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Ensures a <see cref="Permission"/> row exists for every entry in the catalogue, matched by Code.</summary>
    private static async Task SeedPermissionsAsync(CrmDbContext db, CancellationToken cancellationToken)
    {
        var existingCodes = await db.Permissions.Select(p => p.Code).ToListAsync(cancellationToken);
        var existingCodeSet = existingCodes.ToHashSet(StringComparer.Ordinal);

        var missing = Permissions.All.Where(p => !existingCodeSet.Contains(p.Code));
        foreach (var definition in missing)
        {
            db.Permissions.Add(new Permission
            {
                Code = definition.Code,
                Category = definition.Category,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Ensures the four system roles exist by NormalizedName. Returns them keyed by NormalizedName.</summary>
    private static async Task<Dictionary<string, Role>> SeedSystemRolesAsync(CrmDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Roles
            .Where(r => SystemRoles.Select(s => s.NormalizedName).Contains(r.NormalizedName))
            .ToDictionaryAsync(r => r.NormalizedName, cancellationToken);

        foreach (var (normalizedName, name) in SystemRoles)
        {
            if (existing.ContainsKey(normalizedName))
            {
                continue;
            }

            var role = new Role { Name = name, NormalizedName = normalizedName, IsSystem = true };
            db.Roles.Add(role);
            existing[normalizedName] = role;
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    /// <summary>Re-syncs Administrator to have every catalogue permission, so a newly added permission is automatically granted on the next startup.</summary>
    private static async Task SeedAdministratorPermissionsAsync(CrmDbContext db, Role administrator, CancellationToken cancellationToken)
    {
        var grantedCodes = await db.RolePermissions
            .Where(rp => rp.RoleId == administrator.Id)
            .Select(rp => rp.Permission.Code)
            .ToListAsync(cancellationToken);
        var grantedCodeSet = grantedCodes.ToHashSet(StringComparer.Ordinal);

        var missingCodes = Permissions.All.Select(p => p.Code).Where(code => !grantedCodeSet.Contains(code)).ToList();
        if (missingCodes.Count == 0)
        {
            return;
        }

        var missingPermissionIds = await db.Permissions
            .Where(p => missingCodes.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var permissionId in missingPermissionIds)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = administrator.Id, PermissionId = permissionId });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Grants Manager/Agent/Customer their starter permission set, but only the first time each role
    /// has zero <see cref="RolePermission"/> rows — an administrator's later edits are never
    /// overwritten by a subsequent restart.
    /// </summary>
    private static async Task SeedDefaultRolePermissionsAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken)
    {
        // Every default below must stay inside Permissions.EligibleBySystemRole for its role — the
        // starter grant an admin sees on first login is a (smaller) subset of what they're allowed to
        // add later via the Roles UI, never a superset. Do not reintroduce a blanket "all .view
        // codes" shortcut here: that would grant Manager roles.view/permissions.view/system.view/
        // audit.view, none of which are in its Eligible Permissions Matrix row.
        var defaults = new Dictionary<string, string[]>
        {
            ["MANAGER"] =
            [
                Permissions.Users.View, Permissions.Users.Update,
                Permissions.Customers.View, Permissions.Customers.InteractionsRead,
                Permissions.Tickets.View, Permissions.Tickets.Assign,
                Permissions.Tickets.Escalate, Permissions.Tickets.EscalationManage,
                Permissions.Tickets.CategoriesView, Permissions.Tickets.PrioritiesView,
                Permissions.Tickets.CollaborationView, Permissions.Tickets.CollaborationCreate,
                Permissions.Tickets.EmailReply, Permissions.Tickets.ChannelReply,
                Permissions.LiveChat.View, Permissions.LiveChat.Send,
                Permissions.KnowledgeBase.ArticlesView, Permissions.KnowledgeBase.ArticlesViewInternal,
                Permissions.KnowledgeBase.ArticlesManage, Permissions.KnowledgeBase.ArticlesPublish,
                Permissions.KnowledgeBase.CategoriesManage,
                Permissions.KnowledgeBase.SolutionsView, Permissions.KnowledgeBase.SolutionsViewInternal,
                Permissions.KnowledgeBase.SolutionsManage, Permissions.KnowledgeBase.SolutionsPublish,
                Permissions.KnowledgeBase.GuidesView, Permissions.KnowledgeBase.GuidesViewInternal,
                Permissions.KnowledgeBase.GuidesManage, Permissions.KnowledgeBase.GuidesPublish,
                Permissions.KnowledgeBase.Search,
                Permissions.Reports.View,
                Permissions.Branches.View, Permissions.Departments.View,
                Permissions.QuickReplies.View,
                Permissions.Sla.EscalationsView,
                Permissions.Notifications.ViewOwn, Permissions.Notifications.MarkRead,
            ],
            ["AGENT"] =
            [
                Permissions.Tickets.View, Permissions.Tickets.Create, Permissions.Tickets.Update,
                Permissions.Tickets.Escalate,
                Permissions.Tickets.CollaborationView, Permissions.Tickets.CollaborationCreate,
                Permissions.Tickets.EmailReply, Permissions.Tickets.ChannelReply,
                Permissions.LiveChat.View, Permissions.LiveChat.Send,
                Permissions.Customers.View, Permissions.Customers.Update, Permissions.Customers.InteractionsRead,
                Permissions.KnowledgeBase.ArticlesView, Permissions.KnowledgeBase.ArticlesViewInternal,
                Permissions.KnowledgeBase.SolutionsView, Permissions.KnowledgeBase.SolutionsViewInternal,
                Permissions.KnowledgeBase.GuidesView, Permissions.KnowledgeBase.GuidesViewInternal,
                Permissions.KnowledgeBase.Search,
                Permissions.AgentTasks.Read, Permissions.AgentTasks.Create, Permissions.AgentTasks.Update,
                Permissions.AgentTasks.Delete, Permissions.AgentTasks.Complete,
                Permissions.QuickReplies.View, Permissions.QuickReplies.Manage,
                Permissions.Notifications.ViewOwn, Permissions.Notifications.MarkRead,
            ],
            ["CUSTOMER"] =
            [
                Permissions.CustomerPortal.Access,
                Permissions.KnowledgeBase.ArticlesView,
                Permissions.KnowledgeBase.SolutionsView,
                Permissions.KnowledgeBase.GuidesView,
                Permissions.KnowledgeBase.Search,
            ],
        };

        foreach (var (normalizedName, codes) in defaults)
        {
            var role = roles[normalizedName];

            var hasAnyPermission = await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id, cancellationToken);
            if (hasAnyPermission)
            {
                continue;
            }

            var distinctCodes = codes.Distinct(StringComparer.Ordinal).ToList();
            var permissionIds = await db.Permissions
                .Where(p => distinctCodes.Contains(p.Code))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            foreach (var permissionId in permissionIds)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Story 08 added <see cref="Permissions.Customers.InteractionsRead"/> after
    /// <see cref="SeedDefaultRolePermissionsAsync"/> had already run its one-time bootstrap on most
    /// installs (it only seeds a role that currently has zero <see cref="RolePermission"/> rows, so a
    /// Manager/Agent role provisioned before this story would never receive the new grant through
    /// that path). This backfills exactly that one code for exactly those two roles.
    /// </summary>
    private static Task BackfillCustomersInteractionsReadAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken) =>
        BackfillPermissionForRolesAsync(db, roles, Permissions.Customers.InteractionsRead, ["MANAGER", "AGENT"], cancellationToken);

    /// <summary>
    /// Story 10 added <see cref="Permissions.Tickets.CategoriesView"/>/<see cref="Permissions.Tickets.PrioritiesView"/>
    /// after Manager's one-time bootstrap had already run on most installs — same gap as
    /// <see cref="BackfillCustomersInteractionsReadAsync"/>. Manager only (matching
    /// <see cref="SeedDefaultRolePermissionsAsync"/>'s Manager-only default grant for these two codes;
    /// Agent is eligible via <see cref="Permissions.EligibleBySystemRole"/> but not auto-granted,
    /// mirroring how Agent never gets <c>Departments.View</c>/<c>Branches.View</c> by default either).
    /// </summary>
    private static async Task BackfillTicketCategoriesAndPrioritiesViewAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken)
    {
        await BackfillPermissionForRolesAsync(db, roles, Permissions.Tickets.CategoriesView, ["MANAGER"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.Tickets.PrioritiesView, ["MANAGER"], cancellationToken);
    }

    /// <summary>
    /// Story 13 added <see cref="Permissions.Tickets.Escalate"/> after Manager's one-time bootstrap
    /// had already run on most installs — same gap as <see cref="BackfillCustomersInteractionsReadAsync"/>.
    /// The escalation-workflow correction reworked who requests vs. resolves an escalation: Agent and
    /// Manager can both *request* escalation (<see cref="Permissions.Tickets.Escalate"/>), but only
    /// Manager can de-escalate / manage the queue (<see cref="Permissions.Tickets.EscalationManage"/>,
    /// backfilled to Manager only — Agent is not eligible for it, see
    /// <see cref="Permissions.EligibleBySystemRole"/>).
    /// </summary>
    private static async Task BackfillTicketsEscalateAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken)
    {
        await BackfillPermissionForRolesAsync(db, roles, Permissions.Tickets.Escalate, ["MANAGER", "AGENT"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.Tickets.EscalationManage, ["MANAGER"], cancellationToken);
    }

    /// <summary>
    /// Story 16 added <see cref="Permissions.AgentTasks"/> after Agent's one-time bootstrap had
    /// already run on most installs — same gap as <see cref="BackfillCustomersInteractionsReadAsync"/>.
    /// Agent only, matching <see cref="SeedDefaultRolePermissionsAsync"/>'s Agent-only default grant
    /// for these five codes.
    /// </summary>
    private static async Task BackfillAgentTasksAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken)
    {
        await BackfillPermissionForRolesAsync(db, roles, Permissions.AgentTasks.Read, ["AGENT"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.AgentTasks.Create, ["AGENT"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.AgentTasks.Update, ["AGENT"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.AgentTasks.Delete, ["AGENT"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.AgentTasks.Complete, ["AGENT"], cancellationToken);
    }

    /// <summary>
    /// Story 17 added <see cref="Permissions.QuickReplies.View"/> after Manager/Agent's one-time
    /// bootstrap had already run on most installs — same gap as <see cref="BackfillCustomersInteractionsReadAsync"/>.
    /// Administrator already has every permission via <see cref="SeedAdministratorPermissionsAsync"/>, so
    /// only Manager/Agent need the View backfill.
    /// </summary>
    private static Task BackfillQuickRepliesViewAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken) =>
        BackfillPermissionForRolesAsync(db, roles, Permissions.QuickReplies.View, ["MANAGER", "AGENT"], cancellationToken);

    /// <summary>
    /// Correction (post-implementation): Agent also gets <see cref="Permissions.QuickReplies.Manage"/> —
    /// unlike <see cref="Permissions.Tickets.CategoriesManage"/>'s admin-only master-data convention, an
    /// Agent authors and maintains their own quick-reply catalogue day to day. A separate backfill from
    /// <see cref="BackfillQuickRepliesViewAsync"/> since installs that already ran the base Story 17
    /// backfill won't otherwise pick this up.
    /// </summary>
    private static Task BackfillQuickRepliesManageForAgentAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken) =>
        BackfillPermissionForRolesAsync(db, roles, Permissions.QuickReplies.Manage, ["AGENT"], cancellationToken);

    /// <summary>
    /// Story 18 added <see cref="Permissions.Tickets.CollaborationView"/>/<see cref="Permissions.Tickets.CollaborationCreate"/>
    /// after Manager/Agent's one-time bootstrap had already run on most installs — same gap as
    /// <see cref="BackfillCustomersInteractionsReadAsync"/>. Both codes go to both roles: internal
    /// collaboration is a two-way discussion, unlike Escalate's request/resolve split.
    /// </summary>
    private static async Task BackfillTicketCollaborationAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken)
    {
        await BackfillPermissionForRolesAsync(db, roles, Permissions.Tickets.CollaborationView, ["MANAGER", "AGENT"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.Tickets.CollaborationCreate, ["MANAGER", "AGENT"], cancellationToken);
    }

    /// <summary>Story 19 added <see cref="Permissions.Tickets.EmailReply"/> after Manager/Agent's one-time bootstrap had already run on most installs — same gap as <see cref="BackfillTicketCollaborationAsync"/>.</summary>
    private static Task BackfillTicketEmailReplyAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken) =>
        BackfillPermissionForRolesAsync(db, roles, Permissions.Tickets.EmailReply, ["MANAGER", "AGENT"], cancellationToken);

    /// <summary>Story 20 added <see cref="Permissions.Tickets.ChannelReply"/> after Manager/Agent's one-time bootstrap had already run on most installs — same gap as <see cref="BackfillTicketEmailReplyAsync"/>. <see cref="Permissions.Communications"/> needs no backfill: Administrator-only, and Administrator already has every permission via <see cref="SeedAdministratorPermissionsAsync"/>.</summary>
    private static Task BackfillTicketChannelReplyAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken) =>
        BackfillPermissionForRolesAsync(db, roles, Permissions.Tickets.ChannelReply, ["MANAGER", "AGENT"], cancellationToken);

    /// <summary>Story 21 added <see cref="Permissions.LiveChat"/> after Manager/Agent's one-time bootstrap had already run on most installs — same gap as <see cref="BackfillTicketChannelReplyAsync"/>.</summary>
    private static async Task BackfillLiveChatAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken)
    {
        await BackfillPermissionForRolesAsync(db, roles, Permissions.LiveChat.View, ["MANAGER", "AGENT"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.LiveChat.Send, ["MANAGER", "AGENT"], cancellationToken);
    }

    /// <summary>Story 24 added <see cref="Permissions.Sla.EscalationsView"/> after Manager's one-time bootstrap had already run on most installs — same gap as <see cref="BackfillLiveChatAsync"/>. Manager only, matching <see cref="SeedDefaultRolePermissionsAsync"/>'s default grant — Agent/Customer are never eligible for it (see <see cref="Permissions.EligibleBySystemRole"/>).</summary>
    private static Task BackfillSlaEscalationsViewAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken) =>
        BackfillPermissionForRolesAsync(db, roles, Permissions.Sla.EscalationsView, ["MANAGER"], cancellationToken);

    /// <summary>Story 25 added <see cref="Permissions.Notifications"/> after Manager/Agent's one-time bootstrap had already run on most installs — same gap as <see cref="BackfillLiveChatAsync"/>. Both roles (every staff member can be a notification recipient).</summary>
    private static async Task BackfillNotificationsAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken)
    {
        await BackfillPermissionForRolesAsync(db, roles, Permissions.Notifications.ViewOwn, ["MANAGER", "AGENT"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.Notifications.MarkRead, ["MANAGER", "AGENT"], cancellationToken);
    }

    /// <summary>
    /// Story 26 replaced the old "kb.*" placeholder codes (seeded but never actually consumed by any
    /// endpoint) with this finer-grained set. Manager/Agent's one-time bootstrap had already run on
    /// most installs before this story existed, so the same backfill gap as
    /// <see cref="BackfillLiveChatAsync"/> applies — plus Customer, who was never eligible for the old
    /// "kb.view" at all but explicitly must be for the new <see cref="Permissions.KnowledgeBase.ArticlesView"/>
    /// per this story's own visibility rules.
    /// </summary>
    private static async Task BackfillKnowledgeBaseAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken)
    {
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.ArticlesView, ["MANAGER", "AGENT", "CUSTOMER"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.ArticlesViewInternal, ["MANAGER", "AGENT"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.ArticlesManage, ["MANAGER"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.ArticlesPublish, ["MANAGER"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.CategoriesManage, ["MANAGER"], cancellationToken);
    }

    /// <summary>Story 27 added Solutions/Guides as new content types alongside Story 26's Articles, following the exact same View/ViewInternal/Manage/Publish shape — same backfill gap and audience split as <see cref="BackfillKnowledgeBaseAsync"/>.</summary>
    private static async Task BackfillKnowledgeBaseSolutionsAndGuidesAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken)
    {
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.SolutionsView, ["MANAGER", "AGENT", "CUSTOMER"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.SolutionsViewInternal, ["MANAGER", "AGENT"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.SolutionsManage, ["MANAGER"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.SolutionsPublish, ["MANAGER"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.GuidesView, ["MANAGER", "AGENT", "CUSTOMER"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.GuidesViewInternal, ["MANAGER", "AGENT"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.GuidesManage, ["MANAGER"], cancellationToken);
        await BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.GuidesPublish, ["MANAGER"], cancellationToken);
    }

    /// <summary>Story 28 added the cross-content-type search endpoint after every role's one-time bootstrap had already run on most installs — same backfill gap as <see cref="BackfillKnowledgeBaseSolutionsAndGuidesAsync"/>, granted to all four roles (Search is read-only, gated per-content-type by that type's own View/ViewInternal permission).</summary>
    private static Task BackfillKnowledgeBaseSearchAsync(CrmDbContext db, Dictionary<string, Role> roles, CancellationToken cancellationToken) =>
        BackfillPermissionForRolesAsync(db, roles, Permissions.KnowledgeBase.Search, ["MANAGER", "AGENT", "CUSTOMER"], cancellationToken);

    /// <summary>
    /// Grants a single permission code to a fixed set of roles if they don't already have it —
    /// checked and inserted individually and idempotently, so it never touches any other permission
    /// an administrator may have since added or removed, unlike re-running
    /// <see cref="SeedDefaultRolePermissionsAsync"/>'s bootstrap wholesale would.
    /// </summary>
    private static async Task BackfillPermissionForRolesAsync(
        CrmDbContext db, Dictionary<string, Role> roles, string permissionCode, string[] normalizedRoleNames, CancellationToken cancellationToken)
    {
        var permissionId = await db.Permissions
            .Where(p => p.Code == permissionCode)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (permissionId is null)
        {
            // SeedPermissionsAsync (earlier in SeedAsync) always inserts it first; this guards only
            // against an unexpected ordering change, not a real steady-state case.
            return;
        }

        foreach (var normalizedName in normalizedRoleNames)
        {
            if (!roles.TryGetValue(normalizedName, out var role))
            {
                continue;
            }

            var alreadyGranted = await db.RolePermissions.AnyAsync(
                rp => rp.RoleId == role.Id && rp.PermissionId == permissionId, cancellationToken);
            if (alreadyGranted)
            {
                continue;
            }

            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId.Value });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Self-healing link: every user flagged <see cref="User.IsAdmin"/> gets a UserRole to
    /// Administrator if they don't already have one. Covers the freshly-seeded admin, a break-glass
    /// <c>UPDATE Users SET IsAdmin = 1</c> recovery, and a partially-applied prior migration.
    /// </summary>
    private static async Task LinkAdminUsersToAdministratorRoleAsync(CrmDbContext db, Role administrator, ILogger logger, CancellationToken cancellationToken)
    {
        var adminUserIds = await db.Users.Where(u => u.IsAdmin).Select(u => u.Id).ToListAsync(cancellationToken);
        if (adminUserIds.Count == 0)
        {
            return;
        }

        var linkedUserIds = await db.UserRoles
            .Where(ur => ur.RoleId == administrator.Id && adminUserIds.Contains(ur.UserId))
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);
        var linkedUserIdSet = linkedUserIds.ToHashSet();

        var unlinkedUserIds = adminUserIds.Where(id => !linkedUserIdSet.Contains(id)).ToList();
        if (unlinkedUserIds.Count == 0)
        {
            return;
        }

        foreach (var userId in unlinkedUserIds)
        {
            db.UserRoles.Add(new UserRole { UserId = userId, RoleId = administrator.Id });
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "event={Event} userIds={UserIds}",
            "seed_admin_role_relinked", string.Join(",", unlinkedUserIds));
    }

    /// <summary>
    /// Seeds one default department ("General") and one default branch ("Head Office") so a fresh
    /// deployment has a working default to assign users to. Idempotent by <c>NormalizedName</c> —
    /// same guard style as <see cref="SeedSystemRolesAsync"/> — so re-running never duplicates rows,
    /// and an admin who renames or deactivates either one is never overwritten on the next restart.
    /// </summary>
    private static async Task SeedDefaultDepartmentAndBranchAsync(CrmDbContext db, CancellationToken cancellationToken)
    {
        const string departmentNormalizedName = "GENERAL";
        if (!await db.Departments.AnyAsync(d => d.NormalizedName == departmentNormalizedName, cancellationToken))
        {
            db.Departments.Add(new Department { Name = "General", NormalizedName = departmentNormalizedName });
        }

        const string branchNormalizedName = "HEAD OFFICE";
        if (!await db.Branches.AnyAsync(b => b.NormalizedName == branchNormalizedName, cancellationToken))
        {
            db.Branches.Add(new Branch { Name = "Head Office", NormalizedName = branchNormalizedName });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds the standard ticket categories and priorities so a fresh deployment has a working
    /// default catalogue — same rationale and idempotent-by-<c>NormalizedName</c> guard style as
    /// <see cref="SeedDefaultDepartmentAndBranchAsync"/>, since that method already establishes the
    /// convention of seeding this kind of master data. An admin who renames, deactivates, or reorders
    /// any of these is never overwritten on the next restart.
    /// </summary>
    private static async Task SeedDefaultTicketCategoriesAndPrioritiesAsync(CrmDbContext db, CancellationToken cancellationToken)
    {
        (string Name, string NormalizedName)[] categories =
        [
            ("Technical Support", "TECHNICAL SUPPORT"),
            ("Billing", "BILLING"),
            ("Account / Access", "ACCOUNT / ACCESS"),
            ("General Inquiry", "GENERAL INQUIRY"),
            ("Complaint", "COMPLAINT"),
            ("Feature Request", "FEATURE REQUEST"),
        ];

        foreach (var (name, normalizedName) in categories)
        {
            if (!await db.TicketCategories.AnyAsync(c => c.NormalizedName == normalizedName, cancellationToken))
            {
                db.TicketCategories.Add(new TicketCategory { Name = name, NormalizedName = normalizedName });
            }
        }

        (string Name, string NormalizedName, int SortOrder)[] priorities =
        [
            ("Low", "LOW", 10),
            ("Medium", "MEDIUM", 20),
            ("High", "HIGH", 30),
            ("Urgent", "URGENT", 40),
        ];

        foreach (var (name, normalizedName, sortOrder) in priorities)
        {
            if (!await db.TicketPriorities.AnyAsync(p => p.NormalizedName == normalizedName, cancellationToken))
            {
                db.TicketPriorities.Add(new TicketPriority { Name = name, NormalizedName = normalizedName, SortOrder = sortOrder });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Story 26: a small starter set so the category picker on the article form isn't empty on a
    /// fresh install — idempotent by <c>NormalizedName</c>, same convention as
    /// <see cref="SeedDefaultTicketCategoriesAndPrioritiesAsync"/>. An admin who renames, deactivates,
    /// or adds to these is never overwritten on the next restart.
    /// </summary>
    private static async Task SeedDefaultKnowledgeBaseCategoriesAsync(CrmDbContext db, CancellationToken cancellationToken)
    {
        (string Name, string NormalizedName)[] categories =
        [
            ("General", "GENERAL"),
            ("Account", "ACCOUNT"),
            ("Billing", "BILLING"),
        ];

        foreach (var (name, normalizedName) in categories)
        {
            if (!await db.KnowledgeBaseCategories.AnyAsync(c => c.NormalizedName == normalizedName, cancellationToken))
            {
                db.KnowledgeBaseCategories.Add(new KnowledgeBaseCategory { Name = name, NormalizedName = normalizedName });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Story 22: the fallback SLA policy (<see cref="SlaPolicy.PriorityId"/> = null) — 30 minutes to
    /// First Response, 4 hours (240 minutes) to Resolution — that <c>ISlaService.StartForTicketAsync</c>
    /// applies to every ticket with no priority-specific active policy, which in this story is every
    /// ticket, since none are seeded. Idempotent: checked by name rather than by "any policy exists" so
    /// a future story that seeds priority-specific policies doesn't accidentally skip this one.
    /// </summary>
    private static async Task SeedDefaultSlaPolicyAsync(CrmDbContext db, CancellationToken cancellationToken)
    {
        const string defaultPolicyName = "Default SLA";
        if (await db.SlaPolicies.AnyAsync(p => p.Name == defaultPolicyName, cancellationToken))
        {
            return;
        }

        db.SlaPolicies.Add(new SlaPolicy
        {
            PriorityId = null,
            Name = defaultPolicyName,
            FirstResponseMinutes = 30,
            ResolutionMinutes = 240,
            IsActive = true,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds the single system-settings row (<see cref="SystemSetting.Id"/> = 1) with the story's
    /// documented defaults. Idempotent by presence of any row — same guard style as
    /// <see cref="SeedAdminUserAsync"/> — so an administrator's later edits are never overwritten on
    /// the next restart.
    /// </summary>
    private static async Task SeedSystemSettingsAsync(CrmDbContext db, CancellationToken cancellationToken)
    {
        if (await db.SystemSettings.AnyAsync(cancellationToken))
        {
            return;
        }

        db.SystemSettings.Add(new SystemSetting
        {
            Id = 1,
            ApplicationName = "Customer Support CRM",
            SupportEmail = "support@localhost",
            DefaultTimezone = "UTC",
            DefaultCulture = "en-US",
            BrandDisplayName = "Customer Support CRM",
            LogoUrl = null,
            PrimaryColor = "#1976D2",
            SecondaryColor = "#9C27B0",
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
