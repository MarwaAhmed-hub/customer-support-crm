import ArrowForwardIcon from "@mui/icons-material/ArrowForward";
import PeopleAltOutlinedIcon from "@mui/icons-material/PeopleAltOutlined";
import { Box, Button, Card, CardContent, Typography } from "@mui/material";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/useAuth";

/**
 * Makes no API call of its own: the user comes from AuthContext, populated either by the login
 * response or by the me() hydration. It must not call the diagnostics ping endpoint — those are
 * smoke-test endpoints, not product UI.
 */
export function HomePage() {
  const { user, isAdmin } = useAuth();

  return (
    <Box sx={{ maxWidth: 900 }}>
      <Typography variant="h4" sx={{ mb: 0.5 }}>
        Welcome, {user?.displayName}
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 4 }}>
        Here&apos;s a quick overview of your workspace.
      </Typography>

      <Box sx={{ display: "flex", flexDirection: { xs: "column", sm: "row" }, gap: 2 }}>
        <Card variant="outlined" sx={{ flex: 1 }}>
          <CardContent>
            <Typography variant="overline" color="text.secondary">
              Signed in as
            </Typography>
            <Typography variant="h6">{user?.displayName}</Typography>
            <Typography variant="body2" color="text.secondary">
              {user?.email}
            </Typography>
          </CardContent>
        </Card>

        {isAdmin && (
          <Card variant="outlined" sx={{ flex: 1 }}>
            <CardContent>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 1 }}>
                <PeopleAltOutlinedIcon color="primary" />
                <Typography variant="h6">Users</Typography>
              </Box>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                View, create, and manage CRM user accounts.
              </Typography>
              <Button
                component={Link}
                to="/users"
                size="small"
                endIcon={<ArrowForwardIcon />}
              >
                Manage users
              </Button>
            </CardContent>
          </Card>
        )}
      </Box>
    </Box>
  );
}
