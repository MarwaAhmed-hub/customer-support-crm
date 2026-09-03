import { http } from "../../../lib/http";
import type { KnowledgeBaseSearchContentType, KnowledgeBaseSearchResponse } from "./types";

export interface SearchKnowledgeBaseParams {
  q?: string;
  type?: KnowledgeBaseSearchContentType[];
  categoryId?: string;
  page?: number;
  pageSize?: number;
}

export async function searchKnowledgeBase(params: SearchKnowledgeBaseParams = {}): Promise<KnowledgeBaseSearchResponse> {
  const response = await http.get<KnowledgeBaseSearchResponse>("/knowledge-base/search", {
    params,
    // axios's default array serialization ("type[]=Faq") doesn't match ASP.NET Core's query-string
    // binding for an array parameter, which expects the key repeated: "type=Faq&type=Solution".
    paramsSerializer: { indexes: null },
  });
  return response.data;
}
