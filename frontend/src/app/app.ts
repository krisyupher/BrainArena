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
    // The hub connection is app-wide, not tied to any one route: connect once the user is
    // authenticated, keep it alive across navigation, and only tear it down on logout.
    effect(() => {
      if (this.auth.isAuthenticated()) {
        this.roomHub.connect();
      } else {
        this.roomHub.disconnect();
      }
    });
  }

  setLang(lang: string): void {
    this.transloco.setActiveLang(lang);
  }

  logout(): void {
    this.auth.logout();
  }
}
