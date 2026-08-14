const INTRO_SEEN_KEY = 'sislab:demo-intro-seen';

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
    /* empty */
  }
}
