namespace CustomerSupportCrm.Domain.Tickets;

/// <summary>
/// Story 13: the full ticket status lifecycle, plus the directed graph of allowed transitions
/// between them. Story 11 only ever set <see cref="Open"/>; every other value and the transition
/// rules below are this story's addition.
/// </summary>
public static class TicketStatuses
{
    public const string Open = "open";
    public const string InProgress = "in_progress";
    public const string Pending = "pending";
    public const string Resolved = "resolved";
    public const string Closed = "closed";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Open, InProgress, Pending, Resolved, Closed,
    };

    /// <summary>
    /// The agreed lifecycle graph: Open -> InProgress -> Pending -> InProgress -> Resolved -> Closed,
    /// plus explicit reopen paths (Resolved/Closed -> InProgress) and Open -> Closed as a cancel path.
    /// A status transitioning to itself is deliberately not in any of these sets — a same-status
    /// "transition" is rejected the same as any other unlisted edge.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> Transitions =
        new Dictionary<string, IReadOnlyCollection<string>>
        {
            [Open] = new[] { InProgress, Closed },
            [InProgress] = new[] { Pending, Resolved },
            [Pending] = new[] { InProgress },
            [Resolved] = new[] { Closed, InProgress },
            [Closed] = new[] { InProgress },
        };

    public static bool IsKnown(string status) => All.Contains(status);

    public static bool CanTransition(string from, string to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>Story 24: a ticket in either of these statuses is "done" — its Resolution SLA is satisfied and it generates no further escalations of any kind.</summary>
    public static bool IsResolvedOrClosed(string status) => status is Resolved or Closed;
}
