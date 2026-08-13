import { Navigate } from 'react-router-dom';
import { useAuth } from '@/modules/auth/AuthProvider';
import { LoginPage } from '@/modules/auth/pages/LoginPage';
import { IS_DEMO } from '@/demo/isDemo';

/**
 * Entry element of the public /login route.
 *
 * With a real backend it is just the login screen. In the public demo the visitor is signed in
 * automatically during bootstrap, so the form is a dead end that reads as "you need an account": while the
 * session is still resolving — or once it resolved — /login bounces to the dashboard instead.
 *
 * The one state that still renders the page is a FAILED auto sign-in, where redirecting would trade a dead
 * end for an infinite bounce; the page's "Entrar na demonstração" button is then the manual retry.
 */
export function LoginRoute() {
  const { status } = useAuth();

  if (IS_DEMO && status !== 'unauthenticated') return <Navigate to="/" replace />;

  return <LoginPage />;
}
