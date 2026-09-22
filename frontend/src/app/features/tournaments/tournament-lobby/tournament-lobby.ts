import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { TournamentService } from '../../../core/services/tournament.service';
import { RoomHubService } from '../../../core/services/room-hub.service';
import { TournamentSummary } from '../../../core/models/tournament.model';
import { CreateTournamentForm } from '../create-tournament-form/create-tournament-form';

@Component({
  imports: [TranslocoPipe, RouterLink, CreateTournamentForm],
  selector: 'app-tournament-lobby',
  styleUrl: './tournament-lobby.scss',
  templateUrl: './tournament-lobby.html'
})
export class TournamentLobby implements OnInit, OnDestroy {
  private readonly tournamentService = inject(TournamentService);
  private readonly roomHub = inject(RoomHubService);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  readonly tournaments = signal<TournamentSummary[]>([]);
  readonly loading = signal(true);
  readonly showCreateForm = signal(false);

  private hubSubscription?: Subscription;

  ngOnInit(): void {
    this.loadTournaments();
    this.hubSubscription = this.roomHub.tournamentListChanged.subscribe(() => this.loadTournaments());
  }

  ngOnDestroy(): void {
    this.hubSubscription?.unsubscribe();
  }

  onTournamentCreated(tournamentId: string): void {
    this.showCreateForm.set(false);
    this.router.navigate(['/tournaments', tournamentId]);
  }

  private loadTournaments(): void {
    this.tournamentService.getOpenTournaments().subscribe({
      next: (tournaments) => {
        this.tournaments.set(tournaments);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
