import { createBrowserRouter, Navigate } from 'react-router-dom';
import { AppShell } from '@/app/layout/AppShell';
import { DashboardPage } from '@/modules/dashboard/pages/DashboardPage';
import { InventoryPage } from '@/modules/inventory/pages/InventoryPage';
import { EquipmentPage } from '@/modules/inventory/pages/EquipmentPage';
import { PartnersPage } from '@/modules/inventory/pages/PartnersPage';
import { ControlledPage } from '@/modules/controlled/pages/ControlledPage';
import { LabelsPage } from '@/modules/labels/pages/LabelsPage';
import { QuickConsumptionPage } from '@/modules/quick-consumption/pages/QuickConsumptionPage';
import { MembersPage } from '@/modules/identity/pages/MembersPage';
import { ProfileEditPage } from '@/modules/identity/pages/ProfileEditPage';
import { ConfigurationPage } from '@/modules/configuration/pages/ConfigurationPage';
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
      { path: 'quick-consumption', element: <QuickConsumptionPage /> },
      { path: 'inventory', element: <InventoryPage /> },
      { path: 'labels', element: <LabelsPage /> },
      { path: 'controlled', element: <ControlledPage /> },
      { path: 'equipment', element: <EquipmentPage /> },
      { path: 'partners', element: <PartnersPage /> },
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
      { path: 'audit', element: <AuditPage /> },
      { path: 'notifications', element: <NotificationsPage /> },
      { path: '*', element: <Navigate to="/" replace /> },
    ],
  },
]);
