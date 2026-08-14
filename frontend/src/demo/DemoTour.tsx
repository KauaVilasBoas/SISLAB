import { useEffect, useLayoutEffect, useState } from 'react';
import {
  ArrowLeft,
  ArrowRight,
  Boxes,
  CalendarRange,
  Check,
  Compass,
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
  /**
   * Seletor do elemento a destacar naquela parada (coach-mark/spotlight). Aponta para uma âncora estável
   * da tela, hoje o item da sidebar da rota. Ausente (ou não encontrado em runtime) cai no fallback: scrim
   * suave sem recorte, com o anel de destaque na viewport inteira.
   */
  spotlight?: string;
}

/**
 * O roteiro do tour: paradas em telas REAIS da demo, cada uma com uma legenda curta que explica o valor
 * daquela tela para quem toca o laboratório no dia a dia. Cada passo navega até a rota, ilumina um alvo
 * (coach-mark) e mostra o card guiado. O alvo padrão é o item de navegação da rota, sempre presente e
 * estável; trocar o seletor por um elemento interno da tela é só ajustar "spotlight" aqui.
 */
const TOUR_STOPS: TourStop[] = [
  {
    path: '/',
    icon: LayoutDashboard,
    title: 'Visão geral do laboratório',
    legend:
      'A primeira tela reúne o que precisa de atenção agora: itens a vencer, estoque baixo e alertas. O que antes vivia espalhado em várias planilhas cabe aqui, num relance.',
    spotlight: '[data-tour="nav:/"]',
  },
  {
    path: '/inventory',
    icon: Boxes,
    title: 'Estoque sempre em dia',
    legend:
      'Cada item com seu saldo, lote e validade. Registre entradas e consumos em segundos e saiba exatamente o que tem, onde está e quando vence.',
    spotlight: '[data-tour="nav:/inventory"]',
  },
  {
    path: '/controlled',
    icon: ShieldCheck,
    title: 'Controlados com rastreabilidade',
    legend:
      'Materiais controlados exigem prestação de contas. Aqui o saldo é por frasco, com o histórico de cada uso pronto para a fiscalização.',
    spotlight: '[data-tour="nav:/controlled"]',
  },
  {
    path: '/agenda/schedule',
    icon: CalendarRange,
    title: 'Agenda do laboratório',
    legend:
      'Salas, equipamentos e compromissos num só calendário. Menos conflito de reserva, mais previsibilidade para a equipe.',
    spotlight: '[data-tour="nav:/agenda/schedule"]',
  },
];

/** Retângulo do alvo do spotlight, em coordenadas de viewport, já com a folga (padding) aplicada. */
interface SpotlightRect {
  top: number;
  left: number;
  width: number;
  height: number;
}

const SPOTLIGHT_PADDING = 8;

/**
 * Mede o elemento-alvo da parada e mantém o retângulo do recorte em dia enquanto a parada estiver ativa.
 * A navegação e a montagem da tela são assíncronas, então tentamos algumas vezes por frames até o alvo
 * existir; depois reagimos a resize/scroll. Some o retângulo (null) quando não há alvo: o overlay então
 * usa o fallback de scrim uniforme.
 */
function useSpotlightRect(
  selector: string | undefined,
  stopKey: number,
): SpotlightRect | null {
  const [rect, setRect] = useState<SpotlightRect | null>(null);

  useLayoutEffect(() => {
    if (!selector) {
      setRect(null);
      return;
    }

    let frame = 0;
    let attempts = 0;

    const measure = () => {
      const el = document.querySelector<HTMLElement>(selector);
      if (el) {
        const r = el.getBoundingClientRect();
        if (r.width > 0 && r.height > 0) {
          setRect({
            top: r.top - SPOTLIGHT_PADDING,
            left: r.left - SPOTLIGHT_PADDING,
            width: r.width + SPOTLIGHT_PADDING * 2,
            height: r.height + SPOTLIGHT_PADDING * 2,
          });
          return;
        }
      }
      // A tela ainda não montou (ou é mobile, sem sidebar): tenta por alguns frames e então desiste
      // graciosamente para o fallback de scrim uniforme.
      if (attempts < 20) {
        attempts += 1;
        frame = requestAnimationFrame(measure);
      } else {
        setRect(null);
      }
    };

    frame = requestAnimationFrame(measure);

    const onReflow = () => measure();
    window.addEventListener('resize', onReflow);
    window.addEventListener('scroll', onReflow, true);
    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener('resize', onReflow);
      window.removeEventListener('scroll', onReflow, true);
    };
  }, [selector, stopKey]);

  return rect;
}

export function DemoTour({ onFinish }: { onFinish: () => void }) {
  const [index, setIndex] = useState(0);

  const stop = TOUR_STOPS[index];
  const isFirst = index === 0;
  const isLast = index === TOUR_STOPS.length - 1;

  // Cada parada roteia para a tela real; o card e o recorte acompanham a navegação. Usamos a API imperativa
  // do router (singleton): o tour vive fora do RouterProvider, então useNavigate() não está disponível aqui.
  useEffect(() => {
    void router.navigate(stop.path);
  }, [stop.path]);

  const spotlight = useSpotlightRect(stop.spotlight, index);
  const Icon = stop.icon;

  return (
    <div className="pointer-events-none fixed inset-0 z-50">
      <SpotlightOverlay rect={spotlight} />

      {/* Anel de destaque na viewport: reforça, em qualquer resolução, que a pessoa entrou no modo tour. */}
      <div className="pointer-events-none absolute inset-0 rounded-lg ring-2 ring-inset ring-primary/40" />

      <div className="pointer-events-auto absolute inset-x-0 bottom-4 mx-auto w-[min(92vw,32rem)] overflow-hidden rounded-xl border-2 border-primary/40 bg-card text-card-foreground shadow-2xl ring-4 ring-primary/10 sm:bottom-6">
        <div className="flex items-center justify-between gap-3 border-b bg-primary/10 px-4 py-2">
          <span className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-primary">
            <Compass className="size-3.5" aria-hidden />
            Tour guiado
          </span>
          <span className="text-xs font-medium text-muted-foreground">
            {index + 1} de {TOUR_STOPS.length}
          </span>
          <button
            type="button"
            onClick={onFinish}
            aria-label="Pular o tour"
            className="rounded-md p-1 text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
          >
            <X className="size-4" />
          </button>
        </div>

        <div className="flex items-start gap-3 p-4">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <Icon className="size-5" aria-hidden />
          </span>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold leading-tight">{stop.title}</p>
            <p className="mt-1 text-[13px] leading-snug text-muted-foreground">
              {stop.legend}
            </p>
          </div>
        </div>

        <div className="flex items-center justify-between gap-3 border-t p-3">
          <div className="flex items-center gap-1.5" aria-hidden>
            {TOUR_STOPS.map((_, i) => (
              <span
                key={i}
                className={cn(
                  'h-1.5 rounded-full transition-all',
                  i === index ? 'w-5 bg-primary' : 'w-1.5 bg-muted-foreground/30',
                )}
              />
            ))}
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

/**
 * O scrim do modo tour. Com alvo, abre um recorte arredondado (spotlight) sobre ele via máscara SVG e
 * contorna o recorte com um anel de destaque, deixando a tela real visível no foco. Sem alvo, é um scrim
 * uniforme e suave: comunica o modo sem esconder o conteúdo. Nunca captura cliques (pointer-events-none),
 * porque o tour é para MOSTRAR as telas, não travá-las.
 */
function SpotlightOverlay({ rect }: { rect: SpotlightRect | null }) {
  // Escurecimento propositalmente leve: o objetivo é enquadrar, não ocultar as telas reais.
  const scrim = 'rgb(2 6 23 / 0.55)';

  if (!rect) {
    return (
      <div
        className="pointer-events-none absolute inset-0"
        style={{ background: scrim }}
      />
    );
  }

  const radius = 10;
  const maskId = 'demo-tour-spotlight-mask';

  return (
    <div className="pointer-events-none absolute inset-0">
      <svg className="absolute inset-0 h-full w-full">
        <defs>
          <mask id={maskId}>
            <rect x="0" y="0" width="100%" height="100%" fill="white" />
            <rect
              x={rect.left}
              y={rect.top}
              width={rect.width}
              height={rect.height}
              rx={radius}
              ry={radius}
              fill="black"
            />
          </mask>
        </defs>
        <rect
          x="0"
          y="0"
          width="100%"
          height="100%"
          fill={scrim}
          mask={`url(#${maskId})`}
        />
        <rect
          x={rect.left}
          y={rect.top}
          width={rect.width}
          height={rect.height}
          rx={radius}
          ry={radius}
          fill="none"
          className="stroke-primary"
          strokeWidth={2}
        />
      </svg>
    </div>
  );
}
