/**
 * Auth module contracts — mirror the backend response shapes.
 *
 * These endpoints (Lumen auth + active-company) return RAW bodies (no ApiResult envelope),
 * so the shapes here match the JSON exactly.
 */

/** Profile summary carried by GET /api/me (Lumen GetCurrentUserResult.Profiles). */
export interface UserProfileSummary {
  id: string;
  name: string;
}

/** Authenticated user — GET /api/me (Lumen GetCurrentUserResult). */
export interface CurrentUser {
  id: string;
  email: string;
  username: string;
  createdAt: string;
  lastLoginAt: string | null;
  emailConfirmedAt: string | null;
  profiles: UserProfileSummary[];
}

/** POST /api/auth/login request body — identifier is the e-mail (or username). */
export interface LoginRequest {
  identifier: string;
  password: string;
}

/** POST /api/auth/login response (Lumen LoginResult). The cookie is the browser's source of truth. */
export interface LoginResult {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  tokenType: string;
}

/** GET /api/companies/mine item (CompanyMembershipDto). */
export interface CompanyMembership {
  id: string;
  name: string;
}

/** GET /api/companies/active (ActiveCompanyDto). */
export interface ActiveCompany {
  companyId: string;
}
