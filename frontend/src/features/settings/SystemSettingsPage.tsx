import UploadOutlinedIcon from "@mui/icons-material/UploadOutlined";
import { Alert, Box, Button, CircularProgress, Divider, Paper, Snackbar, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useRef, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import * as settingsApi from "./settingsApi";
import { useBranding } from "./useBranding";

const GENERIC_FAILURE = "Something went wrong. Please try again.";
const HEX_COLOR_PATTERN = /^#[0-9A-Fa-f]{6}$/;
const MAX_LOGO_BYTES = 2 * 1024 * 1024; // 2 MB — mirrors SystemSettingsController.MaxLogoBytes.
const ACCEPTED_LOGO_TYPES = ["image/png", "image/jpeg", "image/gif", "image/webp", "image/svg+xml"];

interface FieldErrors {
  applicationName?: string;
  supportEmail?: string;
  defaultTimezone?: string;
  defaultCulture?: string;
  brandDisplayName?: string;
  logoUrl?: string;
  primaryColor?: string;
  secondaryColor?: string;
}

/**
 * Very small e-mail check — the server (`EmailAddressAttribute`) is the source of truth; this only
 * avoids an obviously-wrong round trip. Deliberately does not require a dot in the domain part:
 * the seeded default (`support@localhost`) has none, and .NET's `EmailAddressAttribute` accepts it.
 */
function isPlausibleEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+$/.test(value);
}

function isAbsoluteHttpUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch {
    return false;
  }
}

/** Maps the controller's `{ error: "invalid_..." }` body to the field it belongs to. */
const ERROR_CODE_TO_FIELD: Record<string, keyof FieldErrors> = {
  invalid_application_name: "applicationName",
  invalid_support_email: "supportEmail",
  invalid_default_timezone: "defaultTimezone",
  invalid_default_culture: "defaultCulture",
  invalid_brand_display_name: "brandDisplayName",
  invalid_logo_url: "logoUrl",
  invalid_primary_color: "primaryColor",
  invalid_secondary_color: "secondaryColor",
};

export function SystemSettingsPage() {
  const { refresh } = useBranding();

  const [applicationName, setApplicationName] = useState("");
  const [supportEmail, setSupportEmail] = useState("");
  const [defaultTimezone, setDefaultTimezone] = useState("UTC");
  const [defaultCulture, setDefaultCulture] = useState("en-US");
  const [brandDisplayName, setBrandDisplayName] = useState("");
  const [logoUrl, setLogoUrl] = useState("");
  const [primaryColor, setPrimaryColor] = useState("#1976D2");
  const [secondaryColor, setSecondaryColor] = useState("#9C27B0");

  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [uploadingLogo, setUploadingLogo] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [savedOpen, setSavedOpen] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    let cancelled = false;
    settingsApi
      .getSystemSettings()
      .then((settings) => {
        if (cancelled) return;
        setApplicationName(settings.applicationName);
        setSupportEmail(settings.supportEmail);
        setDefaultTimezone(settings.defaultTimezone);
        setDefaultCulture(settings.defaultCulture);
        setBrandDisplayName(settings.brandDisplayName);
        setLogoUrl(settings.logoUrl ?? "");
        setPrimaryColor(settings.primaryColor);
        setSecondaryColor(settings.secondaryColor);
      })
      .catch(() => {
        if (!cancelled) setFormError("Could not load system settings. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  function validate(): FieldErrors {
    const errors: FieldErrors = {};

    const trimmedApplicationName = applicationName.trim();
    if (trimmedApplicationName.length === 0) {
      errors.applicationName = "Application name is required.";
    } else if (trimmedApplicationName.length > 120) {
      errors.applicationName = "Application name must be 120 characters or fewer.";
    }

    const trimmedSupportEmail = supportEmail.trim();
    if (trimmedSupportEmail.length === 0) {
      errors.supportEmail = "Support email is required.";
    } else if (!isPlausibleEmail(trimmedSupportEmail)) {
      errors.supportEmail = "Enter a valid email address.";
    } else if (trimmedSupportEmail.length > 200) {
      errors.supportEmail = "Support email must be 200 characters or fewer.";
    }

    const trimmedTimezone = defaultTimezone.trim();
    if (trimmedTimezone.length === 0) {
      errors.defaultTimezone = "Default timezone is required.";
    } else if (trimmedTimezone.length > 100) {
      errors.defaultTimezone = "Default timezone must be 100 characters or fewer.";
    }

    const trimmedCulture = defaultCulture.trim();
    if (trimmedCulture.length === 0) {
      errors.defaultCulture = "Default culture is required.";
    } else if (trimmedCulture.length > 20) {
      errors.defaultCulture = "Default culture must be 20 characters or fewer.";
    }

    const trimmedBrandDisplayName = brandDisplayName.trim();
    if (trimmedBrandDisplayName.length === 0) {
      errors.brandDisplayName = "Brand display name is required.";
    } else if (trimmedBrandDisplayName.length > 120) {
      errors.brandDisplayName = "Brand display name must be 120 characters or fewer.";
    }

    const trimmedLogoUrl = logoUrl.trim();
    if (trimmedLogoUrl.length > 0 && !isAbsoluteHttpUrl(trimmedLogoUrl)) {
      errors.logoUrl = "Logo URL must be an absolute http:// or https:// URL.";
    } else if (trimmedLogoUrl.length > 500) {
      errors.logoUrl = "Logo URL must be 500 characters or fewer.";
    }

    if (!HEX_COLOR_PATTERN.test(primaryColor.trim())) {
      errors.primaryColor = "Enter a hex color like #1976D2.";
    }

    if (!HEX_COLOR_PATTERN.test(secondaryColor.trim())) {
      errors.secondaryColor = "Enter a hex color like #9C27B0.";
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

    const trimmedLogoUrl = logoUrl.trim();

    try {
      await settingsApi.updateSystemSettings({
        applicationName: applicationName.trim(),
        supportEmail: supportEmail.trim(),
        defaultTimezone: defaultTimezone.trim(),
        defaultCulture: defaultCulture.trim(),
        brandDisplayName: brandDisplayName.trim(),
        logoUrl: trimmedLogoUrl.length > 0 ? trimmedLogoUrl : null,
        primaryColor: primaryColor.trim(),
        secondaryColor: secondaryColor.trim(),
      });
      refresh();
      setSavedOpen(true);
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 400) {
        const errorCode: unknown = caught.response.data?.error;
        const field = typeof errorCode === "string" ? ERROR_CODE_TO_FIELD[errorCode] : undefined;
        if (field !== undefined) {
          setFieldErrors((current) => ({ ...current, [field]: "The server rejected this value. Please check it and try again." }));
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

  /**
   * Uploads the picked file, then fills the Logo URL field with the URL it now lives at — exactly
   * as if that URL had been pasted in by hand. Nothing is persisted until Save is clicked; a
   * client-side size/type check runs first so an obviously-rejected file never leaves the browser.
   */
  async function handleLogoFileSelected(event: ChangeEvent<HTMLInputElement>): Promise<void> {
    const file = event.target.files?.[0];
    event.target.value = ""; // lets the same file be re-picked after an error, since <input> otherwise ignores an unchanged selection

    if (file === undefined) return;

    if (!ACCEPTED_LOGO_TYPES.includes(file.type)) {
      setFieldErrors((current) => ({ ...current, logoUrl: "Choose a PNG, JPEG, GIF, WEBP, or SVG image." }));
      return;
    }

    if (file.size > MAX_LOGO_BYTES) {
      setFieldErrors((current) => ({ ...current, logoUrl: "Image must be 2 MB or smaller." }));
      return;
    }

    // exactOptionalPropertyTypes forbids `logoUrl: undefined` — omit the key entirely instead.
    setFieldErrors((current) => {
      const { logoUrl: _clearedLogoUrl, ...rest } = current;
      return rest;
    });
    setUploadingLogo(true);
    setFormError(null);

    try {
      const { logoUrl: uploadedUrl } = await settingsApi.uploadLogo(file);
      setLogoUrl(uploadedUrl);
    } catch {
      setFieldErrors((current) => ({ ...current, logoUrl: "Upload failed. Please try again." }));
    } finally {
      setUploadingLogo(false);
    }
  }

  if (loading) {
    return (
      <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
        <CircularProgress size={22} />
        <Typography color="text.secondary">Loading…</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ maxWidth: 640 }}>
      <Typography variant="h4" sx={{ mb: 1 }}>
        System Settings
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Configure the application and its branding across the CRM.
      </Typography>

      <Paper component="form" onSubmit={handleSubmit} noValidate variant="outlined" sx={{ p: 3 }}>
        {formError !== null && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {formError}
          </Alert>
        )}

        <Typography variant="h6" sx={{ mb: 1 }}>
          General
        </Typography>

        <TextField
          id="applicationName"
          label="Application name"
          value={applicationName}
          onChange={(event) => setApplicationName(event.target.value)}
          error={fieldErrors.applicationName !== undefined}
          helperText={fieldErrors.applicationName}
          fullWidth
          margin="normal"
        />

        <TextField
          id="supportEmail"
          label="Support email"
          type="email"
          value={supportEmail}
          onChange={(event) => setSupportEmail(event.target.value)}
          error={fieldErrors.supportEmail !== undefined}
          helperText={fieldErrors.supportEmail}
          fullWidth
          margin="normal"
        />

        <TextField
          id="defaultTimezone"
          label="Default timezone"
          value={defaultTimezone}
          onChange={(event) => setDefaultTimezone(event.target.value)}
          error={fieldErrors.defaultTimezone !== undefined}
          helperText={fieldErrors.defaultTimezone ?? "IANA timezone id, e.g. UTC or Africa/Cairo."}
          fullWidth
          margin="normal"
        />

        <TextField
          id="defaultCulture"
          label="Default culture"
          value={defaultCulture}
          onChange={(event) => setDefaultCulture(event.target.value)}
          error={fieldErrors.defaultCulture !== undefined}
          helperText={fieldErrors.defaultCulture ?? "e.g. en-US."}
          fullWidth
          margin="normal"
        />

        <Divider sx={{ my: 3 }} />

        <Typography variant="h6" sx={{ mb: 1 }}>
          Branding
        </Typography>

        <TextField
          id="brandDisplayName"
          label="Brand display name"
          value={brandDisplayName}
          onChange={(event) => setBrandDisplayName(event.target.value)}
          error={fieldErrors.brandDisplayName !== undefined}
          helperText={fieldErrors.brandDisplayName ?? "Shown in the topbar; falls back to the application name when blank."}
          fullWidth
          margin="normal"
        />

        <Box sx={{ display: "flex", gap: 1.5, alignItems: "flex-start", mt: 2 }}>
          <TextField
            id="logoUrl"
            label="Logo URL"
            value={logoUrl}
            onChange={(event) => setLogoUrl(event.target.value)}
            error={fieldErrors.logoUrl !== undefined}
            helperText={fieldErrors.logoUrl ?? "Optional. Paste a URL, or upload an image from your device."}
            fullWidth
            margin="none"
          />
          {logoUrl.trim().length > 0 && (
            <Box
              component="img"
              src={logoUrl.trim()}
              alt="Logo preview"
              onError={(event) => {
                (event.target as HTMLImageElement).style.display = "none";
              }}
              sx={{ height: 40, width: 40, objectFit: "contain", border: "1px solid", borderColor: "divider", borderRadius: 1, mt: 0.25 }}
            />
          )}
          <input
            ref={fileInputRef}
            type="file"
            accept={ACCEPTED_LOGO_TYPES.join(",")}
            onChange={handleLogoFileSelected}
            style={{ display: "none" }}
          />
          <Button
            type="button"
            variant="outlined"
            startIcon={<UploadOutlinedIcon />}
            onClick={() => fileInputRef.current?.click()}
            disabled={uploadingLogo}
            sx={{ mt: 0.25, whiteSpace: "nowrap" }}
          >
            {uploadingLogo ? "Uploading…" : "Upload"}
          </Button>
        </Box>

        <Box sx={{ display: "flex", gap: 2, mt: 2 }}>
          <TextField
            id="primaryColor"
            label="Primary color"
            value={primaryColor}
            onChange={(event) => setPrimaryColor(event.target.value)}
            error={fieldErrors.primaryColor !== undefined}
            helperText={fieldErrors.primaryColor}
            fullWidth
          />
          <Box
            component="input"
            type="color"
            value={HEX_COLOR_PATTERN.test(primaryColor.trim()) ? primaryColor.trim() : "#1976D2"}
            onChange={(event) => setPrimaryColor((event.target as HTMLInputElement).value.toUpperCase())}
            sx={{ width: 48, height: 40, mt: 1, border: "1px solid", borderColor: "divider", borderRadius: 1, p: 0.5 }}
          />
        </Box>

        <Box sx={{ display: "flex", gap: 2, mt: 2, mb: 1 }}>
          <TextField
            id="secondaryColor"
            label="Secondary color"
            value={secondaryColor}
            onChange={(event) => setSecondaryColor(event.target.value)}
            error={fieldErrors.secondaryColor !== undefined}
            helperText={fieldErrors.secondaryColor}
            fullWidth
          />
          <Box
            component="input"
            type="color"
            value={HEX_COLOR_PATTERN.test(secondaryColor.trim()) ? secondaryColor.trim() : "#9C27B0"}
            onChange={(event) => setSecondaryColor((event.target as HTMLInputElement).value.toUpperCase())}
            sx={{ width: 48, height: 40, mt: 1, border: "1px solid", borderColor: "divider", borderRadius: 1, p: 0.5 }}
          />
        </Box>

        <Box sx={{ display: "flex", gap: 1.5, mt: 3 }}>
          <Button type="submit" variant="contained" disabled={submitting}>
            {submitting ? "Saving…" : "Save"}
          </Button>
        </Box>
      </Paper>

      <Snackbar
        open={savedOpen}
        autoHideDuration={3000}
        onClose={() => setSavedOpen(false)}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
      >
        <Alert severity="success" onClose={() => setSavedOpen(false)} sx={{ width: "100%" }}>
          Settings saved.
        </Alert>
      </Snackbar>
    </Box>
  );
}
