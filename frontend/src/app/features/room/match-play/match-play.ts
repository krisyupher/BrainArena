import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { RoomHubService } from '../../../core/services/room-hub.service';
import { AuthService } from '../../../core/services/auth.service';
import { QuestionRevealedEvent, QuestionStartedEvent, ScoreboardEntry } from '../../../core/models/match.model';

type Phase = 'countdown' | 'question' | 'reveal';

@Component({
  imports: [TranslocoPipe],
  selector: 'app-match-play',
  styleUrl: './match-play.scss',
  templateUrl: './match-play.html'
})
export class MatchPlay implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly roomHub = inject(RoomHubService);
  private readonly auth = inject(AuthService);

  readonly phase = signal<Phase>('countdown');
  readonly currentQuestion = signal<QuestionStartedEvent | null>(null);
  readonly reveal = signal<QuestionRevealedEvent | null>(null);
  readonly scoreboard = signal<ScoreboardEntry[]>([]);
  readonly selectedOption = signal<number | null>(null);
  readonly answerLocked = signal(false);
  readonly secondsLeft = signal(0);
  readonly errorMessage = signal<string | null>(null);

  private roomId!: string;
  private endsAt: Date | null = null;
  private tickHandle?: ReturnType<typeof setInterval>;
  private readonly subscriptions = new Subscription();

  get myUserId(): string | undefined {
    return this.auth.currentUser()?.userId;
  }

  ngOnInit(): void {
    this.roomId = this.route.snapshot.paramMap.get('id')!;
    this.roomHub.joinRoomGroup(this.roomId);

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

    this.subscriptions.add(
      this.roomHub.matchResync.subscribe((resync) => {
        this.scoreboard.set(resync.scoreboard);
        if (resync.phase === 'Question' && resync.currentQuestion) {
          this.phase.set('question');
          this.currentQuestion.set(resync.currentQuestion);
          this.setEndsAt(new Date(resync.currentQuestion.endsAtUtc));
        } else if (resync.phase === 'Reveal' && resync.currentReveal) {
          this.phase.set('reveal');
          this.reveal.set(resync.currentReveal);
          this.answerLocked.set(true);
          this.setEndsAt(new Date(resync.currentReveal.endsAtUtc));
        } else {
          this.phase.set('countdown');
          this.endsAt = null;
        }
      })
    );

    this.tickHandle = setInterval(() => this.tick(), 250);
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    if (this.tickHandle) {
      clearInterval(this.tickHandle);
    }
  }

  selectOption(index: number): void {
    if (this.answerLocked() || this.phase() !== 'question') {
      return;
    }
    const question = this.currentQuestion();
    if (!question) {
      return;
    }

    this.selectedOption.set(index);
    this.answerLocked.set(true);

    this.roomHub
      .submitAnswer(this.roomId, question.matchQuestionId, index)
      .catch((err: Error) => this.errorMessage.set(err?.message ?? null));
  }

  isCorrectOption(index: number): boolean {
    return this.reveal()?.correctOptionIndex === index;
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
