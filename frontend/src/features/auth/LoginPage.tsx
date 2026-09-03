import ChatOutlinedIcon from "@mui/icons-material/ChatOutlined";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import {
  Alert,
  Box,
  Button,
  Divider,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Paper,
  TextField,
  Typography,
} from "@mui/material";
import axios from "axios";
import { useState } from "react";
import type { FormEvent } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "./useAuth";

// This module deliberately does NOT import authApi. Going through useAuth().login is what keeps
// login to exactly one request per submit; a direct import is how a second call would creep in.

const INVALID_CREDENTIALS = "Invalid email or password.";
const GENERIC_FAILURE = "Something went wrong. Please try again.";

/** Reads the intended path preserved by ProtectedRoute, without casting an unknown. */
function readFrom(state: unknown): string {
  if (typeof state === "object" && state !== null && "from" in state) {
    const value: unknown = Reflect.get(state, "from");
    if (typeof value === "string" && value.length > 0) {
      return value;
    }
  }
  return "/";
}

export function LoginPage() {
  const { login, status } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Already signed in and navigated here manually.
  if (status === "authenticated") {
    return <Navigate to="/" replace />;
  }

  const canSubmit = email.trim().length > 0 && password.trim().length > 0 && !submitting;

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();

    // Guards a fast double-click or a second Enter — the other half of "login must not happen twice".
    if (submitting) return;

    setSubmitting(true);
    setError(null);

    try {
      await login(email, password);
      navigate(readFrom(location.state), { replace: true });   // keeps /login out of the history stack
    } catch (caught: unknown) {
      // Never surface the raw server error to the user.
      const status401 = axios.isAxiosError(caught) && caught.response?.status === 401;
      setError(status401 ? INVALID_CREDENTIALS : GENERIC_FAILURE);
    } finally {
      // In a finally block so a failed attempt does not lock the form.
      setSubmitting(false);
    }
  }

  return (
    <Box
      component="main"
      sx={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "100vh",
        p: 2,
        bgcolor: "background.default",
      }}
    >
      <Paper
        component="form"
        onSubmit={handleSubmit}
        noValidate
        elevation={0}
        sx={{
          width: "100%",
          maxWidth: 380,
          p: 4,
          border: "1px solid",
          borderColor: "divider",
        }}
      >
        <Typography variant="h5" sx={{ mb: 0.5 }}>
          Sign in
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
          Customer Support CRM
        </Typography>

        {error !== null && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <TextField
          id="email"
          name="email"
          label="Email"
          type="email"
          autoComplete="username"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          fullWidth
          margin="normal"
        />
        <TextField
          id="password"
          name="password"
          label="Password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          fullWidth
          margin="normal"
        />

        <Button type="submit" variant="contained" fullWidth disabled={!canSubmit} sx={{ mt: 3, py: 1.1 }}>
          {submitting ? "Signing in…" : "Sign in"}
        </Button>

        {/*
          One place for both halves of the channel-testing story instead of scattered links each
          tester has to remember. Both are genuinely anonymous, same as a real customer reaching either
          one — neither needs a login, so both links work whether or not the form above is filled in.
        */}
        <Divider sx={{ my: 3 }} />
        <Typography variant="overline" color="text.secondary" sx={{ display: "block", mb: 1, letterSpacing: 0.6 }}>
          Dev / test tools
        </Typography>
        <Paper variant="outlined" sx={{ borderRadius: 1.5, overflow: "hidden" }}>
          <List disablePadding>
            <ListItemButton component={Link} to="/admin/channel-simulator" sx={{ py: 1.25 }}>
              <ListItemIcon sx={{ minWidth: 34 }}>
                <ScienceOutlinedIcon fontSize="small" color="action" />
              </ListItemIcon>
              <ListItemText
                primary="Channel Simulator"
                secondary="Email · Web Form · WhatsApp · SMS"
                slotProps={{ primary: { variant: "body2", sx: { fontWeight: 600 } }, secondary: { variant: "caption" } }}
              />
              <ChevronRightIcon fontSize="small" color="disabled" />
            </ListItemButton>
            <Divider component="li" />
            <ListItemButton component={Link} to="/live-chat" sx={{ py: 1.25 }}>
              <ListItemIcon sx={{ minWidth: 34 }}>
                <ChatOutlinedIcon fontSize="small" color="action" />
              </ListItemIcon>
              <ListItemText
                primary="Live Chat widget"
                secondary="No login needed"
                slotProps={{ primary: { variant: "body2", sx: { fontWeight: 600 } }, secondary: { variant: "caption" } }}
              />
              <ChevronRightIcon fontSize="small" color="disabled" />
            </ListItemButton>
          </List>
        </Paper>
      </Paper>
    </Box>
  );
}
