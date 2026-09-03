import { CssBaseline } from "@mui/material";
import { ThemeProvider } from "@mui/material/styles";
import { useMemo } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AgentDashboardPage } from "./features/agent-desk/dashboard/AgentDashboardPage";
import { TaskFormPage } from "./features/agent-desk/tasks/TaskFormPage";
import { TasksListPage } from "./features/agent-desk/tasks/TasksListPage";
import { AdminRoute } from "./features/auth/AdminRoute";
import { AuthProvider } from "./features/auth/AuthContext";
import { LoginPage } from "./features/auth/LoginPage";
import { PermissionRoute } from "./features/auth/PermissionRoute";
import { ProtectedRoute } from "./features/auth/ProtectedRoute";
import { AuditLogsPage } from "./features/audit/AuditLogsPage";
import { BranchFormPage } from "./features/branches/BranchFormPage";
import { BranchesListPage } from "./features/branches/BranchesListPage";
import { ChannelSimulatorPage } from "./features/communications/simulator/ChannelSimulatorPage";
import { CustomerDetailPage } from "./features/customers/CustomerDetailPage";
import { CustomerFormPage } from "./features/customers/CustomerFormPage";
import { CustomersListPage } from "./features/customers/CustomersListPage";
import { DepartmentFormPage } from "./features/departments/DepartmentFormPage";
import { DepartmentsListPage } from "./features/departments/DepartmentsListPage";
import { HomePage } from "./features/home/HomePage";
import { FaqsListPage } from "./features/knowledge-base/FaqsListPage";
import { GuideDetailPage } from "./features/knowledge-base/GuideDetailPage";
import { GuideFormPage } from "./features/knowledge-base/GuideFormPage";
import { GuidesListPage } from "./features/knowledge-base/GuidesListPage";
import { HelpArticlesListPage } from "./features/knowledge-base/HelpArticlesListPage";
import { KnowledgeBaseArticleDetailPage } from "./features/knowledge-base/KnowledgeBaseArticleDetailPage";
import { KnowledgeBaseArticleFormPage } from "./features/knowledge-base/KnowledgeBaseArticleFormPage";
import { KnowledgeBaseCategoriesPage } from "./features/knowledge-base/KnowledgeBaseCategoriesPage";
import { KnowledgeBaseHomePage } from "./features/knowledge-base/KnowledgeBaseHomePage";
import { KnowledgeBaseSearchPage } from "./features/knowledge-base/search/KnowledgeBaseSearchPage";
import { SolutionDetailPage } from "./features/knowledge-base/SolutionDetailPage";
import { SolutionFormPage } from "./features/knowledge-base/SolutionFormPage";
import { SolutionsListPage } from "./features/knowledge-base/SolutionsListPage";
import { LiveChatConversationPage } from "./features/live-chat/LiveChatConversationPage";
import { LiveChatInboxPage } from "./features/live-chat/LiveChatInboxPage";
import { LiveChatWidgetPage } from "./features/live-chat/LiveChatWidgetPage";
import { NotificationsInboxPage } from "./features/notifications/NotificationsInboxPage";
import { SupportRequestPage } from "./features/public/SupportRequestPage";
import { QuickReplyFormPage } from "./features/quick-replies/QuickReplyFormPage";
import { QuickRepliesListPage } from "./features/quick-replies/QuickRepliesListPage";
import { RoleFormPage } from "./features/roles/RoleFormPage";
import { RolePermissionsPage } from "./features/roles/RolePermissionsPage";
import { RolesListPage } from "./features/roles/RolesListPage";
import { BrandingProvider } from "./features/settings/BrandingContext";
import { SystemSettingsPage } from "./features/settings/SystemSettingsPage";
import { useBranding } from "./features/settings/useBranding";
import { SlaPoliciesPage } from "./features/sla/SlaPoliciesPage";
import { TicketCategoriesListPage } from "./features/tickets/categories/TicketCategoriesListPage";
import { TicketCategoryFormPage } from "./features/tickets/categories/TicketCategoryFormPage";
import { TicketPrioritiesListPage } from "./features/tickets/priorities/TicketPrioritiesListPage";
import { TicketPriorityFormPage } from "./features/tickets/priorities/TicketPriorityFormPage";
import { TicketDetailPage } from "./features/tickets/tickets/TicketDetailPage";
import { TicketFormPage } from "./features/tickets/tickets/TicketFormPage";
import { TicketsListPage } from "./features/tickets/tickets/TicketsListPage";
import { UserDetailPage } from "./features/users/UserDetailPage";
import { UserFormPage } from "./features/users/UserFormPage";
import { UsersListPage } from "./features/users/UsersListPage";
import { buildTheme } from "./theme";

export function App() {
  return (
    // AuthProvider wraps BrowserRouter so navigation never remounts the provider. BrandingProvider
    // sits inside it (it reads useAuth's status) and outside ThemedApp, which turns the fetched
    // branding colors into the live MUI theme (Story 06).
    <AuthProvider>
      <BrandingProvider>
        <ThemedApp />
      </BrandingProvider>
    </AuthProvider>
  );
}

function ThemedApp() {
  const { branding } = useBranding();
  const theme = useMemo(
    () => buildTheme(branding.primaryColor, branding.secondaryColor),
    [branding.primaryColor, branding.secondaryColor],
  );

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          {/* Story 19: the public Web Form — no auth, no AppLayout, linked from outside the CRM. */}
          <Route path="/support" element={<SupportRequestPage />} />
          {/* Story 21: the public live chat widget — same no-auth, no-AppLayout pattern as /support. */}
          <Route path="/live-chat" element={<LiveChatWidgetPage />} />
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <HomePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/users"
            element={
              <AdminRoute>
                <UsersListPage />
              </AdminRoute>
            }
          />
          <Route
            path="/users/new"
            element={
              <AdminRoute>
                <UserFormPage />
              </AdminRoute>
            }
          />
          <Route
            path="/users/:id"
            element={
              <AdminRoute>
                <UserDetailPage />
              </AdminRoute>
            }
          />
          <Route
            path="/users/:id/edit"
            element={
              <AdminRoute>
                <UserFormPage />
              </AdminRoute>
            }
          />
          <Route
            path="/roles"
            element={
              <PermissionRoute required="roles.view">
                <RolesListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/roles/new"
            element={
              <PermissionRoute required="roles.create">
                <RoleFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/roles/:id/edit"
            element={
              <PermissionRoute required="roles.update">
                <RoleFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/roles/:id/permissions"
            element={
              <PermissionRoute required="permissions.assign">
                <RolePermissionsPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/departments"
            element={
              <PermissionRoute required="departments.view">
                <DepartmentsListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/departments/new"
            element={
              <PermissionRoute required="departments.create">
                <DepartmentFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/departments/:id/edit"
            element={
              <PermissionRoute required="departments.update">
                <DepartmentFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/branches"
            element={
              <PermissionRoute required="branches.view">
                <BranchesListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/branches/new"
            element={
              <PermissionRoute required="branches.create">
                <BranchFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/branches/:id/edit"
            element={
              <PermissionRoute required="branches.update">
                <BranchFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/customers"
            element={
              <PermissionRoute required="customers.view">
                <CustomersListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/customers/new"
            element={
              <PermissionRoute required="customers.create">
                <CustomerFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/customers/:id"
            element={
              <PermissionRoute required="customers.view">
                <CustomerDetailPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/customers/:id/edit"
            element={
              <PermissionRoute required="customers.update">
                <CustomerFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/tickets/categories"
            element={
              <PermissionRoute required="tickets.categories.view">
                <TicketCategoriesListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/tickets/categories/new"
            element={
              <PermissionRoute required="tickets.categories.manage">
                <TicketCategoryFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/tickets/categories/:id/edit"
            element={
              <PermissionRoute required="tickets.categories.manage">
                <TicketCategoryFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/tickets/priorities"
            element={
              <PermissionRoute required="tickets.priorities.view">
                <TicketPrioritiesListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/quick-replies"
            element={
              <PermissionRoute required="quickreplies.view">
                <QuickRepliesListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/quick-replies/new"
            element={
              <PermissionRoute required="quickreplies.manage">
                <QuickReplyFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/quick-replies/:id/edit"
            element={
              <PermissionRoute required="quickreplies.manage">
                <QuickReplyFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/tickets/priorities/new"
            element={
              <PermissionRoute required="tickets.priorities.manage">
                <TicketPriorityFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/tickets/priorities/:id/edit"
            element={
              <PermissionRoute required="tickets.priorities.manage">
                <TicketPriorityFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/tickets"
            element={
              <PermissionRoute required="tickets.view">
                <TicketsListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/tickets/new"
            element={
              <PermissionRoute required="tickets.create">
                <TicketFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/tickets/:id"
            element={
              <PermissionRoute required="tickets.view">
                <TicketDetailPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/tickets/:id/edit"
            element={
              <PermissionRoute required="tickets.update">
                <TicketFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/audit-logs"
            element={
              <PermissionRoute required="audit.view">
                <AuditLogsPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/notifications"
            element={
              <PermissionRoute required="notifications.view.own">
                <NotificationsInboxPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base"
            element={
              <PermissionRoute required="knowledgebase.articles.view">
                <KnowledgeBaseHomePage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/faqs"
            element={
              <PermissionRoute required="knowledgebase.articles.view">
                <FaqsListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/articles"
            element={
              <PermissionRoute required="knowledgebase.articles.view">
                <HelpArticlesListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/faqs/new"
            element={
              <PermissionRoute required="knowledgebase.articles.manage">
                <KnowledgeBaseArticleFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/articles/new"
            element={
              <PermissionRoute required="knowledgebase.articles.manage">
                <KnowledgeBaseArticleFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/faqs/:id/edit"
            element={
              <PermissionRoute required="knowledgebase.articles.manage">
                <KnowledgeBaseArticleFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/articles/:id/edit"
            element={
              <PermissionRoute required="knowledgebase.articles.manage">
                <KnowledgeBaseArticleFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/faqs/:id"
            element={
              <PermissionRoute required="knowledgebase.articles.view">
                <KnowledgeBaseArticleDetailPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/articles/:id"
            element={
              <PermissionRoute required="knowledgebase.articles.view">
                <KnowledgeBaseArticleDetailPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/search"
            element={
              <PermissionRoute required="knowledgebase.search">
                <KnowledgeBaseSearchPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/categories"
            element={
              <PermissionRoute required="knowledgebase.categories.manage">
                <KnowledgeBaseCategoriesPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/solutions"
            element={
              <PermissionRoute required="knowledgebase.solutions.view">
                <SolutionsListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/solutions/new"
            element={
              <PermissionRoute required="knowledgebase.solutions.manage">
                <SolutionFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/solutions/:id/edit"
            element={
              <PermissionRoute required="knowledgebase.solutions.manage">
                <SolutionFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/solutions/:id"
            element={
              <PermissionRoute required="knowledgebase.solutions.view">
                <SolutionDetailPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/guides"
            element={
              <PermissionRoute required="knowledgebase.guides.view">
                <GuidesListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/guides/new"
            element={
              <PermissionRoute required="knowledgebase.guides.manage">
                <GuideFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/guides/:id/edit"
            element={
              <PermissionRoute required="knowledgebase.guides.manage">
                <GuideFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/knowledge-base/guides/:id"
            element={
              <PermissionRoute required="knowledgebase.guides.view">
                <GuideDetailPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/agent/dashboard"
            element={
              <PermissionRoute required="tickets.view">
                <AgentDashboardPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/agent-desk/tasks"
            element={
              <PermissionRoute required="agenttasks.read">
                <TasksListPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/agent-desk/tasks/new"
            element={
              <PermissionRoute required="agenttasks.create">
                <TaskFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/agent-desk/tasks/:id/edit"
            element={
              <PermissionRoute required="agenttasks.update">
                <TaskFormPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/admin/system-settings"
            element={
              <PermissionRoute required="system.view">
                <SystemSettingsPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/admin/sla-policies"
            element={
              <PermissionRoute required="system.view">
                <SlaPoliciesPage />
              </PermissionRoute>
            }
          />
          {/* Dev/test tool — no auth, same as every endpoint it exercises (correction: Email/WhatsApp/
              SMS ingest were originally staff-gated, but each represents a customer submitting
              something, never a staff action, so this page needs no login either; same as /support
              and /live-chat). */}
          <Route path="/admin/channel-simulator" element={<ChannelSimulatorPage />} />
          <Route
            path="/agent-desk/live-chat"
            element={
              <PermissionRoute required="livechat.view">
                <LiveChatInboxPage />
              </PermissionRoute>
            }
          />
          <Route
            path="/agent-desk/live-chat/:id"
            element={
              <PermissionRoute required="livechat.view">
                <LiveChatConversationPage />
              </PermissionRoute>
            }
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </ThemeProvider>
  );
}
