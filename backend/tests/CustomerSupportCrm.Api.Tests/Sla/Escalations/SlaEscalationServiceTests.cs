using CustomerSupportCrm.Api.Sla.Escalations;
using CustomerSupportCrm.Domain.Branches;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Departments;
using CustomerSupportCrm.Domain.Roles;
using CustomerSupportCrm.Domain.Sla;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Sla.Escalations;

/// <summary>
/// Story 24: <see cref="SlaEscalationService"/> — Warning (80% elapsed) / Breach (100% elapsed)
/// detection, idempotency, independent First Response / Resolution evaluation, and routing
/// (Agent/Manager/Administrator — never Customer, which is structurally guaranteed since
/// <see cref="EscalationTargetRole"/> has no Customer member at all).
/// </summary>
public class SlaEscalationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SlaEscalationService CreateService(CrmDbContext db) =>
        new(db, NullLogger<SlaEscalationService>.Instance);

    private static async Task<T> AddAsync<T>(CrmDbContext db, T entity) where T : class
    {
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    private static async Task<(CrmDbContext db, Customer customer, TicketCategory category, TicketPriority priority, User creator)> SeedBaseAsync()
    {
        var db = CreateDb();
        var customer = await AddAsync(db, new Customer { FirstName = "Jane", LastName = "Doe" });
        var category = await AddAsync(db, new TicketCategory { Name = "General", NormalizedName = "GENERAL" });
        var priority = await AddAsync(db, new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 });
        var creator = await AddAsync(db, new User { Email = "creator@local.test", DisplayName = "Creator", PasswordHash = "x" });
        return (db, customer, category, priority, creator);
    }

    private static Ticket NewTicket(
        Customer customer, TicketCategory category, TicketPriority priority, Guid createdByUserId,
        Guid? assignedUserId = null, string status = "open", DateTimeOffset? createdAt = null)
    {
        var created = createdAt ?? Now.AddHours(-1);
        return new Ticket
        {
            CustomerId = customer.Id, Subject = "Subject", Description = "Description",
            CategoryId = category.Id, PriorityId = priority.Id, CreatedByUserId = createdByUserId,
            AssignedUserId = assignedUserId, Status = status, CreatedAt = created, UpdatedAt = created,
        };
    }

    /// <summary>1 hour to First Response, 4 hours to Resolution, both starting at the ticket's CreatedAt.</summary>
    private static async Task<TicketSla> SeedSlaAsync(CrmDbContext db, Ticket ticket, DateTimeOffset? firstResponseAt = null)
    {
        var policy = await AddAsync(db, new SlaPolicy { PriorityId = null, Name = "Default", FirstResponseMinutes = 60, ResolutionMinutes = 240 });
        return await AddAsync(db, new TicketSla
        {
            TicketId = ticket.Id,
            SlaPolicyId = policy.Id,
            StartedAt = ticket.CreatedAt,
            FirstResponseDueAt = ticket.CreatedAt.AddMinutes(60),
            ResolutionDueAt = ticket.CreatedAt.AddMinutes(240),
            FirstResponseAt = firstResponseAt,
        });
    }

    private static async Task<(Department department, Branch branch)> SeedOrgAsync(CrmDbContext db)
    {
        var department = await AddAsync(db, new Department { Name = "Support", NormalizedName = "SUPPORT" });
        var branch = await AddAsync(db, new Branch { Name = "HQ", NormalizedName = "HQ" });
        return (department, branch);
    }

    private static async Task<Role> GetOrAddRoleAsync(CrmDbContext db, string normalizedName)
    {
        var existing = await db.Roles.SingleOrDefaultAsync(r => r.NormalizedName == normalizedName);
        if (existing is not null) return existing;
        return await AddAsync(db, new Role { Name = normalizedName, NormalizedName = normalizedName, IsSystem = true });
    }

    private static async Task<User> AddUserWithRoleAsync(CrmDbContext db, string email, string roleNormalizedName, Guid? departmentId = null, Guid? branchId = null, bool isActive = true, DateTimeOffset? createdAt = null)
    {
        var role = await GetOrAddRoleAsync(db, roleNormalizedName);
        var user = await AddAsync(db, new User
        {
            Email = email, DisplayName = email, PasswordHash = "x", DepartmentId = departmentId, BranchId = branchId,
            IsActive = isActive, CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        });
        await AddAsync(db, new UserRole { UserId = user.Id, RoleId = role.Id });
        return user;
    }

    [Fact]
    public async Task EvaluateAsync_before_warning_threshold_creates_no_records()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, createdAt: Now));
        await SeedSlaAsync(db, ticket);
        var service = CreateService(db);

        // 30 minutes in: 50% of the 60-minute First Response window — below the 80% warning line.
        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(30));

        Assert.Empty(result);
        Assert.Empty(await db.TicketEscalations.Where(e => e.TicketId == ticket.Id).ToListAsync());
    }

    [Fact]
    public async Task EvaluateAsync_at_warning_threshold_creates_single_warning_row()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT");
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now));
        await SeedSlaAsync(db, ticket);
        var service = CreateService(db);

        // 50 minutes in: 83% of the 60-minute First Response window — past warning, before breach (60m).
        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(50));

        var firstResponseRows = result.Where(r => r.SlaType == SlaType.FirstResponse).ToList();
        Assert.Single(firstResponseRows);
        Assert.Equal(EscalationMilestone.Warning, firstResponseRows[0].Milestone);
        Assert.DoesNotContain(result, r => r.SlaType == SlaType.Resolution);
    }

    [Fact]
    public async Task EvaluateAsync_at_breach_threshold_creates_warning_and_breach_rows_once()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT");
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now));
        await SeedSlaAsync(db, ticket);
        var service = CreateService(db);

        // 65 minutes in: past the 60-minute First Response due time — both Warning and Breach apply.
        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(65));

        var firstResponseRows = result.Where(r => r.SlaType == SlaType.FirstResponse).ToList();
        Assert.Equal(2, firstResponseRows.Count);
        Assert.Contains(firstResponseRows, r => r.Milestone == EscalationMilestone.Warning);
        Assert.Contains(firstResponseRows, r => r.Milestone == EscalationMilestone.Breach);
    }

    [Fact]
    public async Task EvaluateAsync_is_idempotent_across_repeated_calls()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT");
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now));
        await SeedSlaAsync(db, ticket);
        var service = CreateService(db);

        var first = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(65));
        var second = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(65));

        Assert.NotEmpty(first);
        Assert.Empty(second);
        Assert.Equal(first.Count, await db.TicketEscalations.CountAsync(e => e.TicketId == ticket.Id));
    }

    [Fact]
    public async Task Assigned_ticket_warning_targets_assigned_agent()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT");
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now));
        await SeedSlaAsync(db, ticket);
        var service = CreateService(db);

        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(50));

        var warning = Assert.Single(result, r => r.SlaType == SlaType.FirstResponse && r.Milestone == EscalationMilestone.Warning);
        Assert.Equal(EscalationTargetRole.Agent, warning.TargetRole);
        Assert.Equal(agent.Id, warning.TargetUserId);
        Assert.False(warning.WasUnassigned);
    }

    [Fact]
    public async Task Assigned_ticket_breach_targets_department_manager()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var (department, _) = await SeedOrgAsync(db);
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT", departmentId: department.Id);
        var manager = await AddUserWithRoleAsync(db, "manager@local.test", "MANAGER", departmentId: department.Id);
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now));
        await SeedSlaAsync(db, ticket);
        var service = CreateService(db);

        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(65));

        var breach = Assert.Single(result, r => r.SlaType == SlaType.FirstResponse && r.Milestone == EscalationMilestone.Breach);
        Assert.Equal(EscalationTargetRole.Manager, breach.TargetRole);
        Assert.Equal(manager.Id, breach.TargetUserId);
        Assert.Null(breach.Notes);
    }

    [Fact]
    public async Task Assigned_ticket_breach_falls_back_to_branch_manager_when_department_has_none()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var (department, branch) = await SeedOrgAsync(db);
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT", departmentId: department.Id, branchId: branch.Id);
        // Manager exists only at the Branch level, not in the agent's Department.
        var branchManager = await AddUserWithRoleAsync(db, "branch-manager@local.test", "MANAGER", branchId: branch.Id);
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now));
        await SeedSlaAsync(db, ticket);
        var service = CreateService(db);

        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(65));

        var breach = Assert.Single(result, r => r.SlaType == SlaType.FirstResponse && r.Milestone == EscalationMilestone.Breach);
        Assert.Equal(EscalationTargetRole.Manager, breach.TargetRole);
        Assert.Equal(branchManager.Id, breach.TargetUserId);
    }

    [Fact]
    public async Task Assigned_ticket_breach_falls_back_to_administrator_when_no_manager_exists()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var (department, _) = await SeedOrgAsync(db);
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT", departmentId: department.Id);
        var administrator = await AddUserWithRoleAsync(db, "admin@local.test", "ADMINISTRATOR");
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now));
        await SeedSlaAsync(db, ticket);
        var service = CreateService(db);

        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(65));

        var breach = Assert.Single(result, r => r.SlaType == SlaType.FirstResponse && r.Milestone == EscalationMilestone.Breach);
        Assert.Equal(EscalationTargetRole.Administrator, breach.TargetRole);
        Assert.Equal(administrator.Id, breach.TargetUserId);
        Assert.Equal("no manager resolved; fell back to administrator", breach.Notes);
    }

    [Fact]
    public async Task Unassigned_ticket_warning_and_breach_both_target_administrator()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var administrator = await AddUserWithRoleAsync(db, "admin@local.test", "ADMINISTRATOR");
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: null, createdAt: Now));
        await SeedSlaAsync(db, ticket);
        var service = CreateService(db);

        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(65));

        var firstResponseRows = result.Where(r => r.SlaType == SlaType.FirstResponse).ToList();
        Assert.Equal(2, firstResponseRows.Count);
        Assert.All(firstResponseRows, r =>
        {
            Assert.Equal(EscalationTargetRole.Administrator, r.TargetRole);
            Assert.Equal(administrator.Id, r.TargetUserId);
            Assert.True(r.WasUnassigned);
        });
    }

    [Fact]
    public async Task First_response_breach_does_not_create_any_resolution_escalation()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT");
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now));
        await SeedSlaAsync(db, ticket);
        var service = CreateService(db);

        // 65 minutes in: First Response (60m) is breached, but Resolution (240m) isn't even at warning
        // (80% of 240m = 192m) yet — the two must be evaluated completely independently.
        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(65));

        Assert.Contains(result, r => r.SlaType == SlaType.FirstResponse);
        Assert.DoesNotContain(result, r => r.SlaType == SlaType.Resolution);
    }

    [Fact]
    public async Task Resolution_evaluated_independently_of_an_already_satisfied_first_response()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT");
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now));
        // First Response was satisfied well within its window.
        await SeedSlaAsync(db, ticket, firstResponseAt: Now.AddMinutes(10));
        var service = CreateService(db);

        // 200 minutes in: past Resolution's 192-minute (80%) warning line, First Response long since done.
        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(200));

        Assert.DoesNotContain(result, r => r.SlaType == SlaType.FirstResponse);
        Assert.Single(result, r => r.SlaType == SlaType.Resolution && r.Milestone == EscalationMilestone.Warning);
    }

    [Fact]
    public async Task First_response_satisfied_before_the_warning_threshold_stops_further_first_response_escalations()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT");
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now));
        await SeedSlaAsync(db, ticket, firstResponseAt: Now.AddMinutes(10));
        var service = CreateService(db);

        // Evaluated well past both First Response milestones — satisfied, so neither ever fires.
        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(65));

        Assert.DoesNotContain(result, r => r.SlaType == SlaType.FirstResponse);
    }

    [Fact]
    public async Task Resolved_ticket_stops_further_resolution_escalations()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT");
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, status: "resolved", createdAt: Now));
        await SeedSlaAsync(db, ticket, firstResponseAt: Now.AddMinutes(10));
        var service = CreateService(db);

        // Evaluated well past the Resolution due time — but the ticket is already Resolved.
        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(300));

        Assert.DoesNotContain(result, r => r.SlaType == SlaType.Resolution);
    }

    [Fact]
    public async Task EvaluateAsync_returns_empty_for_a_ticket_with_no_TicketSla_row()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, createdAt: Now));
        var service = CreateService(db);

        var result = await service.EvaluateAsync(ticket.Id, Now.AddMinutes(65));

        Assert.Empty(result);
    }

    [Fact]
    public async Task EvaluateAllOpenAsync_evaluates_every_non_closed_ticket_and_returns_the_total_created()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT");
        var ticketA = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now.AddMinutes(-65)));
        var ticketB = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now.AddMinutes(-65)));
        await SeedSlaAsync(db, ticketA);
        await SeedSlaAsync(db, ticketB);
        var service = CreateService(db);

        var created = await service.EvaluateAllOpenAsync(Now);

        Assert.Equal(4, created.Count); // 2 tickets x (Warning + Breach) for First Response each
        Assert.Equal(2, await db.TicketEscalations.CountAsync(e => e.TicketId == ticketA.Id));
        Assert.Equal(2, await db.TicketEscalations.CountAsync(e => e.TicketId == ticketB.Id));
    }

    [Fact]
    public async Task EvaluateAllOpenAsync_excludes_resolved_and_closed_tickets_from_the_sweep()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT");
        var closedTicket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, status: "closed", createdAt: Now.AddMinutes(-65)));
        await SeedSlaAsync(db, closedTicket);
        var service = CreateService(db);

        var created = await service.EvaluateAllOpenAsync(Now);

        Assert.Empty(created);
        Assert.Empty(await db.TicketEscalations.Where(e => e.TicketId == closedTicket.Id).ToListAsync());
    }

    [Fact]
    public async Task ListForTicketAsync_returns_the_tickets_history_oldest_first()
    {
        var (db, customer, category, priority, creator) = await SeedBaseAsync();
        var agent = await AddUserWithRoleAsync(db, "agent@local.test", "AGENT");
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: agent.Id, createdAt: Now));
        await SeedSlaAsync(db, ticket);
        var service = CreateService(db);
        await service.EvaluateAsync(ticket.Id, Now.AddMinutes(65));

        var history = await service.ListForTicketAsync(ticket.Id);

        Assert.Equal(2, history.Count);
        Assert.True(history[0].CreatedAtUtc <= history[1].CreatedAtUtc);
    }
}
