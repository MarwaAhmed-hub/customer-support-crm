import { ArticleListView } from "./ArticleListView";

export function HelpArticlesListPage() {
  return (
    <ArticleListView
      contentType="HelpArticle"
      title="Help Articles"
      description="Longer-form guides and how-tos."
      basePath="/knowledge-base/articles"
    />
  );
}
