/** Mirrors the backend DTOs in `Api/KnowledgeBase/Search/KnowledgeBaseSearchDtos.cs` (camelCase properties; the `type` enum serializes as its PascalCase name via `JsonStringEnumConverter`). */

export type KnowledgeBaseSearchContentType = "Faq" | "Article" | "Solution" | "Guide";

export interface KnowledgeBaseSearchResultItem {
  id: string;
  type: KnowledgeBaseSearchContentType;
  title: string;
  categoryId: string | null;
  categoryName: string | null;
  excerpt: string;
  publishedAtUtc: string | null;
}

export interface KnowledgeBaseSearchResponse {
  page: number;
  pageSize: number;
  total: number;
  items: KnowledgeBaseSearchResultItem[];
}
