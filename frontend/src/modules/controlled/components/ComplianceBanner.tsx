import { Loader2, ShieldAlert } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';

interface ComplianceBannerProps {
  /** How many controlled items are expired. */
  expiredCount: number;
  /** Total controlled items on the current page/company view. */
  totalCount: number;
  /** Triggers the CSV export of the audit trail. */
  onExportTrail: () => void;
  /** True while the export request is in flight, to disable the button and show a spinner. */
  exporting: boolean;
}

/**
 * Compliance banner of the Controlados screen (card [E7] #62). Red, attention-grabbing header stating
 * how many controlled substances are expired and reminding the operator that controlled drugs require a
 * formal disposal record and that the append-only audit trail is active. Carries the "Exportar trilha"
 * action. Purely presentational — the counts and the export handler are passed in.
 */
export function ComplianceBanner({
  expiredCount,
  totalCount,
  onExportTrail,
  exporting,
}: ComplianceBannerProps) {
  const atRisk = expiredCount > 0;

  return (
    <div
      role={atRisk ? 'alert' : 'status'}
      className={
        atRisk
          ? 'flex flex-col gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4 sm:flex-row sm:items-center sm:justify-between'
          : 'flex flex-col gap-3 rounded-xl border border-emerald-200 bg-emerald-50 p-4 dark:border-emerald-800 dark:bg-emerald-950 sm:flex-row sm:items-center sm:justify-between'
      }
    >
      <div className="flex items-start gap-3">
        <ShieldAlert
          className={
            atRisk
              ? 'mt-0.5 size-5 shrink-0 text-destructive'
              : 'mt-0.5 size-5 shrink-0 text-emerald-600 dark:text-emerald-400'
          }
        />
        <div className="space-y-0.5">
          <p className="text-sm font-semibold">
            {atRisk
              ? `Compliance em risco — ${expiredCount} de ${totalCount} controlados vencidos.`
              : `Compliance em dia — nenhum dos ${totalCount} controlados vencido.`}
          </p>
          <p className="text-xs text-muted-foreground">
            Fármacos controlados exigem registro formal de descarte. Trilha de auditoria append-only
            ativa.
          </p>
        </div>
      </div>

      <Button
        variant="outline"
        size="sm"
        onClick={onExportTrail}
        disabled={exporting}
        className="shrink-0"
      >
        {exporting ? <Loader2 className="size-4 animate-spin" /> : null}
        Exportar trilha
      </Button>
    </div>
  );
}
