import { useMemo, useState } from 'react';
import { Search } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Badge } from '@/shared/components/ui/badge';
import { useToast } from '@/shared/components/ui/toast';
import type { ApiError } from '@/shared/types/api';
import {
  downloadControlledAuditTrailCsv,
  useControlledAuditTrail,
  useControlledItems,
} from '@/modules/controlled/api/controlled.queries';
import { ComplianceBanner } from '@/modules/controlled/components/ComplianceBanner';
import { ControlledTable } from '@/modules/controlled/components/ControlledTable';
import { AuditTrail } from '@/modules/controlled/components/AuditTrail';
import { ConferenceModal } from '@/modules/controlled/components/ConferenceModal';
import type { ControlledItem } from '@/modules/controlled/types';

/**
 * Controlados compliance screen (card [E7] #62). The most sensitive laboratory operation: a dedicated
 * view of controlled substances with per-bottle balance, colored validity, container state, an
 * append-only audit trail and the periodic conference (physical count).
 *
 * It reuses read models the backend already exposes — the inventory listing narrowed to controlled
 * items and the Audit module trail (card #57) — and adds the conference write (RegisterStockCount),
 * which records the counted balance and its divergence WITHOUT changing the on-hand quantity. Owns the
 * page/selection/search state; the list and trail each paginate independently. Every failure is a toast
 * (no inline error text); the conference lives in a modal.
 */
export function ControlledPage() {
  const toast = useToast();

  const [search, setSearch] = useState('');
  const [itemsPage, setItemsPage] = useState(1);
  const [trailPage, setTrailPage] = useState(1);
  const [auditItem, setAuditItem] = useState<ControlledItem | null>(null);
  const [conferencing, setConferencing] = useState<ControlledItem | null>(null);
  const [exporting, setExporting] = useState(false);

  const itemsQuery = useControlledItems({ search: search || undefined }, itemsPage);
  const trailQuery = useControlledAuditTrail(auditItem?.id, trailPage);

  const items = useMemo(() => itemsQuery.data?.items ?? [], [itemsQuery.data]);

  // Resolve an audited item id to its name for the trail; falls back to a short id when the item is not
  // on the current controlled page (e.g. filtered out or deactivated).
  const itemNameById = useMemo(() => {
    const byId = new Map(items.map((item) => [item.id, item.name]));
    return (id: string) => byId.get(id) ?? `#${id.slice(0, 8)}`;
  }, [items]);

  const expiredCount = items.filter((item) => item.expiryStatus === 'Expired').length;

  const itemsTotalPages = itemsQuery.data?.totalPages ?? 0;
  const itemsTotalCount = itemsQuery.data?.totalCount ?? 0;
  const trailTotalPages = trailQuery.data?.totalPages ?? 0;
  const trailTotalCount = trailQuery.data?.totalCount ?? 0;

  function patchSearch(value: string) {
    setSearch(value);
    setItemsPage(1);
  }

  function auditByItem(item: ControlledItem) {
    setAuditItem(item);
    setTrailPage(1);
  }

  function clearAuditFilter() {
    setAuditItem(null);
    setTrailPage(1);
  }

  async function handleExport() {
    setExporting(true);
    try {
      await downloadControlledAuditTrailCsv(auditItem?.id);
      toast('success', 'Trilha exportada.');
    } catch (err) {
      toast('error', (err as ApiError)?.message ?? 'Não foi possível exportar a trilha.');
    } finally {
      setExporting(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Controlados"
        description="Fármacos controlados: saldo por frasco, validade, trilha de auditoria e compliance."
      />

      <ComplianceBanner
        expiredCount={expiredCount}
        totalCount={itemsTotalCount}
        onExportTrail={handleExport}
        exporting={exporting}
      />

      <section className="space-y-3">
        <div className="flex items-center justify-between gap-4">
          <h2 className="text-sm font-semibold">Fármacos controlados</h2>
          <div className="relative w-full max-w-xs">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              type="search"
              value={search}
              onChange={(e) => patchSearch(e.target.value)}
              placeholder="Buscar por nome, lote ou marca"
              aria-label="Buscar controlados"
              className="pl-8"
            />
          </div>
        </div>

        <ControlledTable query={itemsQuery} onConference={setConferencing} onAudit={auditByItem} />

        <Pagination
          page={itemsPage}
          totalPages={itemsTotalPages}
          totalCount={itemsTotalCount}
          singular="controlado"
          plural="controlados"
          fetching={itemsQuery.isFetching}
          onPrev={() => setItemsPage((p) => Math.max(1, p - 1))}
          onNext={() => setItemsPage((p) => Math.min(itemsTotalPages, p + 1))}
        />
      </section>

      <section className="space-y-3">
        <div className="flex flex-col gap-1">
          <h2 className="text-sm font-semibold">
            Trilha de auditoria
            <Badge variant="muted" className="ml-2 align-middle">
              append-only
            </Badge>
          </h2>
          <p className="text-xs text-muted-foreground">
            Consumos, descartes e conferências de fármacos controlados, do mais recente ao mais antigo.
          </p>
          {auditItem ? (
            <div className="mt-1 flex items-center gap-2">
              <Badge variant="secondary">Filtrando por {auditItem.name}</Badge>
              <button
                type="button"
                onClick={clearAuditFilter}
                className="text-xs text-muted-foreground underline-offset-2 hover:underline"
              >
                limpar
              </button>
            </div>
          ) : null}
        </div>

        <AuditTrail query={trailQuery} itemNameById={itemNameById} />

        <Pagination
          page={trailPage}
          totalPages={trailTotalPages}
          totalCount={trailTotalCount}
          singular="registro"
          plural="registros"
          fetching={trailQuery.isFetching}
          onPrev={() => setTrailPage((p) => Math.max(1, p - 1))}
          onNext={() => setTrailPage((p) => Math.min(trailTotalPages, p + 1))}
        />
      </section>

      {conferencing ? (
        <ConferenceModal item={conferencing} onClose={() => setConferencing(null)} />
      ) : null}
    </div>
  );
}

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  singular: string;
  plural: string;
  fetching: boolean;
  onPrev: () => void;
  onNext: () => void;
}

/** Shared prev/next pager used by both the controlled list and the audit trail. */
function Pagination({
  page,
  totalPages,
  totalCount,
  singular,
  plural,
  fetching,
  onPrev,
  onNext,
}: PaginationProps) {
  if (totalPages <= 1) return null;
  return (
    <div className="flex items-center justify-between gap-4 text-sm text-muted-foreground">
      <span>
        {totalCount} {totalCount === 1 ? singular : plural} · página {page} de {totalPages}
      </span>
      <div className="flex gap-2">
        <Button variant="outline" size="sm" disabled={page <= 1 || fetching} onClick={onPrev}>
          Anterior
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={page >= totalPages || fetching}
          onClick={onNext}
        >
          Próxima
        </Button>
      </div>
    </div>
  );
}
