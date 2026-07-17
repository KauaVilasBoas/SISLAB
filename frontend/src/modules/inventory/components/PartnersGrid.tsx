import type { ReactNode } from 'react';
import { Building2, Loader2, Mail, Pencil, PowerOff, Power, Users } from 'lucide-react';
import { Card, CardContent } from '@/shared/components/ui/card';
import { Button } from '@/shared/components/ui/button';
import type { ApiError } from '@/shared/types/api';
import type { PagedResult } from '@/shared/types/api';
import { useToast } from '@/shared/components/ui/toast';
import {
  useDeactivatePartner,
  useReactivatePartner,
} from '@/modules/inventory/api/partner.queries';
import { partnerTypePresentation } from '@/modules/inventory/components/partner-presentation';
import type { PartnerListItem } from '@/modules/inventory/partner.types';

interface PartnersGridProps {
  query: {
    data?: PagedResult<PartnerListItem>;
    isLoading: boolean;
    isError: boolean;
  };
  onEdit: (partner: PartnerListItem) => void;
}

/**
 * Partners grid (card [E7] #48). Renders the paginated read rows as cards (building icon + name,
 * contact e-mail, type badge, description) with edit and activate/deactivate actions, and delegates
 * loading/error/empty to standardized states. Inactive partners are visually dimmed. The grid owns no
 * selection state — edit is delegated up; the activate/deactivate mutations invalidate the partner
 * namespace so the grid re-renders.
 */
export function PartnersGrid({ query, onEdit }: PartnersGridProps) {
  if (query.isLoading) {
    return (
      <StateCard>
        <Loader2 className="size-4 animate-spin" />
        Carregando parceiros…
      </StateCard>
    );
  }

  if (query.isError) {
    return <StateCard tone="error">Não foi possível carregar os parceiros.</StateCard>;
  }

  const items = query.data?.items ?? [];

  if (items.length === 0) {
    return (
      <Card>
        <CardContent className="flex flex-col items-center justify-center gap-3 py-16 text-center">
          <Users className="size-8 text-muted-foreground" />
          <p className="text-sm text-muted-foreground">
            Nenhum parceiro encontrado. Ajuste os filtros ou cadastre o primeiro.
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {items.map((partner) => (
        <PartnerCard key={partner.id} partner={partner} onEdit={onEdit} />
      ))}
    </div>
  );
}

function PartnerCard({
  partner,
  onEdit,
}: {
  partner: PartnerListItem;
  onEdit: (partner: PartnerListItem) => void;
}) {
  const toast = useToast();
  const deactivate = useDeactivatePartner(partner.id);
  const reactivate = useReactivatePartner(partner.id);
  const type = partnerTypePresentation(partner.type);
  const pending = deactivate.isPending || reactivate.isPending;

  async function toggleActive() {
    try {
      if (partner.isActive) {
        await deactivate.mutateAsync();
        toast('success', 'Parceiro desativado.');
      } else {
        await reactivate.mutateAsync();
        toast('success', 'Parceiro reativado.');
      }
    } catch (err) {
      toast(
        'error',
        (err as ApiError)?.message ?? 'Não foi possível atualizar o parceiro.',
      );
    }
  }

  return (
    <Card className={partner.isActive ? undefined : 'opacity-60'}>
      <CardContent className="flex h-full flex-col gap-3 p-5">
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 items-center gap-2">
            <Building2 className="size-5 shrink-0 text-muted-foreground" />
            <h3 className="truncate font-semibold">{partner.name}</h3>
          </div>
          <span className={type.className}>{type.label}</span>
        </div>

        {partner.email ? (
          <p className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <Mail className="size-3.5 shrink-0" />
            <span className="truncate">{partner.email}</span>
          </p>
        ) : null}

        {partner.cnpj ? (
          <p className="font-mono text-xs text-muted-foreground">{partner.cnpj}</p>
        ) : null}

        {partner.notes ? (
          <p className="line-clamp-3 text-sm text-muted-foreground">{partner.notes}</p>
        ) : null}

        {partner.isActive ? null : (
          <span className="inline-flex w-fit items-center rounded-full bg-muted px-2.5 py-0.5 text-xs font-semibold text-muted-foreground">
            Inativo
          </span>
        )}

        <div className="mt-auto flex gap-2 pt-2">
          <Button variant="outline" size="sm" onClick={() => onEdit(partner)}>
            <Pencil className="size-3.5" />
            Editar
          </Button>
          <Button variant="outline" size="sm" disabled={pending} onClick={toggleActive}>
            {pending ? (
              <Loader2 className="size-3.5 animate-spin" />
            ) : partner.isActive ? (
              <PowerOff className="size-3.5" />
            ) : (
              <Power className="size-3.5" />
            )}
            {partner.isActive ? 'Desativar' : 'Reativar'}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function StateCard({
  children,
  tone = 'muted',
}: {
  children: ReactNode;
  tone?: 'muted' | 'error';
}) {
  return (
    <Card>
      <CardContent
        className={
          tone === 'error'
            ? 'py-16 text-center text-sm text-destructive'
            : 'flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground'
        }
      >
        {children}
      </CardContent>
    </Card>
  );
}
