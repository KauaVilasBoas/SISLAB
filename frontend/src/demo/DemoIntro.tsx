import { useState } from 'react';
import { useAuth } from '@/modules/auth/AuthProvider';
import { DemoRibbon } from '@/demo/DemoRibbon';
import { DemoIntroDialog } from '@/demo/DemoIntroDialog';
import { DemoTour } from '@/demo/DemoTour';
import { HowItsBuiltDialog } from '@/demo/HowItsBuiltDialog';
import { hasSeenDemoIntro, markDemoIntroSeen } from '@/demo/introSeen';

type DemoSurface = 'intro' | 'tour' | 'how-its-built' | null;

/**
 * Orquestra as superfícies da demo: a ribbon persistente, o modal de boas-vindas (só na primeira visita),
 * o tour guiado e a superfície técnica opt-in "Como foi construído". Uma única superfície fica ativa por
 * vez. Marcar o intro como visto vale para qualquer saída da boas-vindas (fechar, iniciar o tour ou abrir
 * a arquitetura), para não reaparecer a cada carregamento.
 */
export function DemoIntro() {
  const { status } = useAuth();
  const [surface, setSurface] = useState<DemoSurface>(() =>
    hasSeenDemoIntro() ? null : 'intro',
  );

  function dismissIntro() {
    markDemoIntroSeen();
    setSurface(null);
  }

  const introReady = status !== 'loading';

  return (
    <>
      <DemoRibbon
        onOpenIntro={() => setSurface('intro')}
        onOpenHowItsBuilt={() => setSurface('how-its-built')}
      />

      {surface === 'intro' && introReady && (
        <DemoIntroDialog
          onStartTour={() => {
            markDemoIntroSeen();
            setSurface('tour');
          }}
          onOpenHowItsBuilt={() => {
            markDemoIntroSeen();
            setSurface('how-its-built');
          }}
          onClose={dismissIntro}
        />
      )}

      {surface === 'tour' && <DemoTour onFinish={() => setSurface(null)} />}

      {surface === 'how-its-built' && (
        <HowItsBuiltDialog onClose={() => setSurface(null)} />
      )}
    </>
  );
}
