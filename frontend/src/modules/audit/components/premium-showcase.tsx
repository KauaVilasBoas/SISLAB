import { PremiumModuleGate } from '@/shared/components/PremiumModuleGate';
import {
  ShowcaseShell,
  SkelHeader,
  SkelTable,
} from '@/shared/components/premium-showcase-skeletons';

/**
 * Premium showcase for the Audit module — the append-only action trail.
 *
 * Reserved for the Premium edition: the /audit route renders this immersive PremiumModuleGate instead
 * of the real screen, so reaching it by clicking the sidebar or typing the URL lands on the vitrine
 * (defense-in-depth, same spirit as the Experiments showcases).
 */

/** Audit trail — a filter row over the append-only entries table (date · actor · action · entity). */
function AuditPreview() {
  return (
    <ShowcaseShell>
      <SkelHeader
        title="Auditoria"
        subtitle="Trilha append-only das ações da empresa — com exportação CSV"
      />
      <div className="flex flex-wrap items-center gap-3">
        <div className="h-9 w-44 rounded-md border bg-card" />
        <div className="h-9 w-36 rounded-md border bg-card" />
        <div className="ml-auto h-9 w-32 rounded-md bg-primary/70" />
      </div>
      <SkelTable columns={['Data', 'Autor', 'Ação', 'Entidade', 'Detalhes']} rows={7} />
    </ShowcaseShell>
  );
}

/** Audit trail — append-only action log with CSV export. */
export function AuditShowcase() {
  return (
    <PremiumModuleGate
      title="Auditoria"
      pitch="Cada ação registrada de forma imutável — quem fez, o quê, em qual entidade e quando — com exportação em CSV para conformidade e rastreabilidade total."
      bullets={[
        'Trilha append-only, à prova de adulteração',
        'Filtro por autor, entidade e período',
        'Exportação em CSV para auditorias e compliance',
      ]}
      preview={<AuditPreview />}
    />
  );
}
