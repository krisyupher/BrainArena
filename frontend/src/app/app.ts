import { Component, effect, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from './core/services/auth.service';
import { RoomHubService } from './core/services/room-hub.service';

@Component({
  imports: [RouterOutlet, RouterLink, TranslocoPipe],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html'
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);
  private readonly roomHub = inject(RoomHubService);

  protected readonly activeLang = this.transloco.activeLang;

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

  setLang(lang: string): void {
    this.transloco.setActiveLang(lang);
  }

  logout(): void {
    this.auth.logout();
  }
}
