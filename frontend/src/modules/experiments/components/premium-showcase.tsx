import { PremiumModuleGate } from '@/shared/components/PremiumModuleGate';
import {
  ShowcaseShell,
  SkelHeader,
  SkelTable,
} from '@/shared/components/premium-showcase-skeletons';

/**
 * Premium showcases for the Experiments module family (in vitro, in vivo, biobank, pendencies).
 *
 * Each screen reserved for the Premium edition renders a PremiumModuleGate whose backdrop is a static,
 * side-effect-free mock of that module's real layout — a faithful teaser, blurred behind the glass,
 * with no network fetch or empty/loading states leaking through. The serial-dilution calculator is NOT
 * here: it stays free and fully navigable.
 */

/** In vitro — cell viability / nitric oxide assays. */
function InVitroPreview() {
  return (
    <ShowcaseShell>
      <SkelHeader
        title="Experimentos"
        subtitle="Ensaios in vitro — desenho da placa e cálculo"
      />
      <div className="flex gap-3">
        <div className="h-16 w-48 rounded-lg border bg-card" />
        <div className="h-16 w-48 rounded-lg border bg-card" />
      </div>
      <SkelTable
        columns={['Título', 'Tipo', 'Status', 'Calculado', 'Criado em', 'Por']}
        rows={6}
      />
    </ShowcaseShell>
  );
}

/** In vivo — experimental design tree (project → batch → group → animal). */
function InVivoPreview() {
  return (
    <ShowcaseShell>
      <SkelHeader
        title="Projetos in vivo"
        subtitle="Delineamento experimental — Projeto, Leva, Grupo, Animal"
      />
      <SkelTable columns={['Projeto', 'Status', 'Levas', 'Animais', 'Início']} rows={6} />
    </ShowcaseShell>
  );
}

/** Biobank — 96-well plate grid + samples list. */
function BiobankPreview() {
  return (
    <ShowcaseShell>
      <SkelHeader
        title="Biobanco"
        subtitle="Amostras coletadas — saldo derivado e análises"
      />
      <div className="grid grid-cols-3 gap-4 sm:grid-cols-6">
        {Array.from({ length: 24 }).map((_, i) => (
          <div key={i} className="aspect-square rounded-md border bg-status-info/15" />
        ))}
      </div>
      <SkelTable
        columns={['Código', 'Tipo', 'Saldo', 'Análises', 'Coletado em']}
        rows={4}
      />
    </ShowcaseShell>
  );
}

/** Pendencies — summary cards + open-work list. */
function PendenciesPreview() {
  return (
    <ShowcaseShell>
      <SkelHeader
        title="Pendências"
        subtitle="Trabalho em aberto — cálculos, etapas e análises"
      />
      <div className="grid gap-4 sm:grid-cols-3">
        {['Aguardando cálculo', 'Etapas pendentes', 'Amostras sem análise'].map(
          (label) => (
            <div key={label} className="rounded-lg border bg-card p-5">
              <div className="text-xs text-muted-foreground">{label}</div>
              <div className="mt-3 h-8 w-12 rounded bg-muted" />
            </div>
          ),
        )}
      </div>
      <SkelTable columns={['Item', 'Tipo', 'Referência', 'Desde']} rows={5} />
    </ShowcaseShell>
  );
}

/** In vitro experiments — list + detail. */
export function InVitroShowcase() {
  return (
    <PremiumModuleGate
      title="In vitro"
      pitch="Desenhe a placa, importe a leitura do equipamento e deixe o SISLAB calcular a viabilidade celular e o óxido nítrico — do rascunho ao relatório exportável."
      bullets={[
        'Editor visual de placa de 96 poços com papéis e réplicas',
        'Importação da leitura e cálculo automático de resultados',
        'Exclusão de outliers e exportação do laudo',
      ]}
      preview={<InVitroPreview />}
    />
  );
}

/** In vivo projects — list + delineation detail. */
export function InVivoShowcase() {
  return (
    <PremiumModuleGate
      title="In vivo"
      pitch="Estruture o delineamento experimental completo — Projeto → Leva → Grupo (dose) → Animal — com rastreabilidade de ponta a ponta e ensaios comportamentais."
      bullets={[
        'Árvore de delineamento com grupos por dose',
        'Ensaios comportamentais e etapas do protocolo',
        'Rastreabilidade por animal e por leva',
      ]}
      preview={<InVivoPreview />}
    />
  );
}

/** Biobank — samples and analyses. */
export function BiobankShowcase() {
  return (
    <PremiumModuleGate
      title="Biobanco"
      pitch="Gerencie o acervo de amostras coletadas nos estudos in vivo com saldo derivado a cada análise e histórico completo de uso."
      bullets={[
        'Inventário de amostras com saldo calculado',
        'Registro de análises e consumo por amostra',
        'Rastreabilidade da coleta ao resultado',
      ]}
      preview={<BiobankPreview />}
    />
  );
}

/** Pendencies — the operator's open work. */
export function PendenciesShowcase() {
  return (
    <PremiumModuleGate
      title="Pendências"
      pitch="Um painel único do trabalho em aberto do laboratório — experimentos aguardando cálculo, etapas não realizadas e amostras sem análise — para nada passar despercebido."
      bullets={[
        'Visão consolidada de tudo que está pendente',
        'Atalhos diretos para o item que precisa de ação',
        'Menos erro humano, mais rastreabilidade',
      ]}
      preview={<PendenciesPreview />}
    />
  );
}
