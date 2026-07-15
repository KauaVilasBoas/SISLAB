import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Building2, CheckCircle2, FlaskConical, Loader2 } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader } from '@/shared/components/ui/card';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { formatDateTime } from '@/shared/lib/format';
import type { ApiError } from '@/shared/types/api';
import {
  useAcceptInvitation,
  useInvitationPreview,
} from '@/modules/identity/api/invitation.queries';
import { useToast } from '@/shared/components/ui/toast';

/** Full-viewport shell shared by every state of the public invitation page. */
function InvitationShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-[420px]">
        <CardHeader className="items-center gap-2 text-center">
          <div className="flex size-11 items-center justify-center rounded-xl bg-primary text-primary-foreground">
            <FlaskConical className="size-6" aria-hidden />
          </div>
          <div>
            <h1 className="text-lg font-semibold tracking-tight">SISLAB</h1>
            <p className="text-sm text-muted-foreground">Convite para empresa</p>
          </div>
        </CardHeader>
        <CardContent className="flex flex-col gap-6">{children}</CardContent>
      </Card>
    </div>
  );
}

/**
 * Public invitation-acceptance page (card #107), route /invite/:token, outside RequireAuth.
 *
 * 1. Loads the anonymous preview (company, e-mail, profile, expiry).
 * 2. When acceptable, offers accept — collecting username/password only when the invitee has no
 *    account yet (requiresAccountCreation).
 * 3. On success, redirects to /login so the invitee can sign in.
 */
export function InvitationAcceptPage() {
  const { token = '' } = useParams();
  const navigate = useNavigate();
  const preview = useInvitationPreview(token);
  const accept = useAcceptInvitation();
  const toast = useToast();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  async function submitAccept(withAccount: boolean) {
    if (withAccount && password !== confirmPassword) {
      toast('error', 'As senhas não coincidem.');
      return;
    }
    try {
      await accept.mutateAsync(
        withAccount ? { token, username: username.trim(), password } : { token },
      );
      navigate('/login', { replace: true });
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível aceitar o convite.');
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void submitAccept(preview.data?.requiresAccountCreation ?? false);
  }

  if (preview.isLoading) {
    return (
      <InvitationShell>
        <p className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" />
          Carregando convite…
        </p>
      </InvitationShell>
    );
  }

  if (preview.isError || !preview.data) {
    return (
      <InvitationShell>
        <p className="text-center text-sm text-destructive" role="alert">
          Convite não encontrado ou inválido.
        </p>
        <Button asChild variant="outline" className="w-full">
          <Link to="/login">Ir para o login</Link>
        </Button>
      </InvitationShell>
    );
  }

  const invitation = preview.data;

  return (
    <InvitationShell>
      <div className="space-y-3 rounded-lg border bg-muted/40 p-4 text-sm">
        <div className="flex items-center gap-2 font-medium">
          <Building2 className="size-4 text-muted-foreground" />
          {invitation.companyName}
        </div>
        <dl className="grid grid-cols-3 gap-1 text-muted-foreground">
          <dt>E-mail</dt>
          <dd className="col-span-2 text-foreground">{invitation.email}</dd>
          <dt>Perfil</dt>
          <dd className="col-span-2 text-foreground">{invitation.profileName}</dd>
          <dt>Expira em</dt>
          <dd className="col-span-2 text-foreground">
            {formatDateTime(invitation.expiresAt)}
          </dd>
        </dl>
      </div>

      {!invitation.acceptable ? (
        <>
          <p className="text-center text-sm text-muted-foreground">
            {invitation.status === 'Accepted'
              ? 'Este convite já foi aceito.'
              : invitation.status === 'Expired'
                ? 'Este convite expirou.'
                : 'Este convite não está mais disponível.'}
          </p>
          <Button asChild variant="outline" className="w-full">
            <Link to="/login">Ir para o login</Link>
          </Button>
        </>
      ) : invitation.requiresAccountCreation ? (
        <form className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
          <p className="text-sm text-muted-foreground">
            Crie sua conta para aceitar o convite.
          </p>

          <div className="flex flex-col gap-2">
            <Label htmlFor="invite-username">Nome de usuário</Label>
            <Input
              id="invite-username"
              autoComplete="username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
              autoFocus
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="invite-password">Senha</Label>
            <Input
              id="invite-password"
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="invite-confirm">Confirmar senha</Label>
            <Input
              id="invite-confirm"
              type="password"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
            />
          </div>

          <Button type="submit" className="w-full" disabled={accept.isPending}>
            {accept.isPending && <Loader2 className="size-4 animate-spin" />}
            Criar conta e aceitar
          </Button>
        </form>
      ) : (
        <>
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            <CheckCircle2 className="size-4 text-emerald-600" />
            Sua conta já existe — basta aceitar para entrar na empresa.
          </p>

          <Button
            className="w-full"
            disabled={accept.isPending}
            onClick={() => void submitAccept(false)}
          >
            {accept.isPending && <Loader2 className="size-4 animate-spin" />}
            Aceitar convite
          </Button>
        </>
      )}
    </InvitationShell>
  );
}
