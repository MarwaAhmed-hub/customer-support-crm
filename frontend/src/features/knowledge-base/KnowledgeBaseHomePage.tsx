import ArticleOutlinedIcon from "@mui/icons-material/ArticleOutlined";
import BuildOutlinedIcon from "@mui/icons-material/BuildOutlined";
import HelpOutlineOutlinedIcon from "@mui/icons-material/HelpOutlineOutlined";
import LabelOutlinedIcon from "@mui/icons-material/LabelOutlined";
import ListAltOutlinedIcon from "@mui/icons-material/ListAltOutlined";
import { Box, Card, CardActionArea, CardContent, Typography } from "@mui/material";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/useAuth";

export function KnowledgeBaseHomePage() {
  const { hasPermission } = useAuth();

  return (
    <Box sx={{ maxWidth: 900 }}>
      <Typography variant="h4" sx={{ mb: 1 }}>
        Knowledge Base
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Answers to common questions and longer-form help articles.
      </Typography>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr 1fr" }, gap: 2 }}>
        <Card variant="outlined">
          <CardActionArea component={Link} to="/knowledge-base/faqs" sx={{ height: "100%", p: 1 }}>
            <CardContent sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1, textAlign: "center" }}>
              <HelpOutlineOutlinedIcon color="primary" fontSize="large" />
              <Typography variant="h6">FAQs</Typography>
              <Typography variant="body2" color="text.secondary">
                Quick answers to common questions.
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>

        <Card variant="outlined">
          <CardActionArea component={Link} to="/knowledge-base/articles" sx={{ height: "100%", p: 1 }}>
            <CardContent sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1, textAlign: "center" }}>
              <ArticleOutlinedIcon color="primary" fontSize="large" />
              <Typography variant="h6">Help Articles</Typography>
              <Typography variant="body2" color="text.secondary">
                Longer-form guides and how-tos.
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>

        {hasPermission("knowledgebase.solutions.view") && (
          <Card variant="outlined">
            <CardActionArea component={Link} to="/knowledge-base/solutions" sx={{ height: "100%", p: 1 }}>
              <CardContent sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1, textAlign: "center" }}>
                <BuildOutlinedIcon color="primary" fontSize="large" />
                <Typography variant="h6">Solutions</Typography>
                <Typography variant="body2" color="text.secondary">
                  Known problems and their fixes.
                </Typography>
              </CardContent>
            </CardActionArea>
          </Card>
        )}

        {hasPermission("knowledgebase.guides.view") && (
          <Card variant="outlined">
            <CardActionArea component={Link} to="/knowledge-base/guides" sx={{ height: "100%", p: 1 }}>
              <CardContent sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1, textAlign: "center" }}>
                <ListAltOutlinedIcon color="primary" fontSize="large" />
                <Typography variant="h6">Guides</Typography>
                <Typography variant="body2" color="text.secondary">
                  Step-by-step walkthroughs.
                </Typography>
              </CardContent>
            </CardActionArea>
          </Card>
        )}

        {hasPermission("knowledgebase.categories.manage") && (
          <Card variant="outlined">
            <CardActionArea component={Link} to="/knowledge-base/categories" sx={{ height: "100%", p: 1 }}>
              <CardContent sx={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 1, textAlign: "center" }}>
                <LabelOutlinedIcon color="primary" fontSize="large" />
                <Typography variant="h6">Categories</Typography>
                <Typography variant="body2" color="text.secondary">
                  Manage knowledge base categories.
                </Typography>
              </CardContent>
            </CardActionArea>
          </Card>
        )}
      </Box>
    </Box>
  );
}
