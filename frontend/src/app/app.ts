import { Component, ElementRef, HostListener, effect, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from './core/services/auth.service';
import { RoomHubService } from './core/services/room-hub.service';
import { ThemeService } from './core/services/theme.service';

@Component({
  imports: [RouterOutlet, RouterLink, TranslocoPipe],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html'
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  private readonly transloco = inject(TranslocoService);
  private readonly roomHub = inject(RoomHubService);
  private readonly elementRef = inject(ElementRef<HTMLElement>);

  protected readonly activeLang = this.transloco.activeLang;
  protected readonly dropdownOpen = signal(false);

  constructor() {
    // The hub connection is app-wide, not tied to any one route or to being logged in — anonymous
    // visitors can spectate a room/match, so it's established unconditionally on bootstrap. A
    // connection's identity is fixed at handshake time (the JWT is read once via
    // accessTokenFactory), so logging in or out mid-session still needs a full reconnect for the
    // hub to pick up the new auth state — this only fires on an actual transition, not on the
    // effect's initial run.
    this.roomHub.connect();
    let wasAuthenticated = this.auth.isAuthenticated();
    effect(() => {
      const isAuthenticated = this.auth.isAuthenticated();
      if (isAuthenticated !== wasAuthenticated) {
        this.roomHub.disconnect().then(() => this.roomHub.connect());
      }
      wasAuthenticated = isAuthenticated;
    });
  }

  // First outside-click-to-close pattern in the codebase — everything else that opens/closes
  // (e.g. the reaction picker) only closes via re-click or an explicit action. A top-level settings
  // menu is more exposed to "opened and forgotten," so this one gets it.
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.dropdownOpen() && !this.elementRef.nativeElement.contains(event.target as Node)) {
      this.dropdownOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.dropdownOpen.set(false);
  }

  toggleDropdown(): void {
    this.dropdownOpen.set(!this.dropdownOpen());
  }

  setLang(lang: string): void {
    this.transloco.setActiveLang(lang);
  }

  toggleTheme(): void {
    this.theme.toggle();
  }

  logout(): void {
    this.dropdownOpen.set(false);
    this.auth.logout();
  }
}
