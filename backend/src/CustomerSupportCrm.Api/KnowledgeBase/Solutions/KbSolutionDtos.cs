using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CustomerSupportCrm.Domain.KnowledgeBase;

namespace CustomerSupportCrm.Api.KnowledgeBase.Solutions;

public sealed record KbSolutionDto(
    Guid Id,
    string Title,
    string Problem,
    string SolutionBody,
    Guid CategoryId,
    string CategoryName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseAudience Audience,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBasePublicationStatus Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? PublishedAtUtc);

public sealed record CreateKbSolutionRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [Required, MinLength(1)] string Problem,
    [Required, MinLength(1)] string SolutionBody,
    [Required] Guid CategoryId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseAudience Audience);

/// <summary>No <c>Status</c> field — a Solution always starts Draft; see <c>KbSolutionsService.CreateAsync</c>.</summary>
public sealed record UpdateKbSolutionRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [Required, MinLength(1)] string Problem,
    [Required, MinLength(1)] string SolutionBody,
    [Required] Guid CategoryId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseAudience Audience);
