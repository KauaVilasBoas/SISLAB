import { createBrowserRouter, Navigate } from 'react-router-dom';
import { AppShell } from '@/app/layout/AppShell';
import { RouteErrorBoundary } from '@/app/RouteErrorBoundary';
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
import { DilutionCalculatorPage } from '@/modules/experiments/pages/DilutionCalculatorPage';
import {
  InVitroShowcase,
  InVivoShowcase,
  BiobankShowcase,
  PendenciesShowcase,
} from '@/modules/experiments/components/premium-showcase';
import { CalendarPage } from '@/modules/agenda/pages/CalendarPage';
import { AuditShowcase } from '@/modules/audit/components/premium-showcase';
import { NotificationsPage } from '@/modules/notifications/pages/NotificationsPage';
import { LoginRoute } from '@/modules/auth/components/LoginRoute';
import { InvitationAcceptPage } from '@/modules/identity/pages/InvitationAcceptPage';
import { RequireAuth } from '@/modules/auth/components/RequireAuth';
import { RequirePermissionRoute } from '@/modules/auth/components/RequirePermissionRoute';
import { Permissions } from '@/modules/auth/permissions';

/**
 * Central route table (card [E7] #44). /login is public (served by LoginRoute, which bounces to the
 * dashboard in the auto-signed-in demo build) and lives OUTSIDE the AppShell;
 * every AppShell route is wrapped in <RequireAuth>, which redirects to /login (preserving the
 * attempted location) when there is no session. Paths align with app/navigation.ts.
 */
export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginRoute />,
    errorElement: <RouteErrorBoundary />,
  },
  {
    path: '/invite/:token',
    element: <InvitationAcceptPage />,
    errorElement: <RouteErrorBoundary />,
  },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    // Errors thrown by any child route bubble up to here, so a single boundary covers the whole shell.
    errorElement: <RouteErrorBoundary />,
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
      // Experiments (cards [E11] #68/#73/#89/#90) — reserved for the Premium edition. Every route below,
      // list AND detail, renders the immersive Premium showcase (PremiumModuleGate) instead of the real
      // screen, so reaching it by clicking the sidebar OR typing the URL lands on the vitrine (defense-in-depth,
      // same spirit as RequirePermissionRoute). The serial-dilution calculator is the one exception: it stays
      // free and fully navigable.
      { path: 'experiments', element: <InVitroShowcase /> },
      // Serial-dilution calculator (SISLAB-05) — stateless compute, free and navigable. Static path registered
      // before the :id route so it never matches as an experiment id, and it is NOT gated.
      { path: 'experiments/dilution', element: <DilutionCalculatorPage /> },
      { path: 'experiments/:id', element: <InVitroShowcase /> },
      { path: 'experiments/in-vivo/projects', element: <InVivoShowcase /> },
      { path: 'experiments/in-vivo/projects/:projectId', element: <InVivoShowcase /> },
      { path: 'experiments/in-vivo/biobank', element: <BiobankShowcase /> },
      { path: 'experiments/in-vivo/biobank/:sampleId', element: <BiobankShowcase /> },
      { path: 'experiments/in-vivo/pendencies', element: <PendenciesShowcase /> },
      { path: 'agenda/schedule', element: <CalendarPage /> },
      // Audit (append-only trail) — reserved for the Premium edition. Renders the immersive Premium showcase
      // instead of the real screen, so reaching it by clicking the sidebar OR typing the URL lands on the vitrine.
      { path: 'audit', element: <AuditShowcase /> },
      { path: 'notifications', element: <NotificationsPage /> },
      { path: '*', element: <Navigate to="/" replace /> },
    ],
  },
]);
