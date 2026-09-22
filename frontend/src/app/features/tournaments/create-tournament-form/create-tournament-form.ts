import { Component, inject, output, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { TournamentService } from '../../../core/services/tournament.service';
import { GAME_MODES, GameMode, ROOM_TOPICS, RoomTopic } from '../../../core/models/room.model';

@Component({
  imports: [ReactiveFormsModule, TranslocoPipe],
  selector: 'app-create-tournament-form',
  styleUrl: './create-tournament-form.scss',
  templateUrl: './create-tournament-form.html'
})
export class CreateTournamentForm {
  private readonly fb = inject(FormBuilder);
  private readonly tournamentService = inject(TournamentService);

  readonly created = output<string>();
  readonly cancelled = output<void>();

  readonly topics = ROOM_TOPICS;
  readonly gameModes = GAME_MODES;
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(40)]],
    topic: ['Math' as RoomTopic, [Validators.required]],
    gameMode: ['multiple-choice' as GameMode, [Validators.required]],
    tournamentSize: [8, [Validators.required, Validators.min(4), Validators.max(64)]],
    roomSize: [2, [Validators.required, Validators.min(2), Validators.max(10)]],
    advancesPerRoom: [1, [Validators.required, Validators.min(1)]],
    minPlayersToStart: [4, [Validators.required, Validators.min(2)]],
    questionCount: [5, [Validators.required, Validators.min(5), Validators.max(20)]],
    secondsPerQuestion: [15, [Validators.required, Validators.min(10), Validators.max(60)]]
  });

  /** Topic only drives the multiple-choice question bank — calculation mode generates its own problems. */
  get isTopicRelevant(): boolean {
    return this.form.controls.gameMode.value === 'multiple-choice';
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.errorMessage.set(null);
    this.submitting.set(true);

    this.tournamentService.create(this.form.getRawValue()).subscribe({
      next: (tournament) => {
        this.submitting.set(false);
        this.created.emit(tournament.id);
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.error?.title ?? null);
      }
    });
  }

  cancel(): void {
    this.cancelled.emit();
  }
}
