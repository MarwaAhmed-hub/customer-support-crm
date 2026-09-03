using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Communications.WebForms;

/// <summary><see cref="Website"/> is a honeypot — a hidden field a real browser never fills, present only so a naive bot filling every field trips it. It carries no <c>[Required]</c>/format attribute on purpose: rejecting a malformed honeypot value would leak that it exists.</summary>
public sealed record WebFormSubmissionRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [Required, StringLength(256, MinimumLength = 1)] string Email,
    [Required, StringLength(200, MinimumLength = 1)] string Subject,
    [Required, StringLength(4000, MinimumLength = 1)] string Description,
    [StringLength(64)] string? Phone = null,
    string? Website = null);

public sealed record WebFormSubmissionResponse(Guid TicketId, Guid CustomerId);
