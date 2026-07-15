import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate, type Location } from 'react-router-dom';
import { FlaskConical, Loader2 } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import {
  Card,
  CardContent,
  CardHeader,
} from '@/shared/components/ui/card';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import type { ApiError } from '@/shared/types/api';
import { useAuth, type LoginOutcome } from '@/modules/auth/AuthProvider';
import { CompanySelector } from '@/modules/auth/components/CompanySelector';
import type { CompanyMembership } from '@/modules/auth/types';

type LocationState = { from?: Location } | null;

/**
 * Login mother screen (public route /login) — card [E7] #44.
 *
 * Fetches nothing on mount; it drives the login flow through the AuthProvider:
 *  1. submit e-mail + password → POST /api/auth/login (backend sets httpOnly cookies);
 *  2. resolve the active company (auto when the user has one, picker when several);
 *  3. navigate to the originally requested route (or the dashboard).
 */
export function LoginPage() {
  const { login, selectCompany } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [companies, setCompanies] = useState<CompanyMembership[] | null>(null);

  const redirectTo = (location.state as LocationState)?.from?.pathname ?? '/';

  function messageFor(err: unknown, fallback: string): string {
    const apiError = err as ApiError;
    if (apiError?.status === 401) return 'E-mail ou senha inválidos.';
    return apiError?.message || fallback;
  }

  function finish(outcome: LoginOutcome) {
    if (outcome.kind === 'active') {
      navigate(redirectTo, { replace: true });
      return;
    }
    if (outcome.kind === 'select') {
      setCompanies(outcome.companies);
      return;
    }
    // 'no-company' — the account is not linked to any company yet.
    setError(
      'Sua conta ainda não está vinculada a nenhuma empresa. Contate o coordenador do laboratório.',
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      finish(await login(identifier, password));
    } catch (err) {
      setError(messageFor(err, 'Não foi possível entrar. Tente novamente.'));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleSelectCompany(companyId: string) {
    setError(null);
    try {
      await selectCompany(companyId);
      navigate(redirectTo, { replace: true });
    } catch (err) {
      setError(messageFor(err, 'Não foi possível ativar a empresa selecionada.'));
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-[380px]">
        <CardHeader className="items-center gap-2 text-center">
          <div className="flex size-11 items-center justify-center rounded-xl bg-primary text-primary-foreground">
            <FlaskConical className="size-6" aria-hidden />
          </div>
          <div>
            <h1 className="text-lg font-semibold tracking-tight">SISLAB</h1>
            <p className="text-sm text-muted-foreground">Gestão de laboratório</p>
          </div>
        </CardHeader>

        <CardContent className="flex flex-col gap-6">
          {companies ? (
            <CompanySelector
              companies={companies}
              onSelect={handleSelectCompany}
              error={error}
            />
          ) : (
            <form className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
              <div className="flex flex-col gap-2">
                <Label htmlFor="identifier">E-mail</Label>
                <Input
                  id="identifier"
                  type="email"
                  autoComplete="username"
                  placeholder="voce@ufba.br"
                  value={identifier}
                  onChange={(e) => setIdentifier(e.target.value)}
                  required
                  autoFocus
                />
              </div>

              <div className="flex flex-col gap-2">
                <Label htmlFor="password">Senha</Label>
                <Input
                  id="password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                />
              </div>

              {error && (
                <p className="text-sm text-destructive" role="alert">
                  {error}
                </p>
              )}

              <Button type="submit" className="w-full" disabled={submitting}>
                {submitting && <Loader2 className="size-4 animate-spin" aria-hidden />}
                Entrar
              </Button>
            </form>
          )}
        </CardContent>
      </Card>

      <p className="fixed inset-x-0 bottom-4 text-center text-xs text-muted-foreground">
        Sessão via cookie httpOnly · multi-empresa
      </p>
    </div>
  );
}
