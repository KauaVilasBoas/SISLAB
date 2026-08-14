import { useEffect, useState } from 'react';
import {
  ArrowLeft,
  ArrowRight,
  Boxes,
  CalendarRange,
  Check,
  LayoutDashboard,
  ShieldCheck,
  X,
  type LucideIcon,
} from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';
import { router } from '@/app/router';

interface TourStop {
  path: string;
  icon: LucideIcon;
  title: string;
  /** A curta legenda de valor, na língua do dono de laboratório. */
  legend: string;
}

/**
 * O roteiro do tour: paradas em telas REAIS da demo, cada uma com uma legenda curta que explica o valor
 * daquela tela para quem toca o laboratório no dia a dia. É um tour de "paradas": cada passo navega até a
 * rota e mostra a legenda flutuante. A estrutura já deixa espaço para, no futuro, destacar um elemento
 * específico da tela (basta adicionar um seletor por parada).
 */
const TOUR_STOPS: TourStop[] = [
  {
    path: '/',
    icon: LayoutDashboard,
    title: 'Visão geral do laboratório',
    legend:
      'A primeira tela reúne o que precisa de atenção agora: itens a vencer, estoque baixo e alertas. O que antes vivia espalhado em várias planilhas cabe aqui, num relance.',
  },
  {
    path: '/inventory',
    icon: Boxes,
    title: 'Estoque sempre em dia',
    legend:
      'Cada item com seu saldo, lote e validade. Registre entradas e consumos em segundos e saiba exatamente o que tem, onde está e quando vence.',
  },
  {
    path: '/controlled',
    icon: ShieldCheck,
    title: 'Controlados com rastreabilidade',
    legend:
      'Materiais controlados exigem prestação de contas. Aqui o saldo é por frasco, com o histórico de cada uso pronto para a fiscalização.',
  },
  {
    path: '/agenda/schedule',
    icon: CalendarRange,
    title: 'Agenda do laboratório',
    legend:
      'Salas, equipamentos e compromissos num só calendário. Menos conflito de reserva, mais previsibilidade para a equipe.',
  },
];

export function DemoTour({ onFinish }: { onFinish: () => void }) {
  const [index, setIndex] = useState(0);

  const stop = TOUR_STOPS[index];
  const isFirst = index === 0;
  const isLast = index === TOUR_STOPS.length - 1;

  // Cada parada roteia para a tela real; a legenda flutuante acompanha a navegação.
  // Usamos a API imperativa do router (singleton): o tour vive fora do RouterProvider,
  // então o hook useNavigate() não está disponível aqui.
  useEffect(() => {
    void router.navigate(stop.path);
  }, [stop.path]);

  const Icon = stop.icon;

  return (
    <div className="pointer-events-none fixed inset-0 z-40">
      <div className="pointer-events-auto absolute inset-x-0 bottom-4 mx-auto w-[min(92vw,32rem)] rounded-xl border bg-card text-card-foreground shadow-lg sm:bottom-6">
        <div className="flex items-start gap-3 border-b p-4">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <Icon className="size-5" aria-hidden />
          </span>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold leading-tight">{stop.title}</p>
            <p className="mt-1 text-[13px] leading-snug text-muted-foreground">
              {stop.legend}
            </p>
          </div>
          <button
            type="button"
            onClick={onFinish}
            aria-label="Pular o tour"
            className="rounded-md p-1 text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
          >
            <X className="size-4" />
          </button>
        </div>

        <div className="flex items-center justify-between gap-3 p-3">
          <div className="flex items-center gap-2" aria-hidden>
            {TOUR_STOPS.map((_, i) => (
              <span
                key={i}
                className={cn(
                  'h-1.5 rounded-full transition-all',
                  i === index ? 'w-5 bg-primary' : 'w-1.5 bg-muted-foreground/30',
                )}
              />
            ))}
            <span className="ml-1 text-xs text-muted-foreground">
              {index + 1} de {TOUR_STOPS.length}
            </span>
          </div>

          <div className="flex items-center gap-2">
            {isFirst ? (
              <Button variant="ghost" size="sm" onClick={onFinish}>
                Pular
              </Button>
            ) : (
              <Button
                variant="outline"
                size="sm"
                onClick={() => setIndex((i) => Math.max(0, i - 1))}
              >
                <ArrowLeft aria-hidden />
                Anterior
              </Button>
            )}
            {isLast ? (
              <Button size="sm" onClick={onFinish}>
                <Check aria-hidden />
                Concluir
              </Button>
            ) : (
              <Button
                size="sm"
                onClick={() => setIndex((i) => Math.min(TOUR_STOPS.length - 1, i + 1))}
              >
                Próximo
                <ArrowRight aria-hidden />
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
