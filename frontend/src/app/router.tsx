import { createBrowserRouter, Navigate } from 'react-router-dom';
import { AppShell } from '@/app/layout/AppShell';
import { DashboardPage } from '@/modules/dashboard/pages/DashboardPage';
import { InventoryPage } from '@/modules/inventory/pages/InventoryPage';
import { MembersPage } from '@/modules/identity/pages/MembersPage';
import { ConfigurationPage } from '@/modules/configuration/pages/ConfigurationPage';
import { AuditPage } from '@/modules/audit/pages/AuditPage';
import { NotificationsPage } from '@/modules/notifications/pages/NotificationsPage';

/**
 * Central route table. Each module contributes its mother screen under the
 * authenticated AppShell. Paths align with app/navigation.ts.
 */
export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppShell />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'inventory', element: <InventoryPage /> },
      { path: 'members', element: <MembersPage /> },
      { path: 'configuration', element: <ConfigurationPage /> },
      { path: 'audit', element: <AuditPage /> },
      { path: 'notifications', element: <NotificationsPage /> },
      { path: '*', element: <Navigate to="/" replace /> },
    ],
  },
]);
