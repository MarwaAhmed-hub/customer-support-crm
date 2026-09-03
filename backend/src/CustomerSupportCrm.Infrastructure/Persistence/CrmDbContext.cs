using CustomerSupportCrm.Domain.AgentDesk;
using CustomerSupportCrm.Domain.Audit;
using CustomerSupportCrm.Domain.Branches;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Departments;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Domain.LiveChat;
using CustomerSupportCrm.Domain.Notifications;
using CustomerSupportCrm.Domain.QuickReplies;
using CustomerSupportCrm.Domain.Roles;
using CustomerSupportCrm.Domain.Sla;
using CustomerSupportCrm.Domain.SystemSettings;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Infrastructure.Persistence;

public class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();

    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();

    public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();

    public DbSet<TicketPriority> TicketPriorities => Set<TicketPriority>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();

    public DbSet<TicketCollaborationComment> TicketCollaborationComments => Set<TicketCollaborationComment>();

    public DbSet<LiveChatSession> LiveChatSessions => Set<LiveChatSession>();

    public DbSet<AgentTask> AgentTasks => Set<AgentTask>();

    public DbSet<QuickReply> QuickReplies => Set<QuickReply>();

    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();

    public DbSet<TicketSla> TicketSlas => Set<TicketSla>();

    public DbSet<AssignmentRoundRobinCursor> AssignmentRoundRobinCursors => Set<AssignmentRoundRobinCursor>();

    public DbSet<TicketEscalation> TicketEscalations => Set<TicketEscalation>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<KnowledgeBaseCategory> KnowledgeBaseCategories => Set<KnowledgeBaseCategory>();

    public DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles => Set<KnowledgeBaseArticle>();

    public DbSet<KbSolution> KbSolutions => Set<KbSolution>();

    public DbSet<KbGuide> KbGuides => Set<KbGuide>();

    public DbSet<KbGuideStep> KbGuideSteps => Set<KbGuideStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            // The application assigns the Guid, so SQL Server must not default it with
            // NEWID()/NEWSEQUENTIALID().
            entity.Property(u => u.Id)
                  .ValueGeneratedNever();

            // Lengths are mandatory, not cosmetic: an unbounded nvarchar(max) cannot participate in
            // an index key at all, and SQL Server caps a nonclustered index key at 1700 bytes
            // (900 on a clustered key, and 900 everywhere before SQL Server 2016).
            // nvarchar(256) is 512 bytes — comfortably inside every one of those limits.
            entity.Property(u => u.Email)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(u => u.DisplayName)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(u => u.PasswordHash)
                  .IsRequired()
                  .HasMaxLength(512);

            entity.Property(u => u.IsActive)
                  .IsRequired();

            entity.Property(u => u.IsAdmin)
                  .IsRequired();

            entity.Property(u => u.CreatedAt)
                  .IsRequired();

            // Normalization is done in application code (EmailNormalizer); no explicit collation
            // is set here on purpose.
            entity.HasIndex(u => u.Email)
                  .IsUnique();

            // No navigation property either side (see the remarks on User.DepartmentId/BranchId), so
            // the target type is given explicitly via the generic HasOne<T>() overload. SetNull, not
            // Cascade/Restrict: there is no delete endpoint for a Department/Branch (only IsActive),
            // but if a row is ever removed by hand, a user should lose the reference, not be deleted
            // or block the removal.
            entity.HasOne<Department>()
                  .WithMany()
                  .HasForeignKey(u => u.DepartmentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<Branch>()
                  .WithMany()
                  .HasForeignKey(u => u.BranchId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(u => u.DepartmentId);
            entity.HasIndex(u => u.BranchId);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Id)
                  .ValueGeneratedNever();

            entity.Property(r => r.Name)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(r => r.NormalizedName)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(r => r.Description)
                  .HasMaxLength(512);

            entity.Property(r => r.IsSystem)
                  .IsRequired();

            entity.Property(r => r.CreatedAt)
                  .IsRequired();

            // Case-insensitive collisions ("Manager" vs "manager") are caught in application code by
            // comparing against NormalizedName (ToUpperInvariant()) before insert; this index is the
            // last line of defense against a race.
            entity.HasIndex(r => r.NormalizedName)
                  .IsUnique();
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(d => d.Id);

            entity.Property(d => d.Id)
                  .ValueGeneratedNever();

            entity.Property(d => d.Name)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(d => d.NormalizedName)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(d => d.Code)
                  .HasMaxLength(32);

            entity.Property(d => d.IsActive)
                  .IsRequired();

            entity.Property(d => d.CreatedAt)
                  .IsRequired();

            entity.Property(d => d.UpdatedAt)
                  .IsRequired();

            // Same pattern as Role.NormalizedName: application code compares against NormalizedName
            // (ToUpperInvariant()) before insert; this index is the last line of defense against a race.
            entity.HasIndex(d => d.NormalizedName)
                  .IsUnique();

            // Code is optional — most departments will have none — so the unique index is filtered
            // to non-null values only; SQL Server treats every NULL as distinct in a unique index by
            // default, so this filter is belt-and-suspenders documentation of that, not strictly
            // required, but matches the plan's intent explicitly rather than relying on the default.
            entity.HasIndex(d => d.Code)
                  .IsUnique()
                  .HasFilter("[Code] IS NOT NULL");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(b => b.Id);

            entity.Property(b => b.Id)
                  .ValueGeneratedNever();

            entity.Property(b => b.Name)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(b => b.NormalizedName)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(b => b.Code)
                  .HasMaxLength(32);

            entity.Property(b => b.IsActive)
                  .IsRequired();

            entity.Property(b => b.CreatedAt)
                  .IsRequired();

            entity.Property(b => b.UpdatedAt)
                  .IsRequired();

            entity.HasIndex(b => b.NormalizedName)
                  .IsUnique();

            entity.HasIndex(b => b.Code)
                  .IsUnique()
                  .HasFilter("[Code] IS NOT NULL");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                  .ValueGeneratedNever();

            entity.Property(c => c.FirstName)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(c => c.LastName)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(c => c.CompanyName)
                  .HasMaxLength(128);

            entity.Property(c => c.Email)
                  .HasMaxLength(256);

            entity.Property(c => c.Phone)
                  .HasMaxLength(64);

            entity.Property(c => c.CreatedAt)
                  .IsRequired();

            entity.Property(c => c.UpdatedAt)
                  .IsRequired();

            // No unique index on Email/Phone, and no search index on Name — Department/User carry
            // neither an equivalent, and the story explicitly gates a duplicate rule and a search
            // index on an existing convention that doesn't exist yet (see CustomersService's remarks).
        });

        modelBuilder.Entity<CustomerInteraction>(entity =>
        {
            entity.HasKey(i => i.Id);

            entity.Property(i => i.Id)
                  .ValueGeneratedNever();

            entity.Property(i => i.OccurredAt)
                  .IsRequired();

            entity.Property(i => i.InteractionType)
                  .IsRequired()
                  .HasMaxLength(64);

            entity.Property(i => i.Summary)
                  .HasMaxLength(512);

            entity.Property(i => i.Details)
                  .HasMaxLength(4000);

            entity.Property(i => i.ExternalMessageId)
                  .HasMaxLength(255);

            entity.Property(i => i.InReplyToMessageId)
                  .HasMaxLength(255);

            entity.Property(i => i.FromAddress)
                  .HasMaxLength(320);

            entity.Property(i => i.ToAddress)
                  .HasMaxLength(320);

            entity.Property(i => i.CreatedAt)
                  .IsRequired();

            // Deleting a customer removes their interaction history with them (Cascade); a deleted
            // user leaves past interactions in place with UserId reset to null (SetNull) — see the
            // "deleted user" edge case in Story 08.
            entity.HasOne(i => i.Customer)
                  .WithMany()
                  .HasForeignKey(i => i.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.User)
                  .WithMany()
                  .HasForeignKey(i => i.UserId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Story 19: Restrict, matching AgentTask.TicketId — a ticket is never hard-deleted (no
            // such endpoint exists), so this never actually blocks a delete; it just documents that an
            // interaction should not silently disappear if that ever changes.
            entity.HasOne(i => i.Ticket)
                  .WithMany()
                  .HasForeignKey(i => i.TicketId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Powers the newest-first-per-customer listing query.
            entity.HasIndex(i => new { i.CustomerId, i.OccurredAt })
                  .IsDescending(false, true);

            // Story 19: fast lookup for the two email flows — idempotent re-ingestion (find by this
            // message's own id) and reply threading (find by the id it's replying to). Not unique: the
            // application layer is the source of truth for the idempotency check, not a DB constraint.
            entity.HasIndex(i => i.ExternalMessageId);

            // Story 19: "every interaction for this ticket" — used to find the latest inbound
            // interaction's ExternalMessageId when composing a reply's In-Reply-To header.
            entity.HasIndex(i => i.TicketId);
        });

        modelBuilder.Entity<CustomerNote>(entity =>
        {
            entity.HasKey(n => n.Id);

            entity.Property(n => n.Id)
                  .ValueGeneratedNever();

            entity.Property(n => n.Body)
                  .IsRequired()
                  .HasMaxLength(4000);

            entity.Property(n => n.CreatedAt)
                  .IsRequired();

            // Never orphan a note — a customer's notes only ever go away with the customer itself
            // (there is no delete-customer-only-not-its-notes case in this story).
            entity.HasOne<Customer>()
                  .WithMany()
                  .HasForeignKey(n => n.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            // A deleted author's past notes stay, with CreatedByUserId reset to null — same pattern
            // as CustomerInteraction.UserId.
            entity.HasOne(n => n.CreatedByUser)
                  .WithMany()
                  .HasForeignKey(n => n.CreatedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(n => n.CustomerId);
        });

        modelBuilder.Entity<AgentTask>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Id)
                  .ValueGeneratedNever();

            entity.Property(t => t.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(t => t.Description)
                  .HasMaxLength(4000);

            entity.Property(t => t.CreatedAt)
                  .IsRequired();

            entity.Property(t => t.UpdatedAt)
                  .IsRequired();

            // Cascade, unlike CustomerNote's SetNull author FK: OwnerUserId is required (a task with
            // no owner has no meaning), so a deleted user's personal task list is deleted with them
            // rather than left as unowned rows nothing can ever read again (every query is scoped to
            // OwnerUserId — see AgentTasksService).
            entity.HasOne(t => t.OwnerUser)
                  .WithMany()
                  .HasForeignKey(t => t.OwnerUserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict, matching every FK on Ticket itself (CustomerId, CategoryId, ...): a ticket is
            // never hard-deleted, only its status changes, so there is no cascade/SetNull case here.
            entity.HasOne(t => t.Ticket)
                  .WithMany()
                  .HasForeignKey(t => t.TicketId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Powers "my tasks" (OwnerUserId alone), the completed/reminder filters layered on top,
            // and "tasks for this ticket" (OwnerUserId + TicketId, from the ticket detail page).
            entity.HasIndex(t => new { t.OwnerUserId, t.CompletedAt });
            entity.HasIndex(t => new { t.OwnerUserId, t.ReminderAt });
            entity.HasIndex(t => t.TicketId);
        });

        modelBuilder.Entity<CustomerAttachment>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id)
                  .ValueGeneratedNever();

            entity.Property(a => a.FileName)
                  .IsRequired()
                  .HasMaxLength(260);

            entity.Property(a => a.StoredFileName)
                  .IsRequired()
                  .HasMaxLength(64);

            entity.Property(a => a.ContentType)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(a => a.SizeBytes)
                  .IsRequired();

            entity.Property(a => a.UploadedAt)
                  .IsRequired();

            // Never orphan an attachment row — see CustomerNote's remarks above; the physical file is
            // removed by CustomerAttachmentsService.DeleteAsync, not by a DB cascade.
            entity.HasOne<Customer>()
                  .WithMany()
                  .HasForeignKey(a => a.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.UploadedByUser)
                  .WithMany()
                  .HasForeignKey(a => a.UploadedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(a => a.CustomerId);
        });

        modelBuilder.Entity<TicketCategory>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                  .ValueGeneratedNever();

            entity.Property(c => c.Name)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(c => c.NormalizedName)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(c => c.Description)
                  .HasMaxLength(512);

            entity.Property(c => c.IsActive)
                  .IsRequired();

            entity.Property(c => c.CreatedAt)
                  .IsRequired();

            entity.Property(c => c.UpdatedAt)
                  .IsRequired();

            // Same pattern as Department/Branch's NormalizedName: application code compares against
            // it (ToUpperInvariant()) before insert; this index is the last line of defense against a
            // race. No Code column here — categories/priorities are identified by Name alone.
            entity.HasIndex(c => c.NormalizedName)
                  .IsUnique();

            // Shadow FK, no navigation property — same pattern as User.DepartmentId (see its remarks):
            // SetNull rather than Restrict/Cascade, since a Department row is deactivated rather than
            // hard-deleted, but a category should not be able to block or be dragged into a removal if
            // one ever is deleted by hand.
            entity.HasOne<Department>()
                  .WithMany()
                  .HasForeignKey(c => c.DepartmentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(c => c.DepartmentId);
        });

        modelBuilder.Entity<QuickReply>(entity =>
        {
            entity.HasKey(q => q.Id);

            entity.Property(q => q.Id)
                  .ValueGeneratedNever();

            entity.Property(q => q.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(q => q.NormalizedTitle)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(q => q.Body)
                  .IsRequired()
                  .HasMaxLength(5000);

            entity.Property(q => q.IsActive)
                  .IsRequired();

            entity.Property(q => q.CreatedAt)
                  .IsRequired();

            entity.Property(q => q.UpdatedAt)
                  .IsRequired();

            // Same pattern as TicketCategory/Department/Branch's NormalizedName.
            entity.HasIndex(q => q.NormalizedTitle)
                  .IsUnique();
        });

        modelBuilder.Entity<TicketPriority>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id)
                  .ValueGeneratedNever();

            entity.Property(p => p.Name)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(p => p.NormalizedName)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(p => p.SortOrder)
                  .IsRequired();

            entity.Property(p => p.Description)
                  .HasMaxLength(512);

            entity.Property(p => p.IsActive)
                  .IsRequired();

            entity.Property(p => p.CreatedAt)
                  .IsRequired();

            entity.Property(p => p.UpdatedAt)
                  .IsRequired();

            entity.HasIndex(p => p.NormalizedName)
                  .IsUnique();

            // Powers the SortOrder-ascending listing query. Not unique — ties are allowed and break
            // by Name (see TicketPrioritiesService.ListAsync).
            entity.HasIndex(p => p.SortOrder);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Id)
                  .ValueGeneratedNever();

            entity.Property(t => t.Subject)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(t => t.Description)
                  .IsRequired()
                  .HasMaxLength(4000);

            entity.Property(t => t.Status)
                  .IsRequired()
                  .HasMaxLength(32);

            entity.Property(t => t.EscalationReason)
                  .HasMaxLength(1024);

            entity.Property(t => t.SourceChannel)
                  .HasMaxLength(32);

            entity.Property(t => t.ExternalConversationId)
                  .HasMaxLength(200);

            entity.Property(t => t.CreatedAt)
                  .IsRequired();

            entity.Property(t => t.UpdatedAt)
                  .IsRequired();

            // Restrict, not Cascade/SetNull, on every FK below: a ticket must always resolve to a
            // real customer/category/priority/creator, and Story 10's categories/priorities are
            // deactivated (IsActive = false) rather than deleted, so a hard delete racing a ticket
            // create is not a case this story needs to handle.
            entity.HasOne(t => t.Customer)
                  .WithMany()
                  .HasForeignKey(t => t.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Category)
                  .WithMany()
                  .HasForeignKey(t => t.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Priority)
                  .WithMany()
                  .HasForeignKey(t => t.PriorityId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.CreatedByUser)
                  .WithMany()
                  .HasForeignKey(t => t.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Story 12: nullable, unlike CreatedByUser — a ticket starts unassigned. Restrict, same
            // reasoning as the FKs above: a user row is never hard-deleted (only deactivated), so
            // there is no cascade/SetNull case this story needs to handle.
            entity.HasOne(t => t.AssignedUser)
                  .WithMany()
                  .HasForeignKey(t => t.AssignedUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Story 13: same nullable/Restrict reasoning as AssignedUser above — a ticket starts
            // de-escalated, and a user row is never hard-deleted.
            entity.HasOne(t => t.EscalatedByUser)
                  .WithMany()
                  .HasForeignKey(t => t.EscalatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => t.CustomerId);
            entity.HasIndex(t => t.AssignedUserId);
            entity.HasIndex(t => t.CategoryId);
            entity.HasIndex(t => t.PriorityId);
            entity.HasIndex(t => t.CreatedByUserId);
            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.CreatedAt);
            entity.HasIndex(t => t.IsEscalated);

            // Story 20: powers InboundMessageService's ExternalConversationId lookup — find the ticket
            // for this channel + provider conversation id in one indexed query.
            entity.HasIndex(t => new { t.SourceChannel, t.ExternalConversationId });
        });

        modelBuilder.Entity<SlaPolicy>(entity =>
        {
            entity.ToTable("SlaPolicies");

            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id)
                  .ValueGeneratedNever();

            entity.Property(p => p.Name)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(p => p.IsActive)
                  .IsRequired();

            entity.Property(p => p.CreatedAt)
                  .IsRequired();

            entity.Property(p => p.UpdatedAt)
                  .IsRequired();

            // Restrict, matching every other Priority FK (Ticket.Priority): TicketPriority rows are
            // deactivated, never hard-deleted, so this never dangles.
            entity.HasOne(p => p.Priority)
                  .WithMany()
                  .HasForeignKey(p => p.PriorityId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Story 22: at most one active policy per priority (including one active default where
            // PriorityId is null) — enforced here, not in application code, so a race between two
            // concurrent "activate this policy" edits can't leave two active rows for the same priority.
            entity.HasIndex(p => p.PriorityId)
                  .IsUnique()
                  .HasFilter("[IsActive] = 1");
        });

        modelBuilder.Entity<TicketSla>(entity =>
        {
            entity.ToTable("TicketSlas");

            entity.HasKey(s => s.Id);

            entity.Property(s => s.Id)
                  .ValueGeneratedNever();

            entity.Property(s => s.FirstResponseStatus)
                  .IsRequired()
                  .HasMaxLength(16);

            entity.Property(s => s.ResolutionStatus)
                  .IsRequired()
                  .HasMaxLength(16);

            entity.Property(s => s.StartedAt)
                  .IsRequired();

            entity.Property(s => s.FirstResponseDueAt)
                  .IsRequired();

            entity.Property(s => s.ResolutionDueAt)
                  .IsRequired();

            entity.Property(s => s.CreatedAt)
                  .IsRequired();

            entity.Property(s => s.UpdatedAt)
                  .IsRequired();

            // Cascade, matching TicketHistory/TicketCollaborationComment: an SLA row has no meaning
            // once its ticket is gone.
            entity.HasOne(s => s.Ticket)
                  .WithMany()
                  .HasForeignKey(s => s.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict — the policy snapshot must survive even if the SlaPolicy row it was copied from
            // is later deactivated or edited; it is never deleted out from under an in-flight ticket.
            entity.HasOne(s => s.SlaPolicy)
                  .WithMany()
                  .HasForeignKey(s => s.SlaPolicyId)
                  .OnDelete(DeleteBehavior.Restrict);

            // One SLA row per ticket.
            entity.HasIndex(s => s.TicketId)
                  .IsUnique();

            // Power the (future, Stories 24/25) breach-scan query: "every ticket still Running whose
            // due time has passed" without a table scan.
            entity.HasIndex(s => s.FirstResponseStatus);
            entity.HasIndex(s => s.ResolutionStatus);
        });

        modelBuilder.Entity<TicketHistory>(entity =>
        {
            entity.ToTable("TicketHistories");

            entity.HasKey(h => h.Id);

            entity.Property(h => h.Id)
                  .ValueGeneratedNever();

            entity.Property(h => h.EventType)
                  .IsRequired()
                  .HasMaxLength(40);

            entity.Property(h => h.Field)
                  .HasMaxLength(80);

            entity.Property(h => h.PreviousValue)
                  .HasMaxLength(512);

            entity.Property(h => h.NewValue)
                  .HasMaxLength(512);

            entity.Property(h => h.Summary)
                  .IsRequired()
                  .HasMaxLength(512);

            entity.Property(h => h.CreatedAt)
                  .IsRequired();

            // Cascade, unlike every FK on Ticket itself: a history row has no meaning once its ticket
            // is gone, so it should not block (Restrict) or need to be deleted separately.
            entity.HasOne(h => h.Ticket)
                  .WithMany()
                  .HasForeignKey(h => h.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict, matching every other user FK on Ticket (AssignedUser, EscalatedByUser): a
            // user row is never hard-deleted, only deactivated.
            entity.HasOne(h => h.PerformedByUser)
                  .WithMany()
                  .HasForeignKey(h => h.PerformedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Powers "history for ticket X, oldest first" — the only query this table serves.
            entity.HasIndex(h => new { h.TicketId, h.CreatedAt });
        });

        modelBuilder.Entity<TicketCollaborationComment>(entity =>
        {
            entity.ToTable("TicketCollaborationComments");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                  .ValueGeneratedNever();

            entity.Property(c => c.Body)
                  .IsRequired()
                  .HasMaxLength(4000);

            entity.Property(c => c.CreatedAt)
                  .IsRequired();

            // Cascade, matching TicketHistory: a comment has no meaning once its ticket is gone.
            entity.HasOne(c => c.Ticket)
                  .WithMany()
                  .HasForeignKey(c => c.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict, matching TicketHistory.PerformedByUser: a user row is never hard-deleted, only
            // deactivated, so a comment's author reference never dangles.
            entity.HasOne(c => c.AuthorUser)
                  .WithMany()
                  .HasForeignKey(c => c.AuthorUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Powers "comments for ticket X, oldest first" — the only query this table serves.
            entity.HasIndex(c => new { c.TicketId, c.CreatedAt });
        });

        modelBuilder.Entity<LiveChatSession>(entity =>
        {
            entity.ToTable("LiveChatSessions");

            entity.HasKey(s => s.Id);

            entity.Property(s => s.Id)
                  .ValueGeneratedNever();

            entity.Property(s => s.SessionToken)
                  .IsRequired()
                  .HasMaxLength(64);

            entity.Property(s => s.CreatedAt)
                  .IsRequired();

            // Restrict, matching every other Ticket/Customer FK on a Story 19-21 sub-record: neither
            // is ever hard-deleted, so there is no cascade/SetNull case this story needs to handle.
            entity.HasOne(s => s.Ticket)
                  .WithMany()
                  .HasForeignKey(s => s.TicketId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Customer)
                  .WithMany()
                  .HasForeignKey(s => s.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            // One session per ticket — a closed chat's ticket is never reused for a new session.
            entity.HasIndex(s => s.TicketId).IsUnique();

            // The anonymous widget's only credential — must resolve to exactly one session.
            entity.HasIndex(s => s.SessionToken).IsUnique();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id)
                  .ValueGeneratedNever();

            entity.Property(p => p.Code)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(p => p.Category)
                  .IsRequired()
                  .HasMaxLength(64);

            entity.Property(p => p.DisplayName)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(p => p.Description)
                  .HasMaxLength(512);

            entity.HasIndex(p => p.Code)
                  .IsUnique();
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            entity.HasOne(rp => rp.Role)
                  .WithMany(r => r.RolePermissions)
                  .HasForeignKey(rp => rp.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(rp => rp.Permission)
                  .WithMany()
                  .HasForeignKey(rp => rp.PermissionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.Property(ur => ur.AssignedAt)
                  .IsRequired();

            entity.HasOne(ur => ur.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(ur => ur.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ur => ur.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(ur => ur.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id)
                  .ValueGeneratedNever();

            entity.Property(a => a.Action)
                  .IsRequired()
                  .HasMaxLength(128);

            entity.Property(a => a.Summary)
                  .IsRequired()
                  .HasMaxLength(512);

            entity.Property(a => a.ActorEmail)
                  .HasMaxLength(256);

            entity.Property(a => a.EntityType)
                  .HasMaxLength(64);

            entity.Property(a => a.EntityId)
                  .HasMaxLength(64);

            entity.Property(a => a.IpAddress)
                  .HasMaxLength(64);

            entity.Property(a => a.UserAgent)
                  .HasMaxLength(512);

            // Metadata can be large (capped at ~8 KB by the service)
            entity.Property(a => a.MetadataJson)
                  .HasColumnType("nvarchar(max)");

            // Support efficient filter + sort queries
            entity.HasIndex(a => new { a.OccurredAtUtc })
                  .IsDescending(new[] { true });

            entity.HasIndex(a => new { a.Action, a.OccurredAtUtc })
                  .IsDescending(new[] { false, true });
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(s => s.Id);

            // Application assigns Id = 1 explicitly (single-tenant, single-row) — same
            // ValueGeneratedNever reasoning as User.Id/Role.Id, just with an int surrogate instead
            // of a Guid, since there is exactly one row and no client ever picks its own id.
            entity.Property(s => s.Id)
                  .ValueGeneratedNever();

            entity.Property(s => s.ApplicationName)
                  .IsRequired()
                  .HasMaxLength(120);

            entity.Property(s => s.SupportEmail)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(s => s.DefaultTimezone)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(s => s.DefaultCulture)
                  .IsRequired()
                  .HasMaxLength(20);

            entity.Property(s => s.BrandDisplayName)
                  .IsRequired()
                  .HasMaxLength(120);

            entity.Property(s => s.LogoUrl)
                  .HasMaxLength(500)
                  .IsRequired(false);

            entity.Property(s => s.PrimaryColor)
                  .IsRequired()
                  .HasMaxLength(7);

            entity.Property(s => s.SecondaryColor)
                  .IsRequired()
                  .HasMaxLength(7);

            entity.Property(s => s.UpdatedAtUtc)
                  .IsRequired();
        });

        // Story 23: one row per department — the department id IS the primary key, not a separate
        // surrogate one, since there is at most one cursor per department by definition.
        modelBuilder.Entity<AssignmentRoundRobinCursor>(entity =>
        {
            entity.HasKey(c => c.DepartmentId);

            entity.Property(c => c.DepartmentId)
                  .ValueGeneratedNever();

            entity.Property(c => c.LastAssignedUserId)
                  .IsRequired();

            entity.Property(c => c.UpdatedAt)
                  .IsRequired();
        });

        // Story 24: append-only records of SLA warning/breach milestones. Cascade on Ticket delete
        // (unlike TicketHistory/TicketSla's own FKs, which this mirrors) — there is no scenario where
        // a ticket is hard-deleted today, but if one is ever added, its escalation trail should go
        // with it rather than orphan or block the delete.
        modelBuilder.Entity<TicketEscalation>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedNever();

            entity.Property(e => e.SlaType)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(e => e.Milestone)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(e => e.TargetRole)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(e => e.ThresholdAtUtc)
                  .IsRequired();

            entity.Property(e => e.CreatedAtUtc)
                  .IsRequired();

            entity.Property(e => e.WasUnassigned)
                  .IsRequired();

            entity.Property(e => e.Notes)
                  .HasMaxLength(512);

            entity.HasOne(e => e.Ticket)
                  .WithMany()
                  .HasForeignKey(e => e.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Idempotency (Story 24 Acceptance 7): at most one row per milestone per ticket. This is
            // the actual source of truth for "don't duplicate" under concurrent evaluator runs;
            // SlaEscalationService's own existence check handles the ordinary non-concurrent case.
            entity.HasIndex(e => new { e.TicketId, e.SlaType, e.Milestone })
                  .IsUnique();

            // No navigation property on User for "my escalations" (matching User.DepartmentId/
            // BranchId's own no-nav convention) — a shadow FK, SetNull rather than Restrict/Cascade,
            // since TargetUserId is informational routing, not a resource a query depends on existing.
            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(e => e.TargetUserId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.TargetUserId, e.CreatedAtUtc });
        });

        // Story 25: in-app notifications for a signed-in staff member. Cascade on Ticket delete,
        // matching TicketEscalation's own FK — there is no scenario where a ticket is hard-deleted
        // today, but a notification about a ticket that no longer exists is meaningless either way.
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(n => n.Id);

            entity.Property(n => n.Id)
                  .ValueGeneratedNever();

            entity.Property(n => n.EventType)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(n => n.SlaType)
                  .HasConversion<int?>();

            entity.Property(n => n.RecipientUserId)
                  .IsRequired();

            entity.Property(n => n.Subject)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(n => n.Body)
                  .IsRequired()
                  .HasMaxLength(4000);

            entity.Property(n => n.DedupeKey)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(n => n.CreatedAtUtc)
                  .IsRequired();

            entity.HasOne(n => n.Ticket)
                  .WithMany()
                  .HasForeignKey(n => n.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);

            // No navigation property on User (matching TicketEscalation.TargetUserId's own shadow-FK
            // convention) — Restrict, not Cascade/SetNull: unlike TargetUserId, RecipientUserId is
            // required (a row is never created without one — see NotificationService), so there is no
            // meaningful "orphaned" state to fall back to.
            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(n => n.RecipientUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Idempotency backstop (Story 25 Acceptance: duplicate event replay produces a single
            // row) — same role TicketEscalation's own unique index plays for Story 24.
            entity.HasIndex(n => n.DedupeKey)
                  .IsUnique();

            // The inbox list query's own access pattern: "my unread notifications, newest first".
            entity.HasIndex(n => new { n.RecipientUserId, n.ReadAtUtc });
        });

        // Story 26: FAQs and Help Articles master data — same shape/rationale as Department/Branch.
        modelBuilder.Entity<KnowledgeBaseCategory>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id)
                  .ValueGeneratedNever();

            entity.Property(c => c.Name)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.HasIndex(c => c.NormalizedName)
                  .IsUnique();

            entity.Property(c => c.IsActive)
                  .IsRequired();

            entity.Property(c => c.CreatedAtUtc)
                  .IsRequired();
        });

        modelBuilder.Entity<KnowledgeBaseArticle>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id)
                  .ValueGeneratedNever();

            entity.Property(a => a.ContentType)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(a => a.Audience)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(a => a.Status)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(a => a.Title)
                  .IsRequired()
                  .HasMaxLength(400);

            // No max length — nvarchar(max), matching Ticket.Description's own "long-form, no cap"
            // treatment (Story 26's Help Article content can be arbitrarily long).
            entity.Property(a => a.Body)
                  .IsRequired();

            entity.Property(a => a.CreatedAtUtc)
                  .IsRequired();

            // Restrict, not Cascade — a category is deactivated (IsActive = false) rather than
            // deleted in the ordinary case, and KnowledgeBaseCategoriesService.DeleteAsync itself
            // blocks a real delete while any article still references the row (see its remarks) —
            // Restrict here is just the DB-level backstop for that same rule.
            entity.HasOne(a => a.Category)
                  .WithMany()
                  .HasForeignKey(a => a.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Story 28 (Knowledge Base Search) filters on exactly this combination.
            entity.HasIndex(a => new { a.ContentType, a.Status, a.Audience });

            entity.HasIndex(a => a.CategoryId);
        });

        // Story 27: Solutions — same Category/Audience/Status shape as KnowledgeBaseArticle above.
        modelBuilder.Entity<KbSolution>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Id)
                  .ValueGeneratedNever();

            entity.Property(s => s.Audience)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(s => s.Status)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(s => s.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(s => s.Problem)
                  .IsRequired();

            entity.Property(s => s.SolutionBody)
                  .IsRequired();

            entity.Property(s => s.CreatedAtUtc)
                  .IsRequired();

            // Restrict — same reasoning as KnowledgeBaseArticle.Category above:
            // KnowledgeBaseCategoriesService.DeleteAsync blocks the real delete at the application
            // level; this is the DB-level backstop.
            entity.HasOne(s => s.Category)
                  .WithMany()
                  .HasForeignKey(s => s.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(s => new { s.Status, s.Audience });
            entity.HasIndex(s => s.CategoryId);
        });

        // Story 27: Guides — same Category/Audience/Status shape, plus an ordered Steps collection.
        modelBuilder.Entity<KbGuide>(entity =>
        {
            entity.HasKey(g => g.Id);

            entity.Property(g => g.Id)
                  .ValueGeneratedNever();

            entity.Property(g => g.Audience)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(g => g.Status)
                  .HasConversion<int>()
                  .IsRequired();

            entity.Property(g => g.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(g => g.Description)
                  .IsRequired();

            entity.Property(g => g.CreatedAtUtc)
                  .IsRequired();

            entity.HasOne(g => g.Category)
                  .WithMany()
                  .HasForeignKey(g => g.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(g => new { g.Status, g.Audience });
            entity.HasIndex(g => g.CategoryId);

            // Cascade — unlike Category (data the guide merely references), Steps belong exclusively
            // to their Guide; deleting a Guide should delete its Steps.
            entity.HasMany(g => g.Steps)
                  .WithOne(step => step.Guide)
                  .HasForeignKey(step => step.GuideId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KbGuideStep>(entity =>
        {
            entity.HasKey(step => step.Id);

            entity.Property(step => step.Id)
                  .ValueGeneratedNever();

            entity.Property(step => step.Instruction)
                  .IsRequired();

            // Not unique — KbGuidesService.UpdateAsync replaces the whole collection (delete + re-add)
            // rather than diffing in place, so a transient duplicate order during that replace must
            // never fail the transaction; ordering is an application-level invariant, this index is
            // purely for the "load a guide's steps in order" query.
            entity.HasIndex(step => new { step.GuideId, step.Order });
        });
    }
}
