import { createBrowserRouter, Navigate } from 'react-router-dom';
import { AppShell } from '@/app/layout/AppShell';
import { DashboardPage } from '@/modules/dashboard/pages/DashboardPage';
import { InventoryPage } from '@/modules/inventory/pages/InventoryPage';
import { EquipmentPage } from '@/modules/inventory/pages/EquipmentPage';
import { PartnersPage } from '@/modules/inventory/pages/PartnersPage';
import { CostReportPage } from '@/modules/inventory/pages/CostReportPage';
import { ControlledPage } from '@/modules/controlled/pages/ControlledPage';
import { LabelsPage } from '@/modules/labels/pages/LabelsPage';
import { QuickConsumptionPage } from '@/modules/quick-consumption/pages/QuickConsumptionPage';
import { MembersPage } from '@/modules/identity/pages/MembersPage';
import { ProfileEditPage } from '@/modules/identity/pages/ProfileEditPage';
import { ConfigurationPage } from '@/modules/configuration/pages/ConfigurationPage';
import { ExperimentsPage } from '@/modules/experiments/pages/ExperimentsPage';
import { ExperimentDetailPage } from '@/modules/experiments/pages/ExperimentDetailPage';
import { DilutionCalculatorPage } from '@/modules/experiments/pages/DilutionCalculatorPage';
import { ProjectsPage } from '@/modules/in-vivo/pages/ProjectsPage';
import { ProjectDetailPage } from '@/modules/in-vivo/pages/ProjectDetailPage';
import { BiobankPage } from '@/modules/in-vivo/pages/BiobankPage';
import { SampleDetailPage } from '@/modules/in-vivo/pages/SampleDetailPage';
import { PendenciesPage } from '@/modules/in-vivo/pages/PendenciesPage';
import { CalendarPage } from '@/modules/agenda/pages/CalendarPage';
import { AuditPage } from '@/modules/audit/pages/AuditPage';
import { NotificationsPage } from '@/modules/notifications/pages/NotificationsPage';
import { LoginPage } from '@/modules/auth/pages/LoginPage';
import { InvitationAcceptPage } from '@/modules/identity/pages/InvitationAcceptPage';
import { RequireAuth } from '@/modules/auth/components/RequireAuth';
import { RequirePermissionRoute } from '@/modules/auth/components/RequirePermissionRoute';
import { Permissions } from '@/modules/auth/permissions';

/**
 * Central route table (card [E7] #44). /login is public and lives OUTSIDE the AppShell;
 * every AppShell route is wrapped in <RequireAuth>, which redirects to /login (preserving the
 * attempted location) when there is no session. Paths align with app/navigation.ts.
 */
export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/invite/:token',
    element: <InvitationAcceptPage />,
  },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <DashboardPage /> },
      {
        path: 'quick-consumption',
        element: (
          <RequirePermissionRoute codes={[Permissions.stock.registerConsumption]}>
            <QuickConsumptionPage />
          </RequirePermissionRoute>
        ),
      },
      { path: 'inventory', element: <InventoryPage /> },
      { path: 'labels', element: <LabelsPage /> },
      { path: 'controlled', element: <ControlledPage /> },
      { path: 'equipment', element: <EquipmentPage /> },
      { path: 'partners', element: <PartnersPage /> },
      {
        path: 'inventory/cost-report',
        element: (
          <RequirePermissionRoute codes={[Permissions.inventory.costRead]}>
            <CostReportPage />
          </RequirePermissionRoute>
        ),
      },
      {
        path: 'members',
        element: (
          <RequirePermissionRoute
            codes={[Permissions.members.listEnriched, Permissions.profiles.listProfiles]}
          >
            <MembersPage />
          </RequirePermissionRoute>
        ),
      },
      {
        path: 'members/profiles/:profileId',
        element: (
          <RequirePermissionRoute codes={[Permissions.profiles.listProfiles]}>
            <ProfileEditPage />
          </RequirePermissionRoute>
        ),
      },
      { path: 'configuration', element: <ConfigurationPage /> },
      // Experiments (card [E11] #68) — visible to every member for now (list/detail reads are only
      // [Authorize]-gated; the write actions are permission-gated on the backend).
      { path: 'experiments', element: <ExperimentsPage /> },
      // Serial-dilution calculator (SISLAB-05) — stateless compute, visible to every member (the GET is only
      // [Authorize]-gated). Static path registered before the :id route so it never matches as an experiment id.
      { path: 'experiments/dilution', element: <DilutionCalculatorPage /> },
      { path: 'experiments/:id', element: <ExperimentDetailPage /> },
      // In vivo (cards [E11] #73/#89/#90) — experimental design, biobank and pendencies. Reads are only
      // [Authorize]-gated; the write actions are permission-gated on the backend and in the UI.
      { path: 'experiments/in-vivo/projects', element: <ProjectsPage /> },
      { path: 'experiments/in-vivo/projects/:projectId', element: <ProjectDetailPage /> },
      { path: 'experiments/in-vivo/biobank', element: <BiobankPage /> },
      { path: 'experiments/in-vivo/biobank/:sampleId', element: <SampleDetailPage /> },
      { path: 'experiments/in-vivo/pendencies', element: <PendenciesPage /> },
      { path: 'agenda/schedule', element: <CalendarPage /> },
      { path: 'audit', element: <AuditPage /> },
      { path: 'notifications', element: <NotificationsPage /> },
      { path: '*', element: <Navigate to="/" replace /> },
    ],
  },
]);
