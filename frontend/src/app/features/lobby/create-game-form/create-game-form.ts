import { Component, inject, input, output, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { RoomService } from '../../../core/services/room.service';
import { TournamentService } from '../../../core/services/tournament.service';
import { GAME_MODES, GameMode, ROOM_TOPICS, RoomKind, RoomTopic } from '../../../core/models/room.model';

/** UI-level kind selector — a superset of the wire-level RoomKind, since "tournament" isn't a Room at all. */
export type GameKind = 'solitary' | 'multiplayer' | 'tournament';

const GAME_KINDS: GameKind[] = ['solitary', 'multiplayer', 'tournament'];

@Component({
  imports: [ReactiveFormsModule, TranslocoPipe],
  selector: 'app-create-game-form',
  styleUrl: './create-game-form.scss',
  templateUrl: './create-game-form.html'
})
export class CreateGameForm {
  private readonly fb = inject(FormBuilder);
  private readonly roomService = inject(RoomService);
  private readonly tournamentService = inject(TournamentService);
  private readonly transloco = inject(TranslocoService);

  /** Pre-selects the form's kind to match whichever lobby tab was active when it was opened. */
  readonly initialKind = input<GameKind>('multiplayer');

  readonly roomCreated = output<string>();
  readonly solitaryStarted = output<string>();
  readonly tournamentCreated = output<string>();
  readonly cancelled = output<void>();

  readonly kinds = GAME_KINDS;
  readonly topics = ROOM_TOPICS;
  readonly gameModes = GAME_MODES;
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly createdShareCode = signal<string | null>(null);
  private createdRoomId: string | null = null;

  readonly form = this.fb.nonNullable.group({
    kind: this.initialKind() as GameKind,
    gameMode: ['multiple-choice' as GameMode, [Validators.required]],
    topic: ['Math' as RoomTopic, [Validators.required]],
    questionCount: [10, [Validators.required, Validators.min(5), Validators.max(20)]],
    secondsPerQuestion: [20, [Validators.required, Validators.min(10), Validators.max(60)]],
    maxPlayers: [6, [Validators.required, Validators.min(2), Validators.max(10)]],
    minPlayersToStart: [2, [Validators.required, Validators.min(2)]],
    isPrivate: [false],
    tournamentSize: [8, [Validators.required, Validators.min(4), Validators.max(64)]],
    roomSize: [2, [Validators.required, Validators.min(2), Validators.max(10)]],
    advancesPerRoom: [1, [Validators.required, Validators.min(1)]]
  });

  /** Topic only drives the multiple-choice question bank — calculation mode generates its own problems. */
  get isTopicRelevant(): boolean {
    return this.form.controls.gameMode.value === 'multiple-choice';
  }

  setKind(kind: GameKind): void {
    this.form.controls.kind.setValue(kind);
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.errorMessage.set(null);
    this.submitting.set(true);

    const values = this.form.getRawValue();
    const name = this.computeName(values.kind, values.gameMode, values.topic);

    if (values.kind === 'tournament') {
      this.tournamentService
        .create({
          name,
          topic: values.topic,
          gameMode: values.gameMode,
          questionCount: values.questionCount,
          secondsPerQuestion: values.secondsPerQuestion,
          tournamentSize: values.tournamentSize,
          roomSize: values.roomSize,
          advancesPerRoom: values.advancesPerRoom,
          minPlayersToStart: values.minPlayersToStart
        })
        .subscribe({
          next: (tournament) => {
            this.submitting.set(false);
            this.tournamentCreated.emit(tournament.id);
          },
          error: (err) => this.handleError(err)
        });
      return;
    }

    const kind: RoomKind = values.kind === 'solitary' ? 'Solitary' : 'Multiplayer';

    this.roomService
      .create({
        name,
        topic: values.topic,
        gameMode: values.gameMode,
        questionCount: values.questionCount,
        secondsPerQuestion: values.secondsPerQuestion,
        maxPlayers: values.maxPlayers,
        minPlayersToStart: values.minPlayersToStart,
        isPrivate: values.isPrivate,
        kind
      })
      .subscribe({
        next: (room) => {
          this.submitting.set(false);

          if (values.kind === 'solitary') {
            // A solitary room auto-starts synchronously inside the create call — if it's not
            // already InProgress by the time the response comes back, question generation failed
            // (e.g. not enough content for the topic) and it never will retry, since nobody else
            // can ever join a 1/1 room. Surface that as a creation failure, not a waiting room.
            if (room.status === 'InProgress') {
              this.solitaryStarted.emit(room.id);
            } else {
              this.errorMessage.set(this.transloco.translate('createRoom.solitaryStartFailed'));
            }
            return;
          }

          this.createdRoomId = room.id;
          if (room.isPrivate && room.shareCode) {
            this.createdShareCode.set(room.shareCode);
          } else {
            this.roomCreated.emit(room.id);
          }
        },
        error: (err) => this.handleError(err)
      });
  }

  continueToRoom(): void {
    if (this.createdRoomId) {
      this.roomCreated.emit(this.createdRoomId);
    }
  }

  cancel(): void {
    this.cancelled.emit();
  }

  private handleError(err: { error?: { title?: string } }): void {
    this.submitting.set(false);
    this.errorMessage.set(err?.error?.title ?? null);
  }

  private computeName(kind: GameKind, gameMode: GameMode, topic: RoomTopic): string {
    const modeLabel = this.transloco.translate('createRoom.gameModeOption.' + gameMode);
    const topicLabel = gameMode === 'multiple-choice' ? this.transloco.translate('lobby.topic.' + topic) : null;
    const base = topicLabel ? `${modeLabel} · ${topicLabel}` : modeLabel;

    if (kind === 'solitary') {
      return `${this.transloco.translate('createRoom.practicePrefix')}: ${base}`;
    }
    if (kind === 'tournament') {
      return `${this.transloco.translate('createRoom.tournamentPrefix')}: ${base}`;
    }
    return base;
  }
}
