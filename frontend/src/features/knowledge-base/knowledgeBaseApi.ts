import { http } from "../../lib/http";
import type {
  CreateKbGuidePayload,
  CreateKbSolutionPayload,
  CreateKnowledgeBaseArticlePayload,
  CreateKnowledgeBaseCategoryPayload,
  KbGuide,
  KbSolution,
  KnowledgeBaseArticle,
  KnowledgeBaseAudience,
  KnowledgeBaseCategory,
  KnowledgeBaseContentType,
  KnowledgeBasePublicationStatus,
  UpdateKbGuidePayload,
  UpdateKbSolutionPayload,
  UpdateKnowledgeBaseArticlePayload,
  UpdateKnowledgeBaseCategoryPayload,
} from "./types";

export interface ListArticlesParams {
  contentType?: KnowledgeBaseContentType;
  categoryId?: string;
  audience?: KnowledgeBaseAudience;
  status?: KnowledgeBasePublicationStatus;
}

export async function listArticles(params: ListArticlesParams = {}): Promise<KnowledgeBaseArticle[]> {
  const response = await http.get<KnowledgeBaseArticle[]>("/knowledge-base/articles", { params });
  return response.data;
}

export async function getArticle(id: string): Promise<KnowledgeBaseArticle> {
  const response = await http.get<KnowledgeBaseArticle>(`/knowledge-base/articles/${id}`);
  return response.data;
}

export async function createArticle(payload: CreateKnowledgeBaseArticlePayload): Promise<KnowledgeBaseArticle> {
  const response = await http.post<KnowledgeBaseArticle>("/knowledge-base/articles", payload);
  return response.data;
}

export async function updateArticle(id: string, payload: UpdateKnowledgeBaseArticlePayload): Promise<KnowledgeBaseArticle> {
  const response = await http.put<KnowledgeBaseArticle>(`/knowledge-base/articles/${id}`, payload);
  return response.data;
}

export async function publishArticle(id: string): Promise<KnowledgeBaseArticle> {
  const response = await http.post<KnowledgeBaseArticle>(`/knowledge-base/articles/${id}/publish`);
  return response.data;
}

export async function unpublishArticle(id: string): Promise<KnowledgeBaseArticle> {
  const response = await http.post<KnowledgeBaseArticle>(`/knowledge-base/articles/${id}/unpublish`);
  return response.data;
}

export async function deleteArticle(id: string): Promise<void> {
  await http.delete(`/knowledge-base/articles/${id}`);
}

export interface ListSolutionsParams {
  categoryId?: string;
  audience?: KnowledgeBaseAudience;
  status?: KnowledgeBasePublicationStatus;
}

export async function listSolutions(params: ListSolutionsParams = {}): Promise<KbSolution[]> {
  const response = await http.get<KbSolution[]>("/knowledge-base/solutions", { params });
  return response.data;
}

export async function getSolution(id: string): Promise<KbSolution> {
  const response = await http.get<KbSolution>(`/knowledge-base/solutions/${id}`);
  return response.data;
}

export async function createSolution(payload: CreateKbSolutionPayload): Promise<KbSolution> {
  const response = await http.post<KbSolution>("/knowledge-base/solutions", payload);
  return response.data;
}

export async function updateSolution(id: string, payload: UpdateKbSolutionPayload): Promise<KbSolution> {
  const response = await http.put<KbSolution>(`/knowledge-base/solutions/${id}`, payload);
  return response.data;
}

export async function publishSolution(id: string): Promise<KbSolution> {
  const response = await http.post<KbSolution>(`/knowledge-base/solutions/${id}/publish`);
  return response.data;
}

export async function unpublishSolution(id: string): Promise<KbSolution> {
  const response = await http.post<KbSolution>(`/knowledge-base/solutions/${id}/unpublish`);
  return response.data;
}

export async function deleteSolution(id: string): Promise<void> {
  await http.delete(`/knowledge-base/solutions/${id}`);
}

export interface ListGuidesParams {
  categoryId?: string;
  audience?: KnowledgeBaseAudience;
  status?: KnowledgeBasePublicationStatus;
}

export async function listGuides(params: ListGuidesParams = {}): Promise<KbGuide[]> {
  const response = await http.get<KbGuide[]>("/knowledge-base/guides", { params });
  return response.data;
}

export async function getGuide(id: string): Promise<KbGuide> {
  const response = await http.get<KbGuide>(`/knowledge-base/guides/${id}`);
  return response.data;
}

export async function createGuide(payload: CreateKbGuidePayload): Promise<KbGuide> {
  const response = await http.post<KbGuide>("/knowledge-base/guides", payload);
  return response.data;
}

export async function updateGuide(id: string, payload: UpdateKbGuidePayload): Promise<KbGuide> {
  const response = await http.put<KbGuide>(`/knowledge-base/guides/${id}`, payload);
  return response.data;
}

export async function publishGuide(id: string): Promise<KbGuide> {
  const response = await http.post<KbGuide>(`/knowledge-base/guides/${id}/publish`);
  return response.data;
}

export async function unpublishGuide(id: string): Promise<KbGuide> {
  const response = await http.post<KbGuide>(`/knowledge-base/guides/${id}/unpublish`);
  return response.data;
}

export async function deleteGuide(id: string): Promise<void> {
  await http.delete(`/knowledge-base/guides/${id}`);
}

export interface ListCategoriesParams {
  includeInactive?: boolean;
}

export async function listCategories(params: ListCategoriesParams = {}): Promise<KnowledgeBaseCategory[]> {
  const response = await http.get<KnowledgeBaseCategory[]>("/knowledge-base/categories", { params });
  return response.data;
}

export async function createCategory(payload: CreateKnowledgeBaseCategoryPayload): Promise<KnowledgeBaseCategory> {
  const response = await http.post<KnowledgeBaseCategory>("/knowledge-base/categories", payload);
  return response.data;
}

export async function updateCategory(id: string, payload: UpdateKnowledgeBaseCategoryPayload): Promise<KnowledgeBaseCategory> {
  const response = await http.put<KnowledgeBaseCategory>(`/knowledge-base/categories/${id}`, payload);
  return response.data;
}

export async function deleteCategory(id: string): Promise<void> {
  await http.delete(`/knowledge-base/categories/${id}`);
}
