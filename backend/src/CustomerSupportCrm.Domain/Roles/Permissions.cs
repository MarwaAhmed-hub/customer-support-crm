namespace CustomerSupportCrm.Domain.Roles;

/// <summary>One entry of the permission catalogue, as returned by <c>GET /api/permissions</c> and seeded into the <see cref="Permission"/> table.</summary>
public sealed record PermissionDefinition(string Code, string Category, string DisplayName, string? Description = null);

/// <summary>
/// The single source of truth for every permission code the codebase references. Nested static
/// classes group codes by feature so call sites can write <c>Permissions.Users.View</c> instead of
/// a magic string, and <see cref="All"/> is what <c>DbSeeder</c> and <c>GET /api/permissions</c>
/// both read — one place to add a code, everywhere else follows automatically.
/// </summary>
/// <remarks>
/// Lives in the Domain project (not Api, despite <c>[HasPermission(...)]</c> being an Api/ASP.NET
/// Core concept) because <c>CustomerSupportCrm.Infrastructure</c>'s <c>DbSeeder</c> needs it too, and
/// Infrastructure cannot reference Api — Api references Infrastructure, not the other way around.
/// </remarks>
public static class Permissions
{
    public static class Users
    {
        public const string View = "users.view";
        public const string Create = "users.create";
        public const string Update = "users.update";
        public const string Delete = "users.delete";
    }

    public static class Roles
    {
        public const string View = "roles.view";
        public const string Create = "roles.create";
        public const string Update = "roles.update";
    }

    public static class PermissionsMgmt
    {
        public const string View = "permissions.view";
        public const string Assign = "permissions.assign";
    }

    public static class Departments
    {
        public const string View = "departments.view";
        public const string Create = "departments.create";
        public const string Update = "departments.update";
    }

    public static class Branches
    {
        public const string View = "branches.view";
        public const string Create = "branches.create";
        public const string Update = "branches.update";
    }

    public static class Tickets
    {
        // Story 11 (ticket creation & tracking) gates its GET/POST/PUT endpoints with these three —
        // View/Create/Update already existed (pre-scaffolded, like Customers.* was before Story 07),
        // so no separate "tickets.manage" slug was added: that would duplicate Update's exact meaning
        // for the one write endpoint (PUT) this story has. Delete/Assign stay unused until the
        // stories that need them (ticket deletion is not in scope anywhere yet; Assign is Story 12).
        public const string View = "tickets.view";
        public const string Create = "tickets.create";
        public const string Update = "tickets.update";
        public const string Delete = "tickets.delete";
        public const string Assign = "tickets.assign";

        /// <summary>Story 13: request escalation — held by Agent and Manager. A separate grant from <see cref="Update"/> so escalation authority (like <see cref="Assign"/>) can be revoked independently of general ticket editing.</summary>
        public const string Escalate = "tickets.escalate";

        /// <summary>Story 13 (escalation workflow correction): de-escalate / manage the escalation queue — Manager-only. Deliberately separate from <see cref="Escalate"/> so an Agent can request escalation but only a Manager can resolve it.</summary>
        public const string EscalationManage = "tickets.escalation.manage";

        // Story 10: master data (categories/priorities) is gated separately from the ticket
        // permissions above — a role can view/manage tickets without being able to reconfigure the
        // category/priority catalogue, and vice versa.
        public const string CategoriesView = "tickets.categories.view";
        public const string CategoriesManage = "tickets.categories.manage";
        public const string PrioritiesView = "tickets.priorities.view";
        public const string PrioritiesManage = "tickets.priorities.manage";

        /// <summary>Story 18: internal, staff-only discussion thread on a ticket — never customer-facing. Separate from <see cref="Update"/> so collaboration access doesn't require full ticket-edit rights; held by both Agent and Manager, matching <see cref="Escalate"/>'s "both request/participate" model.</summary>
        public const string CollaborationView = "tickets.collaboration.view";
        public const string CollaborationCreate = "tickets.collaboration.create";

        /// <summary>Story 19: send an outbound email reply on an email-sourced ticket. Separate from <see cref="Update"/> for the same independent-revocability reason as <see cref="Assign"/>/<see cref="Escalate"/>; held by both Agent and Manager.</summary>
        public const string EmailReply = "tickets.email.reply";

        /// <summary>Story 20: send an outbound WhatsApp/SMS reply on a ticket sourced from either channel. Separate grant from <see cref="EmailReply"/> since the two go through different provider abstractions (<c>IEmailSender</c> vs <c>IChannelMessageDispatcher</c>); same audience (Agent and Manager).</summary>
        public const string ChannelReply = "tickets.channel.reply";
    }

    public static class Customers
    {
        public const string View = "customers.view";
        public const string Create = "customers.create";
        public const string Update = "customers.update";
        public const string Delete = "customers.delete";

        /// <summary>Story 08: read-only access to a customer's interaction history (a separate grant from <see cref="View"/> so it can be revoked independently).</summary>
        public const string InteractionsRead = "customers.interactions.read";

        // Story 09: notes and attachments are gated separately from View/Create/Update/Delete above,
        // so e.g. a role can see a customer's profile without seeing (or editing) its notes/files.
        public const string NotesRead = "customers.notes.read";
        public const string NotesCreate = "customers.notes.create";
        public const string NotesUpdate = "customers.notes.update";
        public const string NotesDelete = "customers.notes.delete";
        public const string AttachmentsRead = "customers.attachments.read";
        public const string AttachmentsCreate = "customers.attachments.create";
        public const string AttachmentsDelete = "customers.attachments.delete";
    }

    /// <summary>
    /// Story 26: FAQs and Help Articles — a finer-grained model than the "kb.*" placeholder codes it
    /// replaces (View/Create/Update/Delete, seeded but never actually consumed by any endpoint before
    /// this story). <see cref="ArticlesView"/> is "see published items" (every authenticated role,
    /// including Customer); <see cref="ArticlesViewInternal"/> additionally unlocks Internal-audience
    /// items (staff only, never Customer); <see cref="ArticlesManage"/> is create/edit/delete;
    /// <see cref="ArticlesPublish"/> is a separate grant from Manage for the same
    /// independent-revocability reason as <see cref="Tickets.Assign"/>/<see cref="Tickets.Escalate"/>.
    /// </summary>
    public static class KnowledgeBase
    {
        public const string ArticlesView = "knowledgebase.articles.view";
        public const string ArticlesViewInternal = "knowledgebase.articles.view.internal";
        public const string ArticlesManage = "knowledgebase.articles.manage";
        public const string ArticlesPublish = "knowledgebase.articles.publish";
        public const string CategoriesManage = "knowledgebase.categories.manage";

        // Story 27: Solutions and Guides are distinct content types from Articles, but follow the exact
        // same four-permission shape (View/ViewInternal/Manage/Publish) rather than the finer-grained
        // Create/Edit/ViewAll/ViewPublishedInternal/ViewPublishedCustomer split the original plan
        // proposed — that split would have left this one feature area with two inconsistent
        // permission granularities for no behavioral difference.
        public const string SolutionsView = "knowledgebase.solutions.view";
        public const string SolutionsViewInternal = "knowledgebase.solutions.view.internal";
        public const string SolutionsManage = "knowledgebase.solutions.manage";
        public const string SolutionsPublish = "knowledgebase.solutions.publish";
        public const string GuidesView = "knowledgebase.guides.view";
        public const string GuidesViewInternal = "knowledgebase.guides.view.internal";
        public const string GuidesManage = "knowledgebase.guides.manage";
        public const string GuidesPublish = "knowledgebase.guides.publish";

        /// <summary>Story 28: gates the cross-content-type search endpoint itself. Which individual results a caller actually sees is still governed by that content type's own View/ViewInternal permission — Search does not bypass them (see <c>KnowledgeBaseSearchService</c>).</summary>
        public const string Search = "knowledgebase.search";
    }

    public static class Reports
    {
        public const string View = "reports.view";
    }

    public static class SystemConfig
    {
        public const string View = "system.view";
        public const string Update = "system.update";
    }

    public static class AuditLogs
    {
        public const string View = "audit.view";
    }

    public static class CustomerPortal
    {
        public const string Access = "portal.access";
    }

    /// <summary>Story 16: personal to-do items owned by exactly one Agent — never shared, never linked to a ticket or customer. Read/Create/Update/Delete/Complete are separate grants (like <c>Customers.Notes*</c>) purely for consistency with the rest of the catalogue; the service scopes every query to the caller's own rows regardless of which of these a role holds.</summary>
    public static class AgentTasks
    {
        public const string Read = "agenttasks.read";
        public const string Create = "agenttasks.create";
        public const string Update = "agenttasks.update";
        public const string Delete = "agenttasks.delete";
        public const string Complete = "agenttasks.complete";
    }

    /// <summary>Story 17: reusable response-template text Agents can insert into a ticket reply. <c>View</c> is Agent/Manager (they use the picker). <c>Manage</c> (create/edit/delete the catalogue) is Administrator and Agent — unlike <see cref="Tickets.CategoriesManage"/>'s admin-only master-data convention, an Agent maintains their own quick-reply catalogue day to day (Manager stays view-only).</summary>
    public static class QuickReplies
    {
        public const string View = "quickreplies.view";
        public const string Manage = "quickreplies.manage";
    }

    /// <summary>
    /// Story 21: the Live Chat agent workspace (inbox + conversation). Starting/continuing a chat is
    /// genuinely anonymous (the CRM itself is the transport, no external provider to authenticate
    /// against) — see <see cref="Api.LiveChat.PublicLiveChatController"/>, which carries no permission
    /// gate at all, only its own session token. Correction: the WhatsApp/SMS/Email ingest endpoints
    /// (Stories 19/20) were originally Administrator-gated as a stand-in for provider-signature
    /// authentication, but every one of these channels represents a customer submitting something —
    /// never a staff member — so they are anonymous too now, the same as Web Form and Live Chat. These
    /// two permissions gate the Live Chat *staff* side only. <c>View</c> is Agent/Manager (same audience
    /// as <see cref="Tickets.ChannelReply"/>); <c>Send</c> is a separate grant for the same
    /// independent-revocability reason as every other reply permission in this catalogue.
    /// </summary>
    public static class LiveChat
    {
        public const string View = "livechat.view";
        public const string Send = "livechat.send";
    }

    /// <summary>Story 24: read-only visibility into automatically-generated SLA escalation records. Administrator (full catalogue, automatic) and Manager (explicit grant below) — Agent/Customer never see this, matching who escalations are ever routed to (Agent/Manager/Administrator, but only Manager+ gets to browse the record of them).</summary>
    public static class Sla
    {
        public const string EscalationsView = "sla.escalations.view";
    }

    /// <summary>Story 25: a signed-in staff member's own in-app notification inbox — every staff role (Administrator/Manager/Agent) can be a notification recipient, so all three get both grants; Customer never receives a notification in this pass and is never eligible.</summary>
    public static class Notifications
    {
        public const string ViewOwn = "notifications.view.own";
        public const string MarkRead = "notifications.mark_read";
    }

    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(Users.View, "users", "View users"),
        new(Users.Create, "users", "Create users"),
        new(Users.Update, "users", "Update users"),
        new(Users.Delete, "users", "Delete users"),

        new(Roles.View, "roles", "View roles"),
        new(Roles.Create, "roles", "Create roles"),
        new(Roles.Update, "roles", "Update roles"),

        new(PermissionsMgmt.View, "permissions", "View permissions"),
        new(PermissionsMgmt.Assign, "permissions", "Assign permissions to roles and roles to users"),

        new(Departments.View, "departments", "View departments"),
        new(Departments.Create, "departments", "Create departments"),
        new(Departments.Update, "departments", "Update departments"),

        new(Branches.View, "branches", "View branches"),
        new(Branches.Create, "branches", "Create branches"),
        new(Branches.Update, "branches", "Update branches"),

        new(Tickets.View, "tickets", "View tickets"),
        new(Tickets.Create, "tickets", "Create tickets"),
        new(Tickets.Update, "tickets", "Update tickets"),
        new(Tickets.Delete, "tickets", "Delete tickets"),
        new(Tickets.Assign, "tickets", "Assign tickets"),
        new(Tickets.Escalate, "tickets", "Request ticket escalation"),
        new(Tickets.EscalationManage, "tickets", "De-escalate and manage escalated tickets"),
        new(Tickets.CategoriesView, "tickets", "View ticket categories"),
        new(Tickets.CategoriesManage, "tickets", "Manage ticket categories"),
        new(Tickets.PrioritiesView, "tickets", "View ticket priorities"),
        new(Tickets.PrioritiesManage, "tickets", "Manage ticket priorities"),
        new(Tickets.CollaborationView, "tickets", "View internal ticket collaboration comments"),
        new(Tickets.CollaborationCreate, "tickets", "Add internal ticket collaboration comments"),
        new(Tickets.EmailReply, "tickets", "Send an outbound email reply on an email-sourced ticket"),
        new(Tickets.ChannelReply, "tickets", "Send an outbound WhatsApp/SMS reply on a channel-sourced ticket"),

        new(Customers.View, "customers", "View customers"),
        new(Customers.Create, "customers", "Create customers"),
        new(Customers.Update, "customers", "Update customers"),
        new(Customers.Delete, "customers", "Delete customers"),
        new(Customers.InteractionsRead, "customers", "View customer interaction history"),
        new(Customers.NotesRead, "customers", "View customer notes"),
        new(Customers.NotesCreate, "customers", "Add customer notes"),
        new(Customers.NotesUpdate, "customers", "Edit customer notes"),
        new(Customers.NotesDelete, "customers", "Delete customer notes"),
        new(Customers.AttachmentsRead, "customers", "View customer attachments"),
        new(Customers.AttachmentsCreate, "customers", "Upload customer attachments"),
        new(Customers.AttachmentsDelete, "customers", "Delete customer attachments"),

        new(KnowledgeBase.ArticlesView, "kb", "View published knowledge base articles"),
        new(KnowledgeBase.ArticlesViewInternal, "kb", "View internal-audience knowledge base articles"),
        new(KnowledgeBase.ArticlesManage, "kb", "Create, edit, and delete knowledge base articles"),
        new(KnowledgeBase.ArticlesPublish, "kb", "Publish and unpublish knowledge base articles"),
        new(KnowledgeBase.CategoriesManage, "kb", "Manage knowledge base categories"),
        new(KnowledgeBase.SolutionsView, "kb", "View published knowledge base solutions"),
        new(KnowledgeBase.SolutionsViewInternal, "kb", "View internal-audience knowledge base solutions"),
        new(KnowledgeBase.SolutionsManage, "kb", "Create, edit, and delete knowledge base solutions"),
        new(KnowledgeBase.SolutionsPublish, "kb", "Publish and unpublish knowledge base solutions"),
        new(KnowledgeBase.GuidesView, "kb", "View published knowledge base guides"),
        new(KnowledgeBase.GuidesViewInternal, "kb", "View internal-audience knowledge base guides"),
        new(KnowledgeBase.GuidesManage, "kb", "Create, edit, and delete knowledge base guides"),
        new(KnowledgeBase.GuidesPublish, "kb", "Publish and unpublish knowledge base guides"),
        new(KnowledgeBase.Search, "kb", "Search across all knowledge base content types"),

        new(Reports.View, "reports", "View reports"),

        new(SystemConfig.View, "system", "View system configuration"),
        new(SystemConfig.Update, "system", "Update system configuration"),

        new(AuditLogs.View, "audit", "View audit logs"),

        new(CustomerPortal.Access, "portal", "Access the customer portal"),

        new(AgentTasks.Read, "agenttasks", "View personal tasks"),
        new(AgentTasks.Create, "agenttasks", "Create personal tasks"),
        new(AgentTasks.Update, "agenttasks", "Edit personal tasks"),
        new(AgentTasks.Delete, "agenttasks", "Delete personal tasks"),
        new(AgentTasks.Complete, "agenttasks", "Complete/reopen personal tasks"),

        new(QuickReplies.View, "quickreplies", "View quick reply templates"),
        new(QuickReplies.Manage, "quickreplies", "Create, edit, and delete quick reply templates"),

        new(LiveChat.View, "livechat", "View the live chat inbox and conversations"),
        new(LiveChat.Send, "livechat", "Send a live chat reply"),

        new(Sla.EscalationsView, "sla", "View SLA escalation records"),

        new(Notifications.ViewOwn, "notifications", "View your own notifications"),
        new(Notifications.MarkRead, "notifications", "Mark your own notifications as read"),
    ];

    /// <summary>
    /// The Eligible Permissions Matrix: the fixed subset of <see cref="All"/> that each seeded
    /// system role is allowed to hold, keyed by <see cref="Role.NormalizedName"/>. Administrator and
    /// custom (non-system) roles are deliberately absent — they are eligible for the full catalogue,
    /// which <see cref="PermissionCatalog.EligibleFor"/> treats as the default for any role that has
    /// no entry here.
    /// </summary>
    /// <remarks>
    /// This is the single source of truth for the matrix: <c>GET /api/roles/{id}/eligible-permissions</c>
    /// (what the Roles UI renders) and <c>PUT /api/roles/{id}/permissions</c> (what it enforces on
    /// save) both read it via <see cref="IPermissionCatalog.EligibleFor"/> — neither hard-codes its
    /// own copy. Manager/Agent/Customer are never eligible for <c>roles.*</c>, <c>permissions.*</c>,
    /// <c>system.*</c>, or <c>audit.view</c>; Agent is additionally never eligible for
    /// <c>tickets.delete</c>/<c>tickets.assign</c>/<c>tickets.escalation.manage</c> (an Agent may
    /// *request* escalation via <c>tickets.escalate</c>, but only Manager/Administrator can resolve
    /// it); Customer is eligible for nothing beyond <c>portal.access</c>, its three
    /// <c>tickets.*</c> permissions, and (Story 26/27) read-only <c>knowledgebase.articles.view</c> /
    /// <c>knowledgebase.solutions.view</c> / <c>knowledgebase.guides.view</c>.
    /// </remarks>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> EligibleBySystemRole { get; } =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["MANAGER"] = new HashSet<string>(StringComparer.Ordinal)
            {
                Users.View, Users.Update,
                Customers.View, Customers.Create, Customers.Update, Customers.InteractionsRead,
                Customers.NotesRead, Customers.NotesCreate, Customers.NotesUpdate, Customers.NotesDelete,
                Customers.AttachmentsRead, Customers.AttachmentsCreate, Customers.AttachmentsDelete,
                Tickets.View, Tickets.Create, Tickets.Update, Tickets.Delete, Tickets.Assign,
                Tickets.Escalate, Tickets.EscalationManage,
                Tickets.CategoriesView, Tickets.PrioritiesView,
                Tickets.CollaborationView, Tickets.CollaborationCreate,
                Tickets.EmailReply, Tickets.ChannelReply,
                LiveChat.View, LiveChat.Send,
                KnowledgeBase.ArticlesView, KnowledgeBase.ArticlesViewInternal,
                KnowledgeBase.ArticlesManage, KnowledgeBase.ArticlesPublish, KnowledgeBase.CategoriesManage,
                KnowledgeBase.SolutionsView, KnowledgeBase.SolutionsViewInternal,
                KnowledgeBase.SolutionsManage, KnowledgeBase.SolutionsPublish,
                KnowledgeBase.GuidesView, KnowledgeBase.GuidesViewInternal,
                KnowledgeBase.GuidesManage, KnowledgeBase.GuidesPublish,
                KnowledgeBase.Search,
                Reports.View, Branches.View, Departments.View,
                QuickReplies.View,
                Sla.EscalationsView,
                Notifications.ViewOwn, Notifications.MarkRead,
            },
            ["AGENT"] = new HashSet<string>(StringComparer.Ordinal)
            {
                Tickets.View, Tickets.Create, Tickets.Update, Tickets.Escalate,
                Tickets.CategoriesView, Tickets.PrioritiesView,
                Tickets.CollaborationView, Tickets.CollaborationCreate,
                Tickets.EmailReply, Tickets.ChannelReply,
                LiveChat.View, LiveChat.Send,
                Customers.View, Customers.Update, Customers.InteractionsRead,
                Customers.NotesRead, Customers.NotesCreate, Customers.NotesUpdate,
                Customers.AttachmentsRead, Customers.AttachmentsCreate,
                // Story 26/27: read-only, both audiences — an Agent never creates/edits/publishes KB
                // content (that's Manager/Administrator only via the *Manage/*Publish permissions).
                KnowledgeBase.ArticlesView, KnowledgeBase.ArticlesViewInternal,
                KnowledgeBase.SolutionsView, KnowledgeBase.SolutionsViewInternal,
                KnowledgeBase.GuidesView, KnowledgeBase.GuidesViewInternal,
                KnowledgeBase.Search,
                Reports.View,
                AgentTasks.Read, AgentTasks.Create, AgentTasks.Update, AgentTasks.Delete, AgentTasks.Complete,
                // Correction (post-implementation): unlike Tickets.CategoriesManage (admin-only master
                // data), an Agent authors and maintains their own quick-reply catalogue day to day, so
                // Agent gets Manage here too, not just View.
                QuickReplies.View, QuickReplies.Manage,
                Notifications.ViewOwn, Notifications.MarkRead,
            },
            ["CUSTOMER"] = new HashSet<string>(StringComparer.Ordinal)
            {
                CustomerPortal.Access, Tickets.View, Tickets.Create, Tickets.Update,
                // Story 26/27: published + CustomerFacing items only — enforced server-side in each
                // service, not just by omitting the *ViewInternal codes here.
                KnowledgeBase.ArticlesView,
                KnowledgeBase.SolutionsView,
                KnowledgeBase.GuidesView,
                KnowledgeBase.Search,
            },
        };
}

/// <summary>
/// DI-friendly wrapper around <see cref="Permissions.All"/> so consumers (the seeder, the roles
/// service, <c>PermissionsController</c>) depend on an interface rather than the static class
/// directly.
/// </summary>
public interface IPermissionCatalog
{
    IReadOnlyList<PermissionDefinition> All { get; }

    bool IsValidCode(string code);

    /// <summary>
    /// The subset of <see cref="All"/> the given role is eligible to hold — the Eligible Permissions
    /// Matrix (<see cref="Permissions.EligibleBySystemRole"/>) for Manager/Agent/Customer, or the full
    /// catalogue for Administrator and any custom (non-system) role.
    /// </summary>
    IReadOnlyList<PermissionDefinition> EligibleFor(Role role);
}

public sealed class PermissionCatalog : IPermissionCatalog
{
    private readonly HashSet<string> _codes;

    public PermissionCatalog(IReadOnlyList<PermissionDefinition> all)
    {
        All = all;
        _codes = all.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<PermissionDefinition> All { get; }

    public bool IsValidCode(string code) => _codes.Contains(code);

    public IReadOnlyList<PermissionDefinition> EligibleFor(Role role)
    {
        if (!role.IsSystem || !Permissions.EligibleBySystemRole.TryGetValue(role.NormalizedName, out var eligibleCodes))
        {
            return All;
        }

        return All.Where(p => eligibleCodes.Contains(p.Code)).ToList();
    }
}
