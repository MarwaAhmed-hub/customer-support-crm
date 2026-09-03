import UploadFileIcon from "@mui/icons-material/UploadFile";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import axios from "axios";
import { useEffect, useRef, useState } from "react";
import type { ChangeEvent } from "react";
import { useAuth } from "../../auth/useAuth";
import * as attachmentsApi from "./attachmentsApi";
import type { CustomerAttachment } from "./types";

const GENERIC_FAILURE = "Something went wrong. Please try again.";

function formatSize(bytes: number): string {
  return `${(bytes / 1024).toFixed(1)} KB`;
}

function formatDate(isoString: string): string {
  return new Date(isoString).toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
  });
}

export function CustomerAttachmentsPanel({ customerId }: { customerId: string }) {
  const { hasPermission } = useAuth();
  const canCreate = hasPermission("customers.attachments.create");
  const canDelete = hasPermission("customers.attachments.delete");

  const [attachments, setAttachments] = useState<CustomerAttachment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [uploading, setUploading] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    attachmentsApi
      .listAttachments(customerId)
      .then((result) => {
        if (!cancelled) setAttachments(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load attachments. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [customerId]);

  async function handleFileChosen(event: ChangeEvent<HTMLInputElement>): Promise<void> {
    const file = event.target.files?.[0];
    // Reset the input immediately so choosing the exact same file again still fires onChange.
    event.target.value = "";
    if (file === undefined || uploading) return;

    setUploading(true);
    setError(null);

    try {
      const uploaded = await attachmentsApi.uploadAttachment(customerId, file);
      setAttachments((current) => [uploaded, ...current]);
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 400) {
        const errorCode: unknown = caught.response.data?.error;
        if (errorCode === "attachment.too_large") {
          setError("This file is too large.");
        } else if (errorCode === "attachment.invalid_type") {
          setError("This file type is not supported.");
        } else if (errorCode === "attachment.empty") {
          setError("The chosen file is empty.");
        } else {
          setError(GENERIC_FAILURE);
        }
      } else {
        setError("Could not upload this file. Please try again.");
      }
    } finally {
      setUploading(false);
    }
  }

  async function handleDownload(attachment: CustomerAttachment): Promise<void> {
    try {
      await attachmentsApi.downloadAttachment(customerId, attachment.id, attachment.fileName);
    } catch {
      setError("Could not download this file. Please try again.");
    }
  }

  async function handleDelete(attachment: CustomerAttachment): Promise<void> {
    if (deletingId !== null) return;
    if (!window.confirm(`Delete "${attachment.fileName}"? This cannot be undone.`)) return;

    setDeletingId(attachment.id);
    setError(null);

    try {
      await attachmentsApi.deleteAttachment(customerId, attachment.id);
      setAttachments((current) => current.filter((a) => a.id !== attachment.id));
    } catch {
      setError("Could not delete this file. Please try again.");
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <Typography variant="subtitle1">Attachments</Typography>
        {canCreate && (
          <>
            <input ref={fileInputRef} type="file" hidden onChange={(event) => void handleFileChosen(event)} />
            <Button
              size="small"
              variant="outlined"
              startIcon={<UploadFileIcon />}
              disabled={uploading}
              onClick={() => fileInputRef.current?.click()}
            >
              {uploading ? "Uploading…" : "Upload"}
            </Button>
          </>
        )}
      </Box>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 3 }}>
          <CircularProgress size={22} />
          <Typography color="text.secondary">Loading…</Typography>
        </Box>
      ) : attachments.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 3, textAlign: "center" }}>
          <Typography color="text.secondary">No attachments yet.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>File</TableCell>
                <TableCell>Size</TableCell>
                <TableCell>Uploaded by</TableCell>
                <TableCell>Uploaded</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {attachments.map((attachment) => (
                <TableRow key={attachment.id} hover>
                  <TableCell>{attachment.fileName}</TableCell>
                  <TableCell>{formatSize(attachment.sizeBytes)}</TableCell>
                  <TableCell>{attachment.uploadedByDisplayName ?? "System"}</TableCell>
                  <TableCell>{formatDate(attachment.uploadedAt)}</TableCell>
                  <TableCell align="right">
                    <Button size="small" onClick={() => void handleDownload(attachment)}>
                      Download
                    </Button>
                    {canDelete && (
                      <Button
                        size="small"
                        color="error"
                        disabled={deletingId === attachment.id}
                        onClick={() => void handleDelete(attachment)}
                      >
                        {deletingId === attachment.id ? "Deleting…" : "Delete"}
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
