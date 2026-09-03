using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CustomerSupportCrm.Domain.KnowledgeBase;

namespace CustomerSupportCrm.Api.KnowledgeBase;

/// <summary>Enums serialize as their names ("Faq"/"HelpArticle", "CustomerFacing"/"Internal", "Draft"/"Published"), matching this API's other enum-backed DTOs (e.g. <c>TicketEscalationDto</c>).</summary>
public sealed record KnowledgeBaseArticleDto(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseContentType ContentType,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseAudience Audience,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBasePublicationStatus Status,
    string Title,
    string Body,
    Guid CategoryId,
    string CategoryName,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? PublishedAtUtc);

// Attributes target the primary-constructor parameters directly, not the generated properties —
// matching Departments/DepartmentDtos.cs. Title's non-empty-after-trim check and Body's non-empty
// check happen in the service (KnowledgeBaseArticlesService), same as TicketsService's
// Subject/Description — StringLength alone lets a whitespace-only string through.
public sealed record CreateKnowledgeBaseArticleRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseContentType ContentType,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseAudience Audience,
    [Required, StringLength(400, MinimumLength = 1)] string Title,
    [Required, MinLength(1)] string Body,
    [Required] Guid CategoryId);

/// <summary>ContentType is deliberately absent — it never changes after creation; the service ignores any attempt to change it even if a caller somehow sends one.</summary>
public sealed record UpdateKnowledgeBaseArticleRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseAudience Audience,
    [Required, StringLength(400, MinimumLength = 1)] string Title,
    [Required, MinLength(1)] string Body,
    [Required] Guid CategoryId);

public sealed record KnowledgeBaseCategoryDto(Guid Id, string Name, bool IsActive);

public sealed record CreateKnowledgeBaseCategoryRequest([Required, StringLength(200, MinimumLength = 1)] string Name);

public sealed record UpdateKnowledgeBaseCategoryRequest([Required, StringLength(200, MinimumLength = 1)] string Name, bool IsActive);
