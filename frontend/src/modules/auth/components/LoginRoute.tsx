import { Navigate } from 'react-router-dom';
import { useAuth } from '@/modules/auth/AuthProvider';
import { LoginPage } from '@/modules/auth/pages/LoginPage';
import { IS_DEMO } from '@/demo/isDemo';

export function LoginRoute() {
  const { status } = useAuth();

  if (IS_DEMO && status !== 'unauthenticated') return <Navigate to="/" replace />;

  return <LoginPage />;
}
