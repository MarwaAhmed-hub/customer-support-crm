using System.Text.Json.Serialization;
using CustomerSupportCrm.Domain.Tickets;

namespace CustomerSupportCrm.Api.Sla.Escalations;

/// <summary>
/// The enums serialize as their names (not raw ints) so this reads like the rest of the API's
/// string-based status fields (e.g. <c>Ticket.Status</c>, <c>TicketSla.FirstResponseStatus</c>) even
/// though the column itself is stored as an int (see <c>CrmDbContext</c>'s <c>HasConversion&lt;int&gt;()</c>).
/// </summary>
public sealed record TicketEscalationDto(
    Guid Id,
    Guid TicketId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] SlaType SlaType,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] EscalationMilestone Milestone,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] EscalationTargetRole TargetRole,
    Guid? TargetUserId,
    DateTime ThresholdAtUtc,
    DateTime CreatedAtUtc,
    bool WasUnassigned,
    string? Notes);
