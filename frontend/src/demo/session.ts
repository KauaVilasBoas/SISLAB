import { Permissions } from '@/modules/auth/permissions';
import type {
  ActiveCompany,
  CompanyMembership,
  CurrentUser,
  CurrentUserPermissions,
  LoginResult,
} from '@/modules/auth/types';

/**
 * The fictional identity the backend-less demo signs in as. Shapes match the real auth contracts exactly
 * (see `@/modules/auth/types`), so the AuthProvider bootstrap/login flow runs unchanged against the mock.
 */

export const DEMO_COMPANY: CompanyMembership = {
  id: 'demo-company',
  name: 'LAFTE (Demonstração)',
};

export const DEMO_ACTIVE_COMPANY: ActiveCompany = { companyId: DEMO_COMPANY.id };

export const DEMO_USER: CurrentUser = {
  id: 'demo-user',
  email: 'demo@sislab.dev',
  username: 'Convidado',
  createdAt: '2026-01-05T12:00:00Z',
  lastLoginAt: '2026-08-05T09:00:00Z',
  emailConfirmedAt: '2026-01-05T12:05:00Z',
  profiles: [{ id: 'demo-admin', name: 'Administrador' }],
};

/** Flatten the whole permission catalogue so no permission-gated screen is hidden in the demo. */
function allPermissionCodes(): string[] {
  return Object.values(Permissions).flatMap((group) =>
    Object.values(group as Record<string, string>),
  );
}

export const DEMO_PERMISSIONS: CurrentUserPermissions = {
  permissions: allPermissionCodes(),
};

/** The login endpoint returns a raw Lumen LoginResult; the demo's is inert (the mock never checks it). */
export const DEMO_LOGIN_RESULT: LoginResult = {
  accessToken: 'demo',
  refreshToken: 'demo',
  expiresIn: 3600,
  tokenType: 'Bearer',
};

/** Credentials the "Entrar na demonstração" button submits — accepted by the mock regardless of value. */
export const DEMO_CREDENTIALS = {
  identifier: 'demo@sislab.dev',
  password: 'demo',
};
