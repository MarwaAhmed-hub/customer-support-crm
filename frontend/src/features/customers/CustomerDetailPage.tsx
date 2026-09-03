import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { Alert, Box, Button, Card, CardContent, CircularProgress, Divider, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import * as customersApi from "./customersApi";
import type { Customer } from "./types";
import { CustomerInteractionHistory } from "./interactions/CustomerInteractionHistory";
import { CustomerNotesPanel } from "./notes/CustomerNotesPanel";
import { CustomerAttachmentsPanel } from "./attachments/CustomerAttachmentsPanel";
import { CustomerTicketsPanel } from "./CustomerTicketsPanel";

function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="body1">{value}</Typography>
    </Box>
  );
}

export function CustomerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { hasPermission } = useAuth();

  const [customer, setCustomer] = useState<Customer | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (id === undefined) return;

    let cancelled = false;
    setLoading(true);
    setNotFound(false);
    setError(null);

    customersApi
      .getCustomer(id)
      .then((current) => {
        if (!cancelled) setCustomer(current);
      })
      .catch((caught: unknown) => {
        if (cancelled) return;
        if (axios.isAxiosError(caught) && caught.response?.status === 404) {
          setNotFound(true);
        } else {
          setError("Could not load this customer. Please try again.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [id]);

  async function handleDelete(): Promise<void> {
    if (customer === null || deleting) return;
    if (!window.confirm(`Delete ${customer.firstName} ${customer.lastName}? This cannot be undone.`)) return;

    setDeleting(true);
    setError(null);

    try {
      await customersApi.deleteCustomer(customer.id);
      navigate("/customers", { replace: true });
    } catch {
      setError("Could not delete this customer. Please try again.");
      setDeleting(false);
    }
  }

  const backLink = (
    <Button component={Link} to="/customers" startIcon={<ArrowBackIcon />} size="small" sx={{ mb: 2 }}>
      Back to customers
    </Button>
  );

  if (loading) {
    return (
      <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
        <CircularProgress size={22} />
        <Typography color="text.secondary">Loading…</Typography>
      </Box>
    );
  }

  if (notFound) {
    return (
      <Box sx={{ maxWidth: 900 }}>
        <Alert severity="warning" sx={{ mb: 2 }}>
          Customer not found.
        </Alert>
        {backLink}
      </Box>
    );
  }

  // A non-404 failure also leaves `customer` at null (the fetch never populated it) — checked after
  // `notFound` specifically so a generic error doesn't fall into the "not found" branch above.
  if (error !== null && customer === null) {
    return (
      <Box sx={{ maxWidth: 900 }}>
        <Alert severity="error">{error}</Alert>
      </Box>
    );
  }

  if (customer === null) {
    // Defensive fallback; not reachable once loading/notFound/error are exhausted.
    return null;
  }

  return (
    <Box sx={{ maxWidth: 900 }}>
      {backLink}

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h4" component="h1" sx={{ mb: 2 }}>
            {customer.firstName} {customer.lastName}
          </Typography>

          {error !== null && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          <Box sx={{ display: "flex", flexDirection: "column", gap: 2, mb: 3 }}>
            <Field label="Company" value={customer.companyName ?? "—"} />
            <Field label="Email" value={customer.email ?? "—"} />
            <Field label="Phone" value={customer.phone ?? "—"} />
            <Field label="Created" value={new Date(customer.createdAt).toLocaleString()} />
          </Box>

          <Divider sx={{ mb: 2 }} />

          <Box sx={{ display: "flex", gap: 1.5 }}>
            {hasPermission("customers.update") && (
              <Button variant="outlined" onClick={() => navigate(`/customers/${customer.id}/edit`)}>
                Edit
              </Button>
            )}
            {hasPermission("customers.delete") && (
              <Button variant="outlined" color="error" disabled={deleting} onClick={() => void handleDelete()}>
                {deleting ? "Deleting…" : "Delete"}
              </Button>
            )}
          </Box>
        </CardContent>
      </Card>

      {hasPermission("tickets.view") && (
        <Card variant="outlined" sx={{ mt: 3 }}>
          <CardContent>
            <CustomerTicketsPanel customerId={customer.id} />
          </CardContent>
        </Card>
      )}

      {hasPermission("customers.interactions.read") && (
        <Card variant="outlined" sx={{ mt: 3 }}>
          <CardContent>
            <CustomerInteractionHistory customerId={customer.id} />
          </CardContent>
        </Card>
      )}

      {hasPermission("customers.notes.read") && (
        <Card variant="outlined" sx={{ mt: 3 }}>
          <CardContent>
            <CustomerNotesPanel customerId={customer.id} />
          </CardContent>
        </Card>
      )}

      {hasPermission("customers.attachments.read") && (
        <Card variant="outlined" sx={{ mt: 3 }}>
          <CardContent>
            <CustomerAttachmentsPanel customerId={customer.id} />
          </CardContent>
        </Card>
      )}
    </Box>
  );
}
