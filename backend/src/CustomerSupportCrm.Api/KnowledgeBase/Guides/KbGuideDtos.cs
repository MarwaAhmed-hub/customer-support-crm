using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CustomerSupportCrm.Domain.KnowledgeBase;

namespace CustomerSupportCrm.Api.KnowledgeBase.Guides;

public sealed record KbGuideStepDto(int Order, string Instruction);

public sealed record KbGuideDto(
    Guid Id,
    string Title,
    string Description,
    Guid CategoryId,
    string CategoryName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseAudience Audience,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBasePublicationStatus Status,
    IReadOnlyList<KbGuideStepDto> Steps,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? PublishedAtUtc);

/// <summary>Server assigns <see cref="KbGuideStep.Order"/> from this list's array index — the client never sends an order number directly.</summary>
public sealed record KbGuideStepInput([Required, MinLength(1)] string Instruction);

public sealed record CreateKbGuideRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [Required, MinLength(1)] string Description,
    [Required] Guid CategoryId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseAudience Audience,
    [Required, MinLength(1)] IReadOnlyList<KbGuideStepInput> Steps);

/// <summary>No <c>Status</c> field — a Guide always starts Draft; see <c>KbGuidesService.CreateAsync</c>.</summary>
public sealed record UpdateKbGuideRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [Required, MinLength(1)] string Description,
    [Required] Guid CategoryId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseAudience Audience,
    [Required, MinLength(1)] IReadOnlyList<KbGuideStepInput> Steps);
