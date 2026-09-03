using System.Text;
using System.Threading.RateLimiting;
using CustomerSupportCrm.Api.AgentDesk.Tasks;
using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Api.Branches;
using CustomerSupportCrm.Api.Communications.Channels;
using CustomerSupportCrm.Api.Communications.Email;
using CustomerSupportCrm.Api.Communications.Inbound;
using CustomerSupportCrm.Api.Communications.WebForms;
using CustomerSupportCrm.Api.Customers;
using CustomerSupportCrm.Api.Customers.Attachments;
using CustomerSupportCrm.Api.Customers.Interactions;
using CustomerSupportCrm.Api.Customers.Notes;
using CustomerSupportCrm.Api.Departments;
using CustomerSupportCrm.Api.LiveChat;
using CustomerSupportCrm.Api.QuickReplies;
using CustomerSupportCrm.Api.Roles;
using CustomerSupportCrm.Api.KnowledgeBase;
using CustomerSupportCrm.Api.KnowledgeBase.Guides;
using CustomerSupportCrm.Api.KnowledgeBase.Search;
using CustomerSupportCrm.Api.KnowledgeBase.Solutions;
using CustomerSupportCrm.Api.Notifications;
using CustomerSupportCrm.Api.Sla;
using CustomerSupportCrm.Api.Sla.Escalations;
using CustomerSupportCrm.Api.SystemSettings;
using CustomerSupportCrm.Api.Tickets.Assignment;
using CustomerSupportCrm.Api.Tickets.Categories;
using CustomerSupportCrm.Api.Tickets.Collaboration;
using CustomerSupportCrm.Api.Tickets.History;
using CustomerSupportCrm.Api.Tickets.Priorities;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Api.Users;
using CustomerSupportCrm.Domain.Roles;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Options — validated at startup so a bad configuration stops the host before
// it serves a single request, rather than surfacing on the first login.
// ---------------------------------------------------------------------------
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey),
        "Jwt:SigningKey is required. Set it via environment variable Jwt__SigningKey, user-secrets, or a secrets manager.")
    // Measured in UTF-8 bytes, not characters: a 32-character non-ASCII key is longer than 32 bytes,
    // and length in chars is the wrong unit for a byte-oriented HMAC key.
    .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey ?? string.Empty) >= 32,
        "Jwt:SigningKey must be at least 32 bytes (256 bits) for HMAC-SHA256.")
    .Validate(o => o.AccessTokenMinutes is > 0 and <= 24 * 60,
        "Jwt:AccessTokenMinutes must be between 1 and 1440.")
    .ValidateOnStart();

// ---------------------------------------------------------------------------
// Persistence
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<CrmDbContext>(o => o.UseSqlServer(
    builder.Configuration.GetConnectionString("Default"),
    // Transient-fault resilience; costs nothing against LocalDB.
    // Caveat for later stories: a retrying execution strategy throws InvalidOperationException if it
    // meets a user-initiated transaction. Nothing here opens one — the single SaveChangesAsync on the
    // rehash path is covered by EF's own transaction. Code that needs an explicit transaction must
    // wrap it in db.Database.CreateExecutionStrategy().ExecuteAsync(...).
    sql => sql.EnableRetryOnFailure()));

// ---------------------------------------------------------------------------
// Auth services
// ---------------------------------------------------------------------------
builder.Services.AddOptions<PasswordHasherOptions>();   // PasswordHasher<T> requires IOptions<PasswordHasherOptions>
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<IUserPermissionsQuery, UserPermissionsQuery>();

// ---------------------------------------------------------------------------
// Roles & permissions (Story 03)
// ---------------------------------------------------------------------------
// A singleton: Permissions.All is a static, in-memory list with no per-request state.
builder.Services.AddSingleton<IPermissionCatalog>(new PermissionCatalog(Permissions.All));
builder.Services.AddScoped<IRolesService, RolesService>();
builder.Services.AddScoped<IUserRolesService, UserRolesService>();

// ---------------------------------------------------------------------------
// Departments & branches (Story 04)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IDepartmentsService, DepartmentsService>();
builder.Services.AddScoped<IBranchesService, BranchesService>();

// ---------------------------------------------------------------------------
// Audit logs (Story 05)
// ---------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// ---------------------------------------------------------------------------
// System configuration & branding (Story 06)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();

// ---------------------------------------------------------------------------
// Customer profiles & contact details (Story 07)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ICustomersService, CustomersService>();

// ---------------------------------------------------------------------------
// Customer interaction history (Story 08)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ICustomerInteractionsService, CustomerInteractionsService>();

// ---------------------------------------------------------------------------
// Customer notes & attachments (Story 09)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ICustomerNotesService, CustomerNotesService>();
builder.Services.AddScoped<ICustomerAttachmentsService, CustomerAttachmentsService>();

// ---------------------------------------------------------------------------
// Ticket categories & priorities (Story 10)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ITicketCategoriesService, TicketCategoriesService>();
builder.Services.AddScoped<ITicketPrioritiesService, TicketPrioritiesService>();
builder.Services.AddScoped<ITicketsService, TicketsService>();
builder.Services.AddScoped<ITicketAssignmentService, TicketAssignmentService>();

// ---------------------------------------------------------------------------
// Ticket history (Story 14)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ITicketHistoryService, TicketHistoryService>();

// ---------------------------------------------------------------------------
// Agent Desk: tasks & reminders (Story 16)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IAgentTasksService, AgentTasksService>();

// ---------------------------------------------------------------------------
// Agent Desk: quick replies (Story 17)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IQuickRepliesService, QuickRepliesService>();

// ---------------------------------------------------------------------------
// Ticket internal collaboration comments (Story 18)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ITicketCollaborationCommentsService, TicketCollaborationCommentsService>();

// ---------------------------------------------------------------------------
// Email & Web Forms channel (Story 19)
// ---------------------------------------------------------------------------
builder.Services.AddOptions<EmailOptions>().Bind(builder.Configuration.GetSection(EmailOptions.SectionName));
// TODO(email-provider): swap NullEmailSender for a real SmtpEmailSender/provider-API implementation
// once one is needed — everything else in this feature (ingestion, threading, the reply endpoint)
// depends only on IEmailSender, not on how a message is actually delivered.
builder.Services.AddSingleton<IEmailSender, NullEmailSender>();
builder.Services.AddScoped<IEmailIngestionService, EmailIngestionService>();
builder.Services.AddScoped<ITicketEmailReplyService, TicketEmailReplyService>();
builder.Services.AddScoped<IWebFormSubmissionService, WebFormSubmissionService>();

// 5 requests/minute per client IP — every anonymous, unauthenticated, internet-facing write in this
// application shares this one guard: the public Web Form, the live chat "start a session" endpoint
// (Story 21), and the Email/WhatsApp/SMS ingest endpoints (Stories 19/20 — correction: these were
// originally staff-authenticated, but every one of them represents a customer submitting something,
// never a staff member, so they moved to this same anonymous+rate-limited shape). Everything else sits
// behind the SetFallbackPolicy auth requirement below. A live chat session's follow-up messages/polling
// are not rate-limited here — both require a session token already obtained from this same limited
// call, so the abuse surface is naturally bounded per session.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public-channel", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

// ---------------------------------------------------------------------------
// WhatsApp & SMS channels (Story 20)
// ---------------------------------------------------------------------------
// TODO(channel-provider): swap NoOpChannelMessageSender for real Twilio/Meta-backed implementations
// once one is needed (Story 46) — everything else here (ingestion, the reply endpoint) depends only
// on IChannelMessageSender/IChannelMessageDispatcher, not on how a message is actually delivered.
builder.Services.AddSingleton<IChannelMessageSender>(sp =>
    new NoOpChannelMessageSender("WhatsApp", sp.GetRequiredService<ILogger<NoOpChannelMessageSender>>()));
builder.Services.AddSingleton<IChannelMessageSender>(sp =>
    new NoOpChannelMessageSender("Sms", sp.GetRequiredService<ILogger<NoOpChannelMessageSender>>()));
builder.Services.AddScoped<IChannelMessageDispatcher, ChannelMessageDispatcher>();
builder.Services.AddScoped<IInboundMessageService, InboundMessageService>();
builder.Services.AddScoped<ITicketChannelReplyService, TicketChannelReplyService>();

// ---------------------------------------------------------------------------
// Live chat channel (Story 21)
// ---------------------------------------------------------------------------
// No provider abstraction here (unlike Email/WhatsApp/SMS): the CRM itself is the transport, so there
// is nothing external to swap out later. Conversation status is derived from the linked Ticket (see
// LiveChatStatus), not stored — no separate assignment/close/reopen wiring needed.
builder.Services.AddScoped<ILiveChatService, LiveChatService>();

// ---------------------------------------------------------------------------
// SLA response/resolution tracking (Story 22)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ISlaService, SlaService>();
builder.Services.AddScoped<ISlaPoliciesService, SlaPoliciesService>();

// ---------------------------------------------------------------------------
// SLA escalation rules (Story 24)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ISlaEscalationService, SlaEscalationService>();
builder.Services.Configure<SlaEscalationOptions>(builder.Configuration.GetSection("Sla:Escalation"));
builder.Services.AddHostedService<SlaEscalationBackgroundService>();

// ---------------------------------------------------------------------------
// Notifications (Story 25)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<INotificationService, NotificationService>();

// ---------------------------------------------------------------------------
// Knowledge base — FAQs & Help Articles (Story 26)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IKnowledgeBaseArticlesService, KnowledgeBaseArticlesService>();
builder.Services.AddScoped<IKnowledgeBaseCategoriesService, KnowledgeBaseCategoriesService>();

// ---------------------------------------------------------------------------
// Knowledge base — Solutions & Guides (Story 27)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IKbSolutionsService, KbSolutionsService>();
builder.Services.AddScoped<IKbGuidesService, KbGuidesService>();

// ---------------------------------------------------------------------------
// Knowledge base — cross-content-type search (Story 28)
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IKnowledgeBaseSearchService, KnowledgeBaseSearchService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

// Configured through the options pipeline, taking a dependency on IOptions<JwtOptions>, rather than
// reading builder.Configuration eagerly here. Reading eagerly would snapshot the configuration as it
// stood at registration time, so any source added later — the in-memory collection an integration
// test supplies, a reloaded file, a late-bound secrets provider — would be silently ignored and the
// handler would validate against a stale (or empty) signing key.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtAccessor) =>
    {
        var jwt = jwtAccessor.Value;

        // Without this the handler rewrites sub -> ClaimTypes.NameIdentifier and
        // email -> ClaimTypes.Email, and User.FindFirst("sub") silently returns null.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),   // tolerates modest server/client clock drift
            NameClaimType = "name",
            RoleClaimType = "role",                 // reserved for Story 03; no role claim is issued yet
        };
    });

// SetFallbackPolicy, not SetDefaultPolicy: the fallback applies to endpoints with *no* authorization
// metadata, so a controller added by a later story is protected unless it opts out with
// [AllowAnonymous]. The default policy would only cover endpoints already marked [Authorize].
// "Admin" (Story 02) is kept, unused by any controller as of Story 03 but harmless, in case a later
// story still wants a single "is an administrator" check without going through the permission
// catalogue. RequireRole checks the "role" claim, whose type was already declared via RoleClaimType
// above; an authenticated caller without it is Forbidden (403), an unauthenticated one is
// Unauthorized (401) — both via the framework's default AuthorizationMiddlewareResultHandler.
//
// "perm:*" policies (Story 03) are resolved on demand by PermissionPolicyProvider, registered below,
// rather than pre-declared here — there is one permission code per [HasPermission(...)] call site,
// and hard-coding an AddPolicy call per code would defeat the point of a data-driven catalogue.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("Authenticated", p => p.RequireAuthenticatedUser())
    .AddPolicy("Admin", p => p.RequireRole("Admin"));

// IAuthorizationPolicyProvider/IAuthorizationHandler are framework extension points that must be
// singletons: PermissionPolicyProvider wraps DefaultAuthorizationPolicyProvider for every policy name
// it doesn't recognise, and PermissionAuthorizationHandler holds no per-request state.
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ---------------------------------------------------------------------------
// MVC + the error contract this story's tests assert
// ---------------------------------------------------------------------------
builder.Services.AddControllers();

// Dev-only API explorer — never exposed outside Development (see app.UseSwagger below). Adds a
// "Bearer" auth scheme to the UI so a token from POST /api/auth/login can be pasted into the
// Authorize button and carried on every subsequent try-it-out call.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste only the raw token — Swagger UI adds the \"Bearer \" prefix itself.",
    });
    o.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

// [ApiController]'s automatic 400 returns ValidationProblemDetails, which does not match the
// { "error": "invalid_request" } contract.
builder.Services.Configure<ApiBehaviorOptions>(o =>
    o.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(new { error = "invalid_request" }));

// ---------------------------------------------------------------------------
// CORS
// ---------------------------------------------------------------------------
// Origins come from configuration; appsettings.json ships an empty array so a misconfigured
// deployment allows no cross-origin caller rather than the wrong one. No hard-coded production
// fallback.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

// AllowCredentials() is deliberately absent: this story authenticates with an Authorization: Bearer
// header, never a cookie or client certificate, so credentialed CORS buys nothing and only widens
// what the browser will expose cross-origin. Add it only if a later story introduces cookie-based
// auth — and note that doing so forbids wildcard origins.
//
// In local development the Vite proxy makes /api same-origin, so CORS is not exercised by the normal
// dev loop; it matters for a non-proxied client or a frontend deployed on a different host.
builder.Services.AddCors(o => o.AddPolicy("Frontend", p => p
    .WithOrigins(corsOrigins)
    .WithHeaders("Authorization", "Content-Type")
    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")));

var app = builder.Build();

// Development only: apply migrations and seed the local admin.
// MigrateAsync, never EnsureCreated — EnsureCreated bypasses the migrations history table and would
// leave the dev database unable to accept Story 02's migration.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    // ValidateOnStart() runs when the host starts — which is app.Run(), i.e. *after* this block.
    // Touching the options here forces the same validators to run first, so a misconfigured app
    // refuses to start without having already migrated the database.
    _ = services.GetRequiredService<IOptions<JwtOptions>>().Value;

    var db = services.GetRequiredService<CrmDbContext>();
    await db.Database.MigrateAsync();

    await DbSeeder.SeedAsync(
        db,
        services.GetRequiredService<IPasswordHasher<User>>(),
        services.GetRequiredService<IConfiguration>(),
        services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbSeeder)));
}

// Dev-only, exactly like the migrate-and-seed block above: never registered outside Development.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();   // http://localhost:5080/swagger
}

// Serves uploaded branding logos from wwwroot/uploads/logos (Story 06 logo-upload extension) as
// plain static files — no auth needed to view a logo image, same as any other public asset.
// Placed before UseCors/UseAuthentication: a static file response carries no auth/CORS decision.
//
// Uses an explicit PhysicalFileProvider rather than the parameterless UseStaticFiles() overload
// (which reads IWebHostEnvironment.WebRootFileProvider): that provider is resolved once, when the
// host environment initializes, and on a project with no wwwroot checked into source control it
// gets fixed as a NullFileProvider *before* Program.cs ever runs — so a wwwroot the app creates for
// itself at runtime (SystemSettingsController.UploadLogo, right below) would silently 401 through
// the auth fallback policy forever, no matter how many files land on disk afterward.
var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(wwwrootPath);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(wwwrootPath) });

// UseCors must precede UseAuthentication so OPTIONS preflights, which carry no Authorization
// header, are answered before authentication runs.
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// With top-level statements, WebApplicationFactory<Program> cannot see Program unless it is public.
public partial class Program { }
