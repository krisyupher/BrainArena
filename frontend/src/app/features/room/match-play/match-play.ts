import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { RoomHubService } from '../../../core/services/room-hub.service';
import { RoomService } from '../../../core/services/room.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  MatchResyncEvent,
  MatchSpectatorSyncEvent,
  QuestionRevealedEvent,
  QuestionStartedEvent,
  ScoreboardEntry
} from '../../../core/models/match.model';
import { MultipleChoicePanel } from './multiple-choice-panel/multiple-choice-panel';
import { CalculationPanel } from './calculation-panel/calculation-panel';
import { Chat } from '../chat/chat';
import { CompetitorsPanel, CompetitorViewModel } from '../competitors-panel/competitors-panel';

type Phase = 'countdown' | 'question' | 'reveal';

@Component({
  imports: [TranslocoPipe, MultipleChoicePanel, CalculationPanel, Chat, CompetitorsPanel],
  selector: 'app-match-play',
  styleUrl: './match-play.scss',
  templateUrl: './match-play.html'
})
export class MatchPlay implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly roomHub = inject(RoomHubService);
  private readonly roomService = inject(RoomService);
  private readonly auth = inject(AuthService);

  readonly phase = signal<Phase>('countdown');
  readonly currentQuestion = signal<QuestionStartedEvent | null>(null);
  readonly reveal = signal<QuestionRevealedEvent | null>(null);
  readonly scoreboard = signal<ScoreboardEntry[]>([]);
  readonly selectedOption = signal<number | null>(null);
  readonly answerLocked = signal(false);
  readonly secondsLeft = signal(0);
  readonly errorMessage = signal<string | null>(null);
  /** Unknown until the room detail lookup resolves — the answer UI stays hidden until then. */
  readonly isParticipant = signal<boolean | null>(null);

  protected roomId!: string;
  private endsAt: Date | null = null;
  private tickHandle?: ReturnType<typeof setInterval>;
  private readonly subscriptions = new Subscription();

  get myUserId(): string | undefined {
    return this.auth.currentUser()?.userId;
  }

  /** Any signed-in user can chat/react here, whether they're playing or just spectating. */
  get isSignedIn(): boolean {
    return this.auth.isAuthenticated();
  }

  get competitors(): CompetitorViewModel[] {
    return this.scoreboard().map((entry) => ({
      userId: entry.userId,
      displayName: entry.displayName,
      score: entry.score,
      isConnected: entry.isConnected
    }));
  }

  ngOnInit(): void {
    this.roomId = this.route.snapshot.paramMap.get('id')!;

    // Whether we can submit answers depends on room membership, which we don't know until this
    // resolves — join as a player (gets MatchResync) or a spectator (gets MatchSpectatorSync)
    // accordingly, rather than assuming player like the pre-spectator-mode code did.
    this.roomService.getById(this.roomId).subscribe({
      next: (room) => {
        const userId = this.myUserId;
        const participant = !!userId && room.players.some((p) => p.userId === userId);
        this.isParticipant.set(participant);
        // Seeds the competitors panel before any live match event arrives (e.g. during the very
        // first question, before a reveal/resync has ever populated real ScoreboardEntry data).
        // Real data below (questionRevealed / applySync) always overwrites this afterwards.
        this.scoreboard.set(room.players.map((p) => ({ userId: p.userId, displayName: p.displayName, score: 0, isConnected: true })));
        if (participant) {
          this.roomHub.joinRoomGroup(this.roomId);
        } else {
          this.roomHub.joinAsSpectator(this.roomId);
        }
      },
      error: () => {
        // Not found / not visible (e.g. an anonymous visitor and a private room) — don't attempt
        // to join anything, the hub would reject it the same way.
        this.router.navigateByUrl('/lobby');
      }
    });

    this.subscriptions.add(
      this.roomHub.matchStarting.subscribe((event) => {
        this.phase.set('countdown');
        this.setEndsAt(new Date(Date.now() + event.countdownSeconds * 1000));
      })
    );

    this.subscriptions.add(
      this.roomHub.questionStarted.subscribe((event) => {
        this.phase.set('question');
        this.currentQuestion.set(event);
        this.reveal.set(null);
        this.selectedOption.set(null);
        this.answerLocked.set(false);
        this.errorMessage.set(null);
        this.setEndsAt(new Date(event.endsAtUtc));
      })
    );

    this.subscriptions.add(this.roomHub.answerAccepted.subscribe(() => this.answerLocked.set(true)));

    this.subscriptions.add(
      this.roomHub.questionRevealed.subscribe((event) => {
        this.phase.set('reveal');
        this.reveal.set(event);
        this.scoreboard.set(event.scoreboard);
        this.answerLocked.set(true);
        this.setEndsAt(new Date(event.endsAtUtc));
      })
    );

    this.subscriptions.add(
      this.roomHub.matchEnded.subscribe(() => {
        this.router.navigate(['/rooms', this.roomId, 'results']);
      })
    );

    this.subscriptions.add(this.roomHub.matchResync.subscribe((resync) => this.applySync(resync)));

    // Non-personalized counterpart of matchResync, for a spectator joining mid-match.
    this.subscriptions.add(this.roomHub.matchSpectatorSync.subscribe((sync) => this.applySync(sync)));

    this.tickHandle = setInterval(() => this.tick(), 250);
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    if (this.tickHandle) {
      clearInterval(this.tickHandle);
    }
  }

  submitOption(index: number): void {
    const question = this.currentQuestion();
    if (!this.isParticipant() || this.answerLocked() || this.phase() !== 'question' || !question) {
      return;
    }

    this.selectedOption.set(index);
    this.answerLocked.set(true);

    this.roomHub
      .submitOptionAnswer(this.roomId, question.matchQuestionId, index)
      .catch((err: Error) => this.errorMessage.set(err?.message ?? null));
  }

  submitNumeric(value: number): void {
    const question = this.currentQuestion();
    if (!this.isParticipant() || this.answerLocked() || this.phase() !== 'question' || !question) {
      return;
    }

    this.answerLocked.set(true);

    this.roomHub
      .submitNumericAnswer(this.roomId, question.matchQuestionId, value)
      .catch((err: Error) => this.errorMessage.set(err?.message ?? null));
  }

  private applySync(sync: MatchResyncEvent | MatchSpectatorSyncEvent): void {
    this.scoreboard.set(sync.scoreboard);
    if (sync.phase === 'Question' && sync.currentQuestion) {
      this.phase.set('question');
      this.currentQuestion.set(sync.currentQuestion);
      this.setEndsAt(new Date(sync.currentQuestion.endsAtUtc));
    } else if (sync.phase === 'Reveal' && sync.currentReveal) {
      this.phase.set('reveal');
      this.reveal.set(sync.currentReveal);
      this.answerLocked.set(true);
      this.setEndsAt(new Date(sync.currentReveal.endsAtUtc));
    } else {
      this.phase.set('countdown');
      this.endsAt = null;
    }
  }

  private setEndsAt(endsAt: Date): void {
    this.endsAt = endsAt;
    this.tick();
  }

  private tick(): void {
    if (!this.endsAt) {
      this.secondsLeft.set(0);
      return;
    }
    const remainingMs = this.endsAt.getTime() - Date.now();
    this.secondsLeft.set(Math.max(0, Math.ceil(remainingMs / 1000)));
  }
}
