const INTRO_SEEN_KEY = 'sislab:demo-intro-seen';

/**
 * "Has this visitor already been introduced to the demo?" — persisted so the welcome dialog greets a
 * first-time visitor and then stays out of the way.
 *
 * Every access is guarded: localStorage throws in private/embedded contexts with storage blocked, and a
 * demo whose whole point is being frictionless must not white-screen over a preference flag. When it is
 * unavailable we fall back to "not seen yet" — showing the intro again is harmless, crashing is not.
 */
export function hasSeenDemoIntro(): boolean {
  try {
    return localStorage.getItem(INTRO_SEEN_KEY) === '1';
  } catch {
    return false;
  }
}

export function markDemoIntroSeen(): void {
  try {
    localStorage.setItem(INTRO_SEEN_KEY, '1');
  } catch {
    // Storage blocked: the visitor simply sees the intro again on the next visit.
  }
}
