import { login as loginRequest } from '@/modules/auth/api/auth.queries';
import { DEMO_CREDENTIALS } from '@/demo/session';

export function signInAsDemoVisitor(): Promise<unknown> {
  return loginRequest(DEMO_CREDENTIALS);
}
