import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { TournamentService } from '../../../core/services/tournament.service';
import { RoomHubService } from '../../../core/services/room-hub.service';
import { TournamentDetail as TournamentDetailModel, TournamentRoundRoom } from '../../../core/models/tournament.model';

@Component({
  imports: [TranslocoPipe, RouterLink],
  selector: 'app-tournament-detail',
  styleUrl: './tournament-detail.scss',
  templateUrl: './tournament-detail.html'
})
export class TournamentDetail implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly tournamentService = inject(TournamentService);
  private readonly roomHub = inject(RoomHubService);
  private readonly transloco = inject(TranslocoService);
  protected readonly auth = inject(AuthService);

  readonly tournament = signal<TournamentDetailModel | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly busy = signal(false);

  private tournamentId!: string;
  private readonly subscriptions = new Subscription();

  get myUserId(): string | undefined {
    return this.auth.currentUser()?.userId;
  }

  get isCreator(): boolean {
    const t = this.tournament();
    return !!t && t.creatorUserId === this.myUserId;
  }

  get isMember(): boolean {
    const t = this.tournament();
    return !!t && !!this.myUserId && t.players.some((p) => p.userId === this.myUserId);
  }

  get canStart(): boolean {
    const t = this.tournament();
    return !!t && t.status === 'Waiting' && this.isCreator && t.players.length >= t.minPlayersToStart;
  }

  /** The current round's room this player is still active in, if any — the "go to your match" link. */
  get myActiveRoom(): TournamentRoundRoom | undefined {
    const t = this.tournament();
    const userId = this.myUserId;
    if (!t || !userId) {
      return undefined;
    }
    const round = t.rounds.find((r) => r.roundNumber === t.currentRoundNumber);
    return round?.rooms.find((room) => room.players.some((p) => p.userId === userId));
  }

  ngOnInit(): void {
    this.tournamentId = this.route.snapshot.paramMap.get('id')!;

    this.loadTournament();
    this.roomHub.joinTournamentGroup(this.tournamentId);
    this.subscriptions.add(this.roomHub.tournamentUpdated.subscribe(() => this.loadTournament()));
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.roomHub.leaveTournamentGroup(this.tournamentId);
  }

  join(): void {
    this.errorMessage.set(null);
    this.busy.set(true);
    this.tournamentService.join(this.tournamentId).subscribe({
      next: (t) => {
        this.tournament.set(t);
        this.busy.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.title ?? null);
        this.busy.set(false);
      }
    });
  }

  leave(): void {
    this.errorMessage.set(null);
    this.busy.set(true);
    this.tournamentService.leave(this.tournamentId).subscribe({
      next: () => this.router.navigateByUrl('/tournaments'),
      error: (err) => {
        this.errorMessage.set(err?.error?.title ?? null);
        this.busy.set(false);
      }
    });
  }

  start(): void {
    this.errorMessage.set(null);
    this.busy.set(true);
    this.tournamentService.start(this.tournamentId).subscribe({
      next: () => this.busy.set(false),
      error: (err) => {
        this.errorMessage.set(err?.error?.title ?? null);
        this.busy.set(false);
      }
    });
  }

  playerName(t: TournamentDetailModel, userId: string): string {
    return t.players.find((p) => p.userId === userId)?.displayName ?? '';
  }

  playerStatus(t: TournamentDetailModel, userId: string): string | undefined {
    return t.players.find((p) => p.userId === userId)?.status;
  }

  private loadTournament(): void {
    this.tournamentService.getById(this.tournamentId).subscribe({
      next: (t) => this.tournament.set(t),
      error: () => this.errorMessage.set(this.transloco.translate('room.notFound'))
    });
  }
}
