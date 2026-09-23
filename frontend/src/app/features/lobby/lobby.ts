import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { RoomService } from '../../core/services/room.service';
import { TournamentService } from '../../core/services/tournament.service';
import { RoomHubService } from '../../core/services/room-hub.service';
import { RoomSummary } from '../../core/models/room.model';
import { TournamentSummary } from '../../core/models/tournament.model';
import { CreateGameForm, GameKind } from './create-game-form/create-game-form';
import { EmptyState } from '../../shared/empty-state/empty-state';

@Component({
  imports: [TranslocoPipe, RouterLink, CreateGameForm, EmptyState],
  selector: 'app-lobby',
  styleUrl: './lobby.scss',
  templateUrl: './lobby.html'
})
export class Lobby implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly roomService = inject(RoomService);
  private readonly tournamentService = inject(TournamentService);
  private readonly roomHub = inject(RoomHubService);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  readonly activeTab = signal<GameKind>('multiplayer');
  readonly rooms = signal<RoomSummary[]>([]);
  readonly tournaments = signal<TournamentSummary[]>([]);
  readonly loading = signal(true);
  readonly showCreateForm = signal(false);

  private readonly subscriptions = new Subscription();

  get multiplayerRooms(): RoomSummary[] {
    return this.rooms().filter((r) => r.kind === 'Multiplayer');
  }

  ngOnInit(): void {
    const tab = this.route.snapshot.queryParamMap.get('tab');
    if (tab === 'solitary' || tab === 'multiplayer' || tab === 'tournament') {
      this.activeTab.set(tab);
    }

    this.loadAll();
    this.subscriptions.add(this.roomHub.roomListChanged.subscribe(() => this.loadRooms()));
    this.subscriptions.add(this.roomHub.tournamentListChanged.subscribe(() => this.loadTournaments()));
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  setTab(tab: GameKind): void {
    this.activeTab.set(tab);
  }

  join(room: RoomSummary): void {
    this.roomService.join(room.id).subscribe(() => this.router.navigate(['/rooms', room.id]));
  }

  onRoomCreated(roomId: string): void {
    this.showCreateForm.set(false);
    this.router.navigate(['/rooms', roomId]);
  }

  onSolitaryStarted(roomId: string): void {
    this.showCreateForm.set(false);
    this.router.navigate(['/rooms', roomId, 'play']);
  }

  onTournamentCreated(tournamentId: string): void {
    this.showCreateForm.set(false);
    this.router.navigate(['/tournaments', tournamentId]);
  }

  private loadAll(): void {
    this.loading.set(true);
    this.loadRooms();
    this.loadTournaments();
  }

  private loadRooms(): void {
    this.roomService.getOpenRooms().subscribe({
      next: (rooms) => {
        this.rooms.set(rooms);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  private loadTournaments(): void {
    this.tournamentService.getOpenTournaments().subscribe({
      next: (tournaments) => this.tournaments.set(tournaments),
      error: () => undefined
    });
  }
}
