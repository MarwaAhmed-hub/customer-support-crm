namespace CustomerSupportCrm.Domain.KnowledgeBase;

/// <summary>Story 27: one ordered step of a <see cref="KbGuide"/>. The whole collection is replaced on every update (delete + re-insert with <see cref="Order"/> set from array index) — never diffed/patched in place.</summary>
public class KbGuideStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GuideId { get; set; }

    public KbGuide? Guide { get; set; }

    public int Order { get; set; }

    public string Instruction { get; set; } = string.Empty;
}
