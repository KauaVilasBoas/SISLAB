import {
  Boxes,
  CalendarRange,
  Code2,
  PlayCircle,
  ShieldCheck,
  type LucideIcon,
} from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Modal } from '@/shared/components/ui/modal';

/**
 * Boas-vindas da demo pública, orientada a valor e na língua de um dono de laboratório. Curto de propósito:
 * diz o que o SISLAB resolve, garante que nada é gravado e oferece o tour guiado. O conteúdo técnico virou
 * opt-in (HowItsBuiltDialog), acessível pelo link discreto "Como foi construído" e pela DemoRibbon.
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
      title="Chega de controlar o laboratório em planilhas"
      description="O SISLAB reúne estoque, vencimentos, controlados, equipamentos e agenda num lugar só."
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            Explorar o sistema
          </Button>
          <Button onClick={onStartTour}>
            <PlayCircle aria-hidden />
            Fazer o tour
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        <p className="text-sm leading-relaxed text-muted-foreground">
          Planilhas se perdem, ninguém sabe o que está para vencer e o controlado vira dor
          de cabeça na fiscalização. O SISLAB coloca ordem no dia a dia do laboratório,
          com rastreabilidade de ponta a ponta.
        </p>

        <ul className="grid gap-2 sm:grid-cols-2">
          <ValueItem icon={Boxes} label="Estoque e vencimentos sob controle" />
          <ValueItem icon={ShieldCheck} label="Controlados prontos para auditoria" />
          <ValueItem icon={CalendarRange} label="Equipamentos e agenda num só lugar" />
          <ValueItem icon={PlayCircle} label="Alertas do que precisa de atenção" />
        </ul>

        <p className="rounded-lg border border-dashed bg-muted/40 px-3 py-2.5 text-xs leading-relaxed text-muted-foreground">
          Esta é uma vitrine somente leitura, com dados fictícios: explore à vontade, nada
          é gravado e nenhum dado real de laboratório é exposto.
        </p>

        <button
          type="button"
          onClick={onOpenHowItsBuilt}
          className="inline-flex items-center gap-1.5 text-xs font-medium text-muted-foreground underline-offset-4 transition-colors hover:text-foreground hover:underline"
        >
          <Code2 className="size-3.5" aria-hidden />
          Você é dev ou recrutador? Veja como foi construído
        </button>
      </div>
    </Modal>
  );
}

function ValueItem({ icon: Icon, label }: { icon: LucideIcon; label: string }) {
  return (
    <li className="flex items-center gap-2.5 rounded-lg border bg-muted/40 px-3 py-2.5">
      <Icon className="size-4 shrink-0 text-primary" aria-hidden />
      <span className="text-[13px] font-medium leading-tight">{label}</span>
    </li>
  );
}
