import AdminPanelSettingsOutlinedIcon from "@mui/icons-material/AdminPanelSettingsOutlined";
import ApartmentOutlinedIcon from "@mui/icons-material/ApartmentOutlined";
import ChatOutlinedIcon from "@mui/icons-material/ChatOutlined";
import ChecklistOutlinedIcon from "@mui/icons-material/ChecklistOutlined";
import ConfirmationNumberOutlinedIcon from "@mui/icons-material/ConfirmationNumberOutlined";
import ContactsOutlinedIcon from "@mui/icons-material/ContactsOutlined";
import ContentPasteOutlinedIcon from "@mui/icons-material/ContentPasteOutlined";
import DashboardOutlinedIcon from "@mui/icons-material/DashboardOutlined";
import HelpOutlineOutlinedIcon from "@mui/icons-material/HelpOutlineOutlined";
import HistoryIcon from "@mui/icons-material/History";
import LabelOutlinedIcon from "@mui/icons-material/LabelOutlined";
import LogoutIcon from "@mui/icons-material/Logout";
import LowPriorityOutlinedIcon from "@mui/icons-material/LowPriorityOutlined";
import MenuIcon from "@mui/icons-material/Menu";
import NotificationsOutlinedIcon from "@mui/icons-material/NotificationsOutlined";
import PeopleAltOutlinedIcon from "@mui/icons-material/PeopleAltOutlined";
import SettingsOutlinedIcon from "@mui/icons-material/SettingsOutlined";
import StoreOutlinedIcon from "@mui/icons-material/StoreOutlined";
import TimerOutlinedIcon from "@mui/icons-material/TimerOutlined";
import {
  AppBar,
  Avatar,
  Badge,
  Box,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Toolbar,
  Tooltip,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import type { MouseEvent, ReactNode } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../features/auth/useAuth";
import * as notificationsApi from "../features/notifications/notificationsApi";
import type { Notification } from "../features/notifications/types";
import { useBranding } from "../features/settings/useBranding";

const DRAWER_WIDTH = 240;

interface NavItem {
  label: string;
  to: string;
  icon: ReactNode;
}

/**
 * Shared chrome for every authenticated page: a top bar and a left-hand nav. Purely presentational
 * — it reads `user`/`hasPermission`/`logout` from AuthContext but adds no new API calls or business
 * logic.
 */
export function AppLayout({ children }: { children: ReactNode }) {
  const { user, hasPermission, logout } = useAuth();
  const { branding } = useBranding();
  const location = useLocation();
  const navigate = useNavigate();
  const [mobileOpen, setMobileOpen] = useState(false);

  // Story 25: the bell in the top bar — a lightweight companion to the full "Notifications" nav
  // entry, not a replacement for it. No live push/polling (matching this codebase's "no new
  // background job framework" convention) — the unread count is fetched once on mount and refreshed
  // whenever the menu is opened or an item is marked read from it.
  const canSeeNotifications = hasPermission("notifications.view.own");
  const [notifAnchorEl, setNotifAnchorEl] = useState<HTMLElement | null>(null);
  const [unreadCount, setUnreadCount] = useState(0);
  const [recentNotifications, setRecentNotifications] = useState<Notification[]>([]);

  function refreshUnreadCount(): void {
    if (!canSeeNotifications) return;
    notificationsApi
      .listMyNotifications({ unreadOnly: true, pageSize: 1 })
      .then((result) => setUnreadCount(result.total))
      .catch(() => undefined);
  }

  useEffect(() => {
    refreshUnreadCount();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- fires once on mount; canSeeNotifications doesn't change within a session.
  }, []);

  function handleOpenNotifications(event: MouseEvent<HTMLElement>): void {
    setNotifAnchorEl(event.currentTarget);
    notificationsApi
      .listMyNotifications({ pageSize: 5 })
      .then((result) => setRecentNotifications(result.items))
      .catch(() => undefined);
  }

  function handleSelectNotification(notification: Notification): void {
    setNotifAnchorEl(null);
    if (notification.readAtUtc === null) {
      notificationsApi
        .markNotificationRead(notification.id)
        .then(refreshUnreadCount)
        .catch(() => undefined);
    }
    navigate(`/tickets/${notification.ticketId}`);
  }

  // Story 03: nav visibility is driven by permissions returned from the API, not a hard-coded role.
  const navItems: NavItem[] = [
    { label: "Dashboard", to: "/", icon: <DashboardOutlinedIcon /> },
    ...(hasPermission("users.view") ? [{ label: "Users", to: "/users", icon: <PeopleAltOutlinedIcon /> }] : []),
    ...(hasPermission("roles.view") ? [{ label: "Roles", to: "/roles", icon: <AdminPanelSettingsOutlinedIcon /> }] : []),
    ...(hasPermission("customers.view")
      ? [{ label: "Customers", to: "/customers", icon: <ContactsOutlinedIcon /> }]
      : []),
    ...(hasPermission("departments.view")
      ? [{ label: "Departments", to: "/departments", icon: <ApartmentOutlinedIcon /> }]
      : []),
    ...(hasPermission("branches.view") ? [{ label: "Branches", to: "/branches", icon: <StoreOutlinedIcon /> }] : []),
    ...(hasPermission("tickets.view") ? [{ label: "Tickets", to: "/tickets", icon: <ConfirmationNumberOutlinedIcon /> }] : []),
    ...(hasPermission("tickets.view")
      ? [{ label: "Agent Dashboard", to: "/agent/dashboard", icon: <DashboardOutlinedIcon /> }]
      : []),
    ...(hasPermission("agenttasks.read")
      ? [{ label: "Tasks & Reminders", to: "/agent-desk/tasks", icon: <ChecklistOutlinedIcon /> }]
      : []),
    ...(hasPermission("tickets.categories.view")
      ? [{ label: "Ticket Categories", to: "/tickets/categories", icon: <LabelOutlinedIcon /> }]
      : []),
    ...(hasPermission("tickets.priorities.view")
      ? [{ label: "Ticket Priorities", to: "/tickets/priorities", icon: <LowPriorityOutlinedIcon /> }]
      : []),
    ...(hasPermission("quickreplies.view")
      ? [{ label: "Quick Replies", to: "/quick-replies", icon: <ContentPasteOutlinedIcon /> }]
      : []),
    ...(hasPermission("livechat.view")
      ? [{ label: "Live Chat", to: "/agent-desk/live-chat", icon: <ChatOutlinedIcon /> }]
      : []),
    ...(hasPermission("notifications.view.own")
      ? [{ label: "Notifications", to: "/notifications", icon: <NotificationsOutlinedIcon /> }]
      : []),
    ...(hasPermission("knowledgebase.articles.view")
      ? [{ label: "Knowledge Base", to: "/knowledge-base", icon: <HelpOutlineOutlinedIcon /> }]
      : []),
    ...(hasPermission("audit.view") ? [{ label: "Audit Logs", to: "/audit-logs", icon: <HistoryIcon /> }] : []),
    ...(hasPermission("system.view")
      ? [{ label: "System Settings", to: "/admin/system-settings", icon: <SettingsOutlinedIcon /> }]
      : []),
    ...(hasPermission("system.view")
      ? [{ label: "SLA Policies", to: "/admin/sla-policies", icon: <TimerOutlinedIcon /> }]
      : []),
  ];

  // Story 06: brandDisplayName (falling back to applicationName, then the static default) drives
  // both the drawer header and the topbar title; a broken logoUrl hides itself via onError rather
  // than showing a broken-image icon.
  const [logoFailed, setLogoFailed] = useState(false);
  const brandTitle = branding.brandDisplayName || branding.applicationName;
  const showLogo = branding.logoUrl !== null && branding.logoUrl.length > 0 && !logoFailed;

  const isActive = (to: string) =>
    to === "/" ? location.pathname === "/" : location.pathname.startsWith(to);

  const drawerContent = (
    <div>
      <Toolbar sx={{ gap: 1 }}>
        {showLogo && (
          // eslint-disable-next-line jsx-a11y/alt-text -- decorative; the brand name text alongside it already labels the mark.
          <Box
            component="img"
            src={branding.logoUrl ?? undefined}
            onError={() => setLogoFailed(true)}
            sx={{ height: 28, width: "auto" }}
          />
        )}
        <Typography variant="h6" noWrap sx={{ fontWeight: 700, color: "primary.main" }}>
          {brandTitle}
        </Typography>
      </Toolbar>
      <Divider />
      <List sx={{ px: 1, pt: 1 }}>
        {navItems.map((item) => (
          <ListItemButton
            key={item.to}
            component={Link}
            to={item.to}
            selected={isActive(item.to)}
            onClick={() => setMobileOpen(false)}
            sx={{ borderRadius: 2, mb: 0.5 }}
          >
            <ListItemIcon sx={{ minWidth: 36 }}>{item.icon}</ListItemIcon>
            <ListItemText primary={item.label} />
          </ListItemButton>
        ))}
      </List>
    </div>
  );

  return (
    <Box sx={{ display: "flex", minHeight: "100vh" }}>
      <AppBar
        position="fixed"
        elevation={0}
        sx={{
          width: { sm: `calc(100% - ${DRAWER_WIDTH}px)` },
          ml: { sm: `${DRAWER_WIDTH}px` },
          borderBottom: "1px solid",
          borderColor: "divider",
        }}
      >
        <Toolbar sx={{ gap: 1 }}>
          <IconButton
            color="inherit"
            edge="start"
            onClick={() => setMobileOpen(true)}
            sx={{ mr: 1, display: { sm: "none" } }}
            aria-label="Open navigation"
          >
            <MenuIcon />
          </IconButton>

          <Typography variant="h6" noWrap sx={{ flexGrow: 1, fontWeight: 600 }}>
            {brandTitle}
          </Typography>

          {user !== null && canSeeNotifications && (
            <>
              <Tooltip title="Notifications">
                <IconButton color="inherit" onClick={handleOpenNotifications} aria-label="Notifications">
                  <Badge badgeContent={unreadCount} color="error" max={99}>
                    <NotificationsOutlinedIcon />
                  </Badge>
                </IconButton>
              </Tooltip>
              <Menu
                anchorEl={notifAnchorEl}
                open={notifAnchorEl !== null}
                onClose={() => setNotifAnchorEl(null)}
                slotProps={{ paper: { sx: { width: 360, maxWidth: "100%" } } }}
              >
                {recentNotifications.length === 0 ? (
                  <MenuItem disabled>No notifications yet.</MenuItem>
                ) : (
                  recentNotifications.map((notification) => (
                    <MenuItem
                      key={notification.id}
                      onClick={() => handleSelectNotification(notification)}
                      sx={{ whiteSpace: "normal", alignItems: "flex-start", py: 1 }}
                    >
                      <ListItemText
                        primary={notification.subject}
                        secondary={notification.body}
                        slotProps={{
                          primary: { sx: { fontWeight: notification.readAtUtc === null ? 700 : 400 } },
                          secondary: {
                            sx: {
                              overflow: "hidden",
                              textOverflow: "ellipsis",
                              display: "-webkit-box",
                              WebkitLineClamp: 2,
                              WebkitBoxOrient: "vertical",
                            },
                          },
                        }}
                      />
                    </MenuItem>
                  ))
                )}
                <Divider />
                <MenuItem component={Link} to="/notifications" onClick={() => setNotifAnchorEl(null)}>
                  View all notifications
                </MenuItem>
              </Menu>
            </>
          )}

          {user !== null && (
            <>
              <Avatar sx={{ width: 32, height: 32, bgcolor: "primary.main", fontSize: 14 }}>
                {user.displayName.charAt(0).toUpperCase()}
              </Avatar>
              <Typography variant="body2" sx={{ display: { xs: "none", sm: "block" } }}>
                {user.displayName}
              </Typography>
              <Tooltip title="Log out">
                <IconButton color="inherit" onClick={logout} aria-label="Log out">
                  <LogoutIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </>
          )}
        </Toolbar>
      </AppBar>

      <Box component="nav" sx={{ width: { sm: DRAWER_WIDTH }, flexShrink: { sm: 0 } }}>
        {/* Mobile: temporary drawer, toggled by the menu button above. */}
        <Drawer
          variant="temporary"
          open={mobileOpen}
          onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{
            display: { xs: "block", sm: "none" },
            "& .MuiDrawer-paper": { width: DRAWER_WIDTH },
          }}
        >
          {drawerContent}
        </Drawer>

        {/* Desktop: permanent drawer, always visible. */}
        <Drawer
          variant="permanent"
          sx={{
            display: { xs: "none", sm: "block" },
            "& .MuiDrawer-paper": { width: DRAWER_WIDTH, borderRight: "1px solid", borderColor: "divider" },
          }}
          open
        >
          {drawerContent}
        </Drawer>
      </Box>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          width: { sm: `calc(100% - ${DRAWER_WIDTH}px)` },
          bgcolor: "background.default",
          minHeight: "100vh",
        }}
      >
        <Toolbar />
        <Box sx={{ p: { xs: 2, sm: 3 } }}>{children}</Box>
      </Box>
    </Box>
  );
}
