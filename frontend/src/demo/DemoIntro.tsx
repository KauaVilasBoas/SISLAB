import { useState } from 'react';
import { useAuth } from '@/modules/auth/AuthProvider';
import { DemoRibbon } from '@/demo/DemoRibbon';
import { DemoIntroDialog } from '@/demo/DemoIntroDialog';
import { hasSeenDemoIntro, markDemoIntroSeen } from '@/demo/introSeen';

export function DemoIntro() {
  const { status } = useAuth();
  const [open, setOpen] = useState(() => !hasSeenDemoIntro());

  function close() {
    setOpen(false);
    markDemoIntroSeen();
  }

  return (
    <>
      <DemoRibbon onOpenIntro={() => setOpen(true)} />
      {open && status !== 'loading' && <DemoIntroDialog onClose={close} />}
    </>
  );
}
