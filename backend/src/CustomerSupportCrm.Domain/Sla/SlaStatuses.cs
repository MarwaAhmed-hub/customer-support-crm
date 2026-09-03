namespace CustomerSupportCrm.Domain.Sla;

/// <summary>
/// Story 22: the two states a ticket's First Response and Resolution clocks each move through
/// independently. <see cref="Running"/> is the only non-terminal value — once either <see cref="Met"/>
/// or <see cref="Breached"/> is recorded it stays that way for this story (Stories 24/25 may add
/// escalation/notification behavior on top, but nothing here ever moves a clock back to Running).
/// </summary>
public static class SlaStatuses
{
    public const string Running = "running";
    public const string Met = "met";
    public const string Breached = "breached";
}
