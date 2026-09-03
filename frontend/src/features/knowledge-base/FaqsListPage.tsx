import { ArticleListView } from "./ArticleListView";

export function FaqsListPage() {
  return (
    <ArticleListView
      contentType="Faq"
      title="FAQs"
      description="Frequently asked questions and their answers."
      basePath="/knowledge-base/faqs"
    />
  );
}
