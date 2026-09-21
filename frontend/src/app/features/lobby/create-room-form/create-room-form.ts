import { Component, inject, output, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { RoomService } from '../../../core/services/room.service';
import { ROOM_TOPICS, RoomTopic } from '../../../core/models/room.model';

@Component({
  imports: [ReactiveFormsModule, TranslocoPipe],
  selector: 'app-create-room-form',
  styleUrl: './create-room-form.scss',
  templateUrl: './create-room-form.html'
})
export class CreateRoomForm {
  private readonly fb = inject(FormBuilder);
  private readonly roomService = inject(RoomService);

  readonly created = output<string>();
  readonly cancelled = output<void>();

  readonly topics = ROOM_TOPICS;
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly createdShareCode = signal<string | null>(null);
  private createdRoomId: string | null = null;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(40)]],
    topic: ['Math' as RoomTopic, [Validators.required]],
    maxPlayers: [6, [Validators.required, Validators.min(2), Validators.max(10)]],
    minPlayersToStart: [2, [Validators.required, Validators.min(2)]],
    questionCount: [10, [Validators.required, Validators.min(5), Validators.max(20)]],
    secondsPerQuestion: [20, [Validators.required, Validators.min(10), Validators.max(60)]],
    isPrivate: [false]
  });

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.errorMessage.set(null);
    this.submitting.set(true);

    this.roomService.create(this.form.getRawValue()).subscribe({
      next: (room) => {
        this.submitting.set(false);
        this.createdRoomId = room.id;
        if (room.isPrivate && room.shareCode) {
          this.createdShareCode.set(room.shareCode);
        } else {
          this.created.emit(room.id);
        }
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.error?.title ?? null);
      }
    });
  }

  continueToRoom(): void {
    if (this.createdRoomId) {
      this.created.emit(this.createdRoomId);
    }
  }

  cancel(): void {
    this.cancelled.emit();
  }
}
