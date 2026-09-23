import { Injectable, signal } from '@angular/core';

export type Theme = 'dark' | 'light';

const STORAGE_KEY = 'brainarena.theme';

/**
 * A tiny inline <script> in index.html already set the `data-theme` attribute on <html> before
 * Angular ever loaded (avoids a flash of the wrong theme on first paint) — this service seeds its
 * signal from that attribute rather than recomputing localStorage/prefers-color-scheme a second
 * time, so there's a single source of truth. The independent recompute below only runs as a
 * defensive fallback for an environment where the inline script didn't execute (e.g. a test
 * harness that renders components without loading index.html).
 */
function readInitialTheme(): Theme {
  const fromDom = document.documentElement.getAttribute('data-theme');
  if (fromDom === 'light' || fromDom === 'dark') {
    return fromDom;
  }

  const stored = localStorage.getItem(STORAGE_KEY);
  if (stored === 'light' || stored === 'dark') {
    return stored;
  }

  // matchMedia is absent in some test/SSR environments — default to dark rather than throwing.
  return typeof window.matchMedia === 'function' && window.matchMedia('(prefers-color-scheme: light)').matches
    ? 'light'
    : 'dark';
}

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly theme = signal<Theme>(readInitialTheme());

  constructor() {
    document.documentElement.setAttribute('data-theme', this.theme());
  }

  setTheme(theme: Theme): void {
    this.theme.set(theme);
    localStorage.setItem(STORAGE_KEY, theme);
    document.documentElement.setAttribute('data-theme', theme);
  }

  toggle(): void {
    this.setTheme(this.theme() === 'dark' ? 'light' : 'dark');
  }
}
