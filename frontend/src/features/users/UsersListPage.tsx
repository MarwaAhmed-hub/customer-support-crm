import PersonAddAlt1Icon from "@mui/icons-material/PersonAddAlt1";
import SearchIcon from "@mui/icons-material/Search";
import {
  Alert,
  Box,
  Button,
  Card,
  Chip,
  CircularProgress,
  InputAdornment,
  MenuItem,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import axios from "axios";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import * as branchesApi from "../branches/branchesApi";
import type { Branch } from "../branches/types";
import * as departmentsApi from "../departments/departmentsApi";
import type { Department } from "../departments/types";
import * as usersApi from "./usersApi";
import type { UserListItem } from "./types";

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 300;
const ALL_OPTION = "";

export function UsersListPage() {
  const { hasPermission } = useAuth();

  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [departmentFilter, setDepartmentFilter] = useState(ALL_OPTION);
  const [branchFilter, setBranchFilter] = useState(ALL_OPTION);
  const [page, setPage] = useState(1);

  const [items, setItems] = useState<UserListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [departments, setDepartments] = useState<Department[]>([]);
  const [branches, setBranches] = useState<Branch[]>([]);

  // Filter dropdown options. Non-fatal on failure: worst case the filters have nothing to offer.
  useEffect(() => {
    let cancelled = false;
    Promise.all([departmentsApi.listDepartments({ includeInactive: false }), branchesApi.listBranches({ includeInactive: false })])
      .then(([fetchedDepartments, fetchedBranches]) => {
        if (cancelled) return;
        setDepartments(fetchedDepartments);
        setBranches(fetchedBranches);
      })
      .catch(() => undefined);

    return () => {
      cancelled = true;
    };
  }, []);

  // Debounce the raw input into the value that actually drives the fetch. This timer re-arms and
  // fires on every keystroke — including a no-op fire on mount, before the user has typed anything
  // — so it must only commit `search` when the trimmed value actually differs from what's already
  // committed; otherwise setSearch("") on mount is a same-value set React bails out on, but an
  // unconditional setPage(1) right beside it would NOT bail out, and could fire well after the user
  // has already navigated to another page (this was observed as a rare flake under a slow/loaded
  // test run, not just in theory).
  useEffect(() => {
    const handle = setTimeout(() => {
      setSearch((current) => {
        const next = searchInput.trim();
        return next === current ? current : next;
      });
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(handle);
  }, [searchInput]);

  // Reset to page 1 only when the search term or a filter actually changes — not on every debounce
  // timer tick — so page state set by the user (e.g. via Next/Previous) is never clobbered by a
  // stray no-op commit. Skipped on the very first render: page is already 1 then, and calling
  // setPage(1) unconditionally on mount is exactly the pattern that caused the bug above.
  const isFirstRender = useRef(true);
  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }
    setPage(1);
  }, [search, departmentFilter, branchFilter]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    // exactOptionalPropertyTypes forbids e.g. `search: undefined` — each key is omitted entirely
    // rather than present-but-undefined when its filter is unset.
    const params: usersApi.ListUsersParams = { page, pageSize: PAGE_SIZE };
    if (search.length > 0) params.search = search;
    if (departmentFilter !== ALL_OPTION) params.departmentId = departmentFilter;
    if (branchFilter !== ALL_OPTION) params.branchId = branchFilter;

    usersApi
      .listUsers(params)
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
        setTotal(result.total);
      })
      .catch(() => {
        if (cancelled) return;
        setError("Could not load users. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [page, search, departmentFilter, branchFilter]);

  async function toggleActive(user: UserListItem): Promise<void> {
    try {
      const updated = await usersApi.setUserActive(user.id, !user.isActive);
      setItems((current) => current.map((u) => (u.id === updated.id ? { ...u, isActive: updated.isActive } : u)));
    } catch (caught: unknown) {
      const message = axios.isAxiosError(caught)
        ? "Could not update this user's status. Please try again."
        : "Something went wrong. Please try again.";
      setError(message);
    }
  }

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <Box sx={{ maxWidth: 1100 }}>
      <Box
        sx={{
          display: "flex",
          flexDirection: { xs: "column", sm: "row" },
          justifyContent: "space-between",
          alignItems: { sm: "center" },
          gap: 2,
          mb: 3,
        }}
      >
        <Box>
          <Typography variant="h4">Users</Typography>
          <Typography variant="body2" color="text.secondary">
            Manage who can access the CRM.
          </Typography>
        </Box>
        {hasPermission("users.create") && (
          <Button component={Link} to="/users/new" variant="contained" startIcon={<PersonAddAlt1Icon />}>
            New user
          </Button>
        )}
      </Box>

      <Card variant="outlined" sx={{ mb: 2, p: 2 }}>
        <Box sx={{ display: "flex", flexDirection: { xs: "column", sm: "row" }, gap: 2 }}>
          <TextField
            placeholder="Search by name or email…"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            size="small"
            fullWidth
            slotProps={{
              htmlInput: { "aria-label": "Search users" },
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" color="action" />
                  </InputAdornment>
                ),
              },
            }}
          />
          <TextField
            select
            label="Department"
            value={departmentFilter}
            onChange={(event) => setDepartmentFilter(event.target.value)}
            size="small"
            sx={{ minWidth: 180 }}
            // displayEmpty: without it, MUI's Select renders the box blank rather than the "All
            // departments" MenuItem's label whenever the value is the empty string. inputLabel.shrink:
            // without it, the "Department" label sits centered (as if the field were truly empty)
            // and overlaps that same displayed text instead of floating above it.
            slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
          >
            <MenuItem value={ALL_OPTION}>All departments</MenuItem>
            {departments.map((department) => (
              <MenuItem key={department.id} value={department.id}>
                {department.name}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Branch"
            value={branchFilter}
            onChange={(event) => setBranchFilter(event.target.value)}
            size="small"
            sx={{ minWidth: 180 }}
            slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
          >
            <MenuItem value={ALL_OPTION}>All branches</MenuItem>
            {branches.map((branch) => (
              <MenuItem key={branch.id} value={branch.id}>
                {branch.name}
              </MenuItem>
            ))}
          </TextField>
        </Box>
      </Card>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
          <CircularProgress size={22} />
          <Typography color="text.secondary">Loading…</Typography>
        </Box>
      ) : items.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No users found.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Email</TableCell>
                <TableCell>Name</TableCell>
                <TableCell>Department</TableCell>
                <TableCell>Branch</TableCell>
                <TableCell>Status</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((user) => (
                <TableRow key={user.id} hover>
                  <TableCell>{user.email}</TableCell>
                  <TableCell>{user.displayName}</TableCell>
                  <TableCell>{user.departmentName ?? "—"}</TableCell>
                  <TableCell>{user.branchName ?? "—"}</TableCell>
                  <TableCell>
                    <Chip
                      label={user.isActive ? "Active" : "Inactive"}
                      color={user.isActive ? "success" : "default"}
                      size="small"
                      variant={user.isActive ? "filled" : "outlined"}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Box sx={{ display: "flex", gap: 1, justifyContent: "flex-end" }}>
                      <Button component={Link} to={`/users/${user.id}`} size="small">
                        View
                      </Button>
                      {hasPermission("users.update") && (
                        <>
                          <Button component={Link} to={`/users/${user.id}/edit`} size="small">
                            Edit
                          </Button>
                          <Button
                            size="small"
                            color={user.isActive ? "error" : "success"}
                            onClick={() => void toggleActive(user)}
                          >
                            {user.isActive ? "Deactivate" : "Activate"}
                          </Button>
                        </>
                      )}
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 2, mt: 3 }}>
        <Button
          variant="outlined"
          size="small"
          disabled={page <= 1}
          onClick={() => setPage((current) => Math.max(1, current - 1))}
        >
          Previous
        </Button>
        <Typography variant="body2" color="text.secondary">
          Page {page} of {totalPages}
        </Typography>
        <Button
          variant="outlined"
          size="small"
          disabled={page >= totalPages}
          onClick={() => setPage((current) => current + 1)}
        >
          Next
        </Button>
      </Box>
    </Box>
  );
}
