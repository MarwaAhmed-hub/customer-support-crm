import { Alert, Box, Button, Paper, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useState } from "react";
import type { FormEvent } from "react";
import * as publicWebFormApi from "./publicWebFormApi";

const GENERIC_FAILURE = "Something went wrong. Please try again.";
const RATE_LIMITED = "Too many requests — please wait a minute and try again.";

interface FieldErrors {
  name?: string;
  email?: string;
  subject?: string;
  description?: string;
}

/**
 * Story 19: the public, unauthenticated Web Form — a customer with no CRM account lands here (e.g.
 * linked from a company website's "Contact Support" button) and submits a ticket via
 * `POST /api/public/web-forms/tickets`. Deliberately rendered standalone with no `AppLayout` (no
 * sidebar, no "you must sign in" framing) — same pattern as `LoginPage`.
 *
 * `website` is a honeypot: visually hidden (off-screen, not `display:none`, and not `type="hidden"` —
 * a naive bot that blindly fills every named input still finds and fills it) so a real visitor never
 * sees or touches it. A submission with it filled still shows the exact same success screen — the
 * backend already returns an indistinguishable 202, and revealing anything different here would let a
 * bot detect and adapt.
 */
export function SupportRequestPage() {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [phone, setPhone] = useState("");
  const [website, setWebsite] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  function validate(): FieldErrors {
    const errors: FieldErrors = {};

    if (name.trim().length === 0) {
      errors.name = "Name is required.";
    } else if (name.trim().length > 128) {
      errors.name = "Name must be 128 characters or fewer.";
    }

    const trimmedEmail = email.trim();
    if (trimmedEmail.length === 0) {
      errors.email = "Email is required.";
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      errors.email = "Enter a valid email address.";
    }

    if (subject.trim().length === 0) {
      errors.subject = "Subject is required.";
    } else if (subject.trim().length > 200) {
      errors.subject = "Subject must be 200 characters or fewer.";
    }

    if (description.trim().length === 0) {
      errors.description = "Please describe your issue.";
    } else if (description.trim().length > 4000) {
      errors.description = "Description must be 4000 characters or fewer.";
    }

    return errors;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (submitting) return;

    const errors = validate();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSubmitting(true);
    setFormError(null);

    // exactOptionalPropertyTypes forbids `phone: undefined`, so the key is omitted entirely rather
    // than present-but-undefined when the field is blank.
    const payload: publicWebFormApi.WebFormSubmissionPayload = {
      name: name.trim(),
      email: email.trim(),
      subject: subject.trim(),
      description: description.trim(),
      website,
    };
    const trimmedPhone = phone.trim();
    if (trimmedPhone.length > 0) payload.phone = trimmedPhone;

    try {
      await publicWebFormApi.submitWebFormTicket(payload);
      setSubmitted(true);
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 429) {
        setFormError(RATE_LIMITED);
      } else if (axios.isAxiosError(caught) && caught.response?.status === 400) {
        const code: unknown = caught.response.data?.error;
        if (code === "invalid_email") {
          setFieldErrors((current) => ({ ...current, email: "Enter a valid email address." }));
        } else {
          setFormError(GENERIC_FAILURE);
        }
      } else {
        setFormError(GENERIC_FAILURE);
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (submitted) {
    return (
      <Box component="main" sx={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", p: 2, bgcolor: "background.default" }}>
        <Paper elevation={0} sx={{ width: "100%", maxWidth: 480, p: 4, border: "1px solid", borderColor: "divider", textAlign: "center" }}>
          <Typography variant="h5" sx={{ mb: 1.5 }}>
            Thanks — we've got it.
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Your request has been received and a member of our support team will get back to you by
            email shortly.
          </Typography>
        </Paper>
      </Box>
    );
  }

  return (
    <Box component="main" sx={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", p: 2, bgcolor: "background.default" }}>
      <Paper component="form" onSubmit={handleSubmit} noValidate elevation={0} sx={{ width: "100%", maxWidth: 480, p: 4, border: "1px solid", borderColor: "divider" }}>
        <Typography variant="h5" sx={{ mb: 0.5 }}>
          Contact Support
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
          Tell us what's going on and we'll follow up by email.
        </Typography>

        {formError !== null && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {formError}
          </Alert>
        )}

        <TextField
          id="name"
          label="Your name"
          value={name}
          onChange={(event) => setName(event.target.value)}
          error={fieldErrors.name !== undefined}
          helperText={fieldErrors.name}
          fullWidth
          margin="normal"
        />
        <TextField
          id="email"
          label="Email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          error={fieldErrors.email !== undefined}
          helperText={fieldErrors.email}
          fullWidth
          margin="normal"
        />
        <TextField
          id="phone"
          label="Phone (optional)"
          value={phone}
          onChange={(event) => setPhone(event.target.value)}
          fullWidth
          margin="normal"
        />
        <TextField
          id="subject"
          label="Subject"
          value={subject}
          onChange={(event) => setSubject(event.target.value)}
          error={fieldErrors.subject !== undefined}
          helperText={fieldErrors.subject}
          fullWidth
          margin="normal"
        />
        <TextField
          id="description"
          label="How can we help?"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          error={fieldErrors.description !== undefined}
          helperText={fieldErrors.description}
          fullWidth
          multiline
          minRows={4}
          margin="normal"
        />

        {/* Honeypot — invisible and unreachable by keyboard for a real visitor. */}
        <Box aria-hidden="true" sx={{ position: "absolute", left: -9999, top: -9999, width: 1, height: 1, overflow: "hidden" }}>
          <TextField
            id="website"
            label="Website"
            name="website"
            autoComplete="off"
            value={website}
            onChange={(event) => setWebsite(event.target.value)}
            slotProps={{ htmlInput: { tabIndex: -1 } }}
          />
        </Box>

        <Button type="submit" variant="contained" fullWidth disabled={submitting} sx={{ mt: 3, py: 1.1 }}>
          {submitting ? "Sending…" : "Send request"}
        </Button>
      </Paper>
    </Box>
  );
}
