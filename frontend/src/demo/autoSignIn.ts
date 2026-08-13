import { login as loginRequest } from '@/modules/auth/api/auth.queries';
import { DEMO_CREDENTIALS } from '@/demo/session';

/**
 * Signs the visitor in as the fictional demo account.
 *
 * Nobody is ever issued credentials for the public demo, so a login form is a dead end: visitors were
 * stopping at /login assuming they needed an account. The AuthProvider bootstrap calls this BEFORE
 * GET /api/me (demo build only), so the mock already reports a session and the regular bootstrap path
 * lands straight on the dashboard — no separate "logged in" code path to keep in sync.
 *
 * Idempotent: the mock accepts the credentials whatever the current session state is.
 */
export function signInAsDemoVisitor(): Promise<unknown> {
  return loginRequest(DEMO_CREDENTIALS);
}
