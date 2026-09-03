/** Mirrors the backend DTOs in `Api/KnowledgeBase/KnowledgeBaseDtos.cs` (camelCase properties — System.Text.Json's default; enums serialize as their PascalCase name via `JsonStringEnumConverter`). */

export type KnowledgeBaseContentType = "Faq" | "HelpArticle";
export type KnowledgeBaseAudience = "CustomerFacing" | "Internal";
export type KnowledgeBasePublicationStatus = "Draft" | "Published";

export interface KnowledgeBaseArticle {
  id: string;
  contentType: KnowledgeBaseContentType;
  audience: KnowledgeBaseAudience;
  status: KnowledgeBasePublicationStatus;
  title: string;
  body: string;
  categoryId: string;
  categoryName: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  publishedAtUtc: string | null;
}

export interface CreateKnowledgeBaseArticlePayload {
  contentType: KnowledgeBaseContentType;
  audience: KnowledgeBaseAudience;
  title: string;
  body: string;
  categoryId: string;
}

export interface UpdateKnowledgeBaseArticlePayload {
  audience: KnowledgeBaseAudience;
  title: string;
  body: string;
  categoryId: string;
}

export interface KbSolution {
  id: string;
  title: string;
  problem: string;
  solutionBody: string;
  categoryId: string;
  categoryName: string;
  audience: KnowledgeBaseAudience;
  status: KnowledgeBasePublicationStatus;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  publishedAtUtc: string | null;
}

export interface CreateKbSolutionPayload {
  title: string;
  problem: string;
  solutionBody: string;
  categoryId: string;
  audience: KnowledgeBaseAudience;
}

export interface UpdateKbSolutionPayload {
  title: string;
  problem: string;
  solutionBody: string;
  categoryId: string;
  audience: KnowledgeBaseAudience;
}

export interface KbGuideStep {
  order: number;
  instruction: string;
}

export interface KbGuide {
  id: string;
  title: string;
  description: string;
  categoryId: string;
  categoryName: string;
  audience: KnowledgeBaseAudience;
  status: KnowledgeBasePublicationStatus;
  steps: KbGuideStep[];
  createdAtUtc: string;
  updatedAtUtc: string | null;
  publishedAtUtc: string | null;
}

export interface CreateKbGuidePayload {
  title: string;
  description: string;
  categoryId: string;
  audience: KnowledgeBaseAudience;
  steps: { instruction: string }[];
}

export interface UpdateKbGuidePayload {
  title: string;
  description: string;
  categoryId: string;
  audience: KnowledgeBaseAudience;
  steps: { instruction: string }[];
}

export interface KnowledgeBaseCategory {
  id: string;
  name: string;
  isActive: boolean;
}

export interface CreateKnowledgeBaseCategoryPayload {
  name: string;
}

export interface UpdateKnowledgeBaseCategoryPayload {
  name: string;
  isActive: boolean;
}
