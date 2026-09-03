using CustomerSupportCrm.Api.Notifications;
using Microsoft.Extensions.Options;

namespace CustomerSupportCrm.Api.Sla.Escalations;

/// <summary>
/// Story 24: the first hosted service in this codebase — periodically sweeps every open ticket for
/// newly-crossed SLA warning/breach milestones via <see cref="ISlaEscalationService.EvaluateAllOpenAsync"/>.
/// A scope is created per cycle since <c>ISlaEscalationService</c> (and the <c>CrmDbContext</c> it
/// depends on) is scoped, while this service itself is a singleton for the app's lifetime — the
/// standard pattern for a <see cref="BackgroundService"/> consuming scoped dependencies.
/// </summary>
public sealed class SlaEscalationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SlaEscalationOptions> options,
    ILogger<SlaEscalationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.EvaluationInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var escalationService = scope.ServiceProvider.GetRequiredService<ISlaEscalationService>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                logger.LogInformation("SLA escalation sweep starting.");
                var created = await escalationService.EvaluateAllOpenAsync(now: null, stoppingToken);

                // Story 25: one notification per newly-created escalation. NotifySlaMilestoneAsync
                // never throws on its own (see its remarks), so this loop needs no per-item try/catch.
                foreach (var escalation in created)
                {
                    await notificationService.NotifySlaMilestoneAsync(escalation, stoppingToken);
                }

                logger.LogInformation("SLA escalation sweep finished: {Created} new escalation(s) created.", created.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host is shutting down — not a sweep failure, exit the loop quietly.
                break;
            }
            catch (Exception ex)
            {
                // A cycle failing outright (e.g. the DB is briefly unreachable) must never crash the
                // host — log and try again next interval. Per-ticket failures within a cycle are
                // already isolated inside EvaluateAllOpenAsync itself; this is the outer safety net.
                logger.LogError(ex, "SLA escalation sweep failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
