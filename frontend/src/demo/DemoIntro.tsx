import { useState } from 'react';
import { useAuth } from '@/modules/auth/AuthProvider';
import { DemoRibbon } from '@/demo/DemoRibbon';
import { DemoIntroDialog } from '@/demo/DemoIntroDialog';
import { hasSeenDemoIntro, markDemoIntroSeen } from '@/demo/introSeen';

/**
 * Demo onboarding, mounted once at the App root (demo build only): the permanent "this is a demo" pill plus
 * the welcome dialog, which opens by itself on a first visit and on demand from the pill afterwards.
 *
 * Dismissing it is what marks the visitor as introduced — reopening from the pill must not re-arm the flag,
 * and the flag is only written on close so a visitor who never engages still gets the intro next time.
 */
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
      {/* Hold the dialog back until the session resolves, so it opens over the dashboard rather than over
          the bootstrap spinner. */}
      {open && status !== 'loading' && <DemoIntroDialog onClose={close} />}
    </>
  );
}
