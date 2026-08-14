import {
  Boxes,
  CalendarRange,
  Code2,
  PlayCircle,
  ShieldCheck,
  TriangleAlert,
  type LucideIcon,
} from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Modal } from '@/shared/components/ui/modal';

/**
 * Boas-vindas da demo pública: um convite curto e impactante, na língua de um dono de laboratório. Diz o
 * que o SISLAB resolve num headline forte, mostra os pilares como chips visuais e oferece o tour guiado.
 * O conteúdo técnico é opt-in (HowItsBuiltDialog), acessível pelo link discreto "Como foi construído".
 */
export function DemoIntroDialog({
  onStartTour,
  onOpenHowItsBuilt,
  onClose,
}: {
  onStartTour: () => void;
  onOpenHowItsBuilt: () => void;
  onClose: () => void;
}) {
  return (
    <Modal
      open
      onClose={onClose}
      title="Adeus, planilhas."
      description="Estoque, validades, controlados, equipamentos e agenda do laboratório num lugar só."
      footer={
        <div className="flex w-full items-center justify-between gap-3">
          <button
            type="button"
            onClick={onOpenHowItsBuilt}
            className="inline-flex items-center gap-1.5 text-xs font-medium text-muted-foreground underline-offset-4 transition-colors hover:text-foreground hover:underline"
          >
            <Code2 className="size-3.5" aria-hidden />
            Como foi construído
          </button>
          <div className="flex items-center gap-2">
            <Button variant="outline" onClick={onClose}>
              Explorar
            </Button>
            <Button onClick={onStartTour}>
              <PlayCircle aria-hidden />
              Fazer o tour
            </Button>
          </div>
        </div>
      }
    >
      <div className="space-y-4">
        <ul className="grid grid-cols-2 gap-2">
          <ValueChip icon={Boxes} label="Estoque e validades" />
          <ValueChip icon={ShieldCheck} label="Controlados" />
          <ValueChip icon={CalendarRange} label="Agenda" />
          <ValueChip icon={TriangleAlert} label="Alertas" />
        </ul>

        <p className="text-xs text-muted-foreground">
          Vitrine somente leitura, com dados fictícios. Nada é gravado.
        </p>
      </div>
    </Modal>
  );
}

function ValueChip({ icon: Icon, label }: { icon: LucideIcon; label: string }) {
  return (
    <li className="flex items-center gap-2 rounded-lg border bg-muted/40 px-3 py-2.5">
      <Icon className="size-4 shrink-0 text-primary" aria-hidden />
      <span className="text-[13px] font-medium leading-tight">{label}</span>
    </li>
  );
}
