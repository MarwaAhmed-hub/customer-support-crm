namespace CustomerSupportCrm.Api.Sla.Escalations;

/// <summary>Bound from configuration section <c>"Sla:Escalation"</c>. Absent config keeps the default.</summary>
public sealed class SlaEscalationOptions
{
    public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromSeconds(60);
}
