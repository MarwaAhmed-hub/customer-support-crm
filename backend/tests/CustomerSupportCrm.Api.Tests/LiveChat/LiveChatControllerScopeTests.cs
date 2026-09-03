using System.Security.Claims;
using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Customers;
using CustomerSupportCrm.Api.LiveChat;
using CustomerSupportCrm.Api.Sla;
using CustomerSupportCrm.Api.Tickets.Assignment;
using CustomerSupportCrm.Api.Tickets.History;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Domain.Roles;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.LiveChat;

/// <summary>
/// The permission-to-scope wiring lives in <see cref="LiveChatController.ResolveScope"/>, not in
/// <see cref="LiveChatService"/> (covered separately by <c>LiveChatServiceTests</c>) — a caller
/// holding <c>tickets.assign</c> (Manager/Admin) sees every conversation, everyone else with only
/// <c>livechat.view</c> (a plain Agent) only sees their own. These tests exercise that wiring end to
/// end against a real <see cref="LiveChatService"/> and an EF InMemory context, the same way
/// <c>UsersControllerDepartmentBranchTests</c> constructs <c>UsersController</c> directly.
/// </summary>
public class LiveChatControllerScopeTests
{
    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task RecordAsync(string action, string summary, string? entityType = null, string? entityId = null, object? metadata = null, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<AuditLogPageDto> QueryAsync(AuditLogQuery query, CancellationToken ct = default) =>
            Task.FromResult(new AuditLogPageDto([], query.Page, query.PageSize, 0));
    }

    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static LiveChatController CreateController(CrmDbContext db, Guid callerUserId, params string[] permissions)
    {
        var customersService = new CustomersService(db);
        var ticketsService = new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance));
        var liveChatService = new CustomerSupportCrm.Api.LiveChat.LiveChatService(db, customersService, ticketsService, new SlaService(db, NullLogger<SlaService>.Instance));
        var controller = new LiveChatController(liveChatService, new NoOpAuditLogService());

        var claims = new List<Claim> { new("sub", callerUserId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test")) },
        };
        return controller;
    }

    private static async Task SeedDefaultsAsync(CrmDbContext db)
    {
        db.TicketCategories.Add(new TicketCategory { Name = "General Inquiry", NormalizedName = "GENERAL INQUIRY" });
        db.TicketPriorities.Add(new TicketPriority { Name = "Medium", NormalizedName = "MEDIUM", SortOrder = 20 });
        db.Users.Add(new User { Email = DbSeeder.SystemUserEmail, DisplayName = "System (Automated)", PasswordHash = "x", IsActive = false });
        await db.SaveChangesAsync();
    }

    /// <summary>Starts a session directly through the service — these tests only exercise <see cref="LiveChatController.List"/>/<c>Get</c>'s scoping, not the public start endpoint.</summary>
    private static async Task<StartLiveChatSessionResponse> StartSessionAsync(CrmDbContext db, string name, string email)
    {
        var customersService = new CustomersService(db);
        var ticketsService = new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance));
        var liveChatService = new CustomerSupportCrm.Api.LiveChat.LiveChatService(db, customersService, ticketsService, new SlaService(db, NullLogger<SlaService>.Instance));
        return await liveChatService.StartAsync(new StartLiveChatSessionRequest(name, email, null, "Hi, I need help"));
    }

    [Fact]
    public async Task List_for_a_caller_without_tickets_assign_only_returns_conversations_assigned_to_them()
    {
        await using var db = CreateDb();
        await SeedDefaultsAsync(db);
        var agent = new User { Email = "agent@example.com", DisplayName = "Agent Smith", PasswordHash = "x", IsActive = true };
        var otherAgent = new User { Email = "other@example.com", DisplayName = "Other Agent", PasswordHash = "x", IsActive = true };
        db.Users.AddRange(agent, otherAgent);
        await db.SaveChangesAsync();

        var mine = await StartSessionAsync(db, "Ali", "ali@example.com");
        var someoneElses = await StartSessionAsync(db, "Sara", "sara@example.com");
        (await db.Tickets.SingleAsync(t => t.Id == mine.TicketId)).AssignedUserId = agent.Id;
        (await db.Tickets.SingleAsync(t => t.Id == someoneElses.TicketId)).AssignedUserId = otherAgent.Id;
        await db.SaveChangesAsync();

        var scopedController = CreateController(db, agent.Id, Permissions.LiveChat.View);
        var result = await scopedController.List(status: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<LiveChatSessionListItemDto>>(ok.Value);
        Assert.Single(items);
        Assert.Equal(mine.TicketId, items[0].TicketId);
    }

    [Fact]
    public async Task List_for_a_caller_with_tickets_assign_returns_every_conversation()
    {
        await using var db = CreateDb();
        await SeedDefaultsAsync(db);
        var agent = new User { Email = "agent@example.com", DisplayName = "Agent Smith", PasswordHash = "x", IsActive = true };
        db.Users.Add(agent);
        await db.SaveChangesAsync();

        var mine = await StartSessionAsync(db, "Ali", "ali@example.com");
        await StartSessionAsync(db, "Sara", "sara@example.com");
        (await db.Tickets.SingleAsync(t => t.Id == mine.TicketId)).AssignedUserId = agent.Id;
        await db.SaveChangesAsync();

        var managerController = CreateController(db, Guid.NewGuid(), Permissions.LiveChat.View, Permissions.Tickets.Assign);
        var result = await managerController.List(status: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<LiveChatSessionListItemDto>>(ok.Value);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Get_for_a_caller_without_tickets_assign_returns_404_for_a_conversation_assigned_to_someone_else()
    {
        await using var db = CreateDb();
        await SeedDefaultsAsync(db);
        var agent = new User { Email = "agent@example.com", DisplayName = "Agent Smith", PasswordHash = "x", IsActive = true };
        var otherAgent = new User { Email = "other@example.com", DisplayName = "Other Agent", PasswordHash = "x", IsActive = true };
        db.Users.AddRange(agent, otherAgent);
        await db.SaveChangesAsync();

        var started = await StartSessionAsync(db, "Ali", "ali@example.com");
        (await db.Tickets.SingleAsync(t => t.Id == started.TicketId)).AssignedUserId = otherAgent.Id;
        await db.SaveChangesAsync();

        var scopedController = CreateController(db, agent.Id, Permissions.LiveChat.View);
        var result = await scopedController.Get(started.SessionId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Get_for_a_caller_with_tickets_assign_returns_a_conversation_assigned_to_someone_else()
    {
        await using var db = CreateDb();
        await SeedDefaultsAsync(db);
        var otherAgent = new User { Email = "other@example.com", DisplayName = "Other Agent", PasswordHash = "x", IsActive = true };
        db.Users.Add(otherAgent);
        await db.SaveChangesAsync();

        var started = await StartSessionAsync(db, "Ali", "ali@example.com");
        (await db.Tickets.SingleAsync(t => t.Id == started.TicketId)).AssignedUserId = otherAgent.Id;
        await db.SaveChangesAsync();

        var managerController = CreateController(db, Guid.NewGuid(), Permissions.LiveChat.View, Permissions.Tickets.Assign);
        var result = await managerController.Get(started.SessionId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LiveChatSessionDetailDto>(ok.Value);
        Assert.Equal(started.SessionId, dto.SessionId);
    }

    [Fact]
    public async Task List_returns_401_when_the_caller_has_no_resolvable_user_id_and_lacks_tickets_assign()
    {
        await using var db = CreateDb();
        await SeedDefaultsAsync(db);
        var customersService = new CustomersService(db);
        var ticketsService = new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance));
        var liveChatService = new CustomerSupportCrm.Api.LiveChat.LiveChatService(db, customersService, ticketsService, new SlaService(db, NullLogger<SlaService>.Instance));
        var controller = new LiveChatController(liveChatService, new NoOpAuditLogService())
        {
            ControllerContext = new ControllerContext
            {
                // Only a permission claim, no "sub" — mirrors a malformed/edge-case token rather than
                // anything issued by this app's own JwtTokenService in practice.
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", Permissions.LiveChat.View)], authenticationType: "test")) },
            },
        };

        var result = await controller.List(status: null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }
}
