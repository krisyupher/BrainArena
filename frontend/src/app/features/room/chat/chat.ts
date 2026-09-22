import { Component, OnDestroy, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { ChatService } from '../../../core/services/chat.service';
import { RoomHubService } from '../../../core/services/room-hub.service';
import { AuthService } from '../../../core/services/auth.service';
import { ChatMessageDto } from '../../../core/models/chat.model';

/**
 * Waiting-room and results-screen chat only — never mounted on the match-play page, which is how
 * "disabled during questions" is enforced client-side (the server also rejects sends while a
 * match is in progress, as defense in depth).
 */
@Component({
  imports: [ReactiveFormsModule, TranslocoPipe],
  selector: 'app-chat',
  styleUrl: './chat.scss',
  templateUrl: './chat.html'
})
export class Chat implements OnInit, OnDestroy {
  readonly roomId = input.required<string>();
  /** False for a spectator: the feed still renders, but the send form is hidden. */
  readonly canSend = input(true);

  private readonly chatService = inject(ChatService);
  private readonly roomHub = inject(RoomHubService);
  private readonly fb = inject(FormBuilder);
  protected readonly auth = inject(AuthService);

  readonly messages = signal<ChatMessageDto[]>([]);
  readonly errorMessage = signal<string | null>(null);
  private readonly locallyReportedIds = signal<ReadonlySet<string>>(new Set());

  readonly form = this.fb.nonNullable.group({
    text: ['', [Validators.required, Validators.maxLength(200)]]
  });

  private readonly subscriptions = new Subscription();

  ngOnInit(): void {
    this.chatService.getHistory(this.roomId()).subscribe((messages) => this.messages.set(messages));

    this.subscriptions.add(
      this.roomHub.chatMessageReceived.subscribe((message) => {
        this.messages.update((list) => [...list, message]);
      })
    );

    this.subscriptions.add(
      this.roomHub.chatMessageReported.subscribe((messageId) => {
        this.locallyReportedIds.update((set) => new Set(set).add(messageId));
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  send(): void {
    if (this.form.invalid) {
      return;
    }

    this.errorMessage.set(null);
    const text = this.form.getRawValue().text;

    this.roomHub
      .sendChatMessage(this.roomId(), text)
      .then(() => this.form.reset())
      .catch((err: Error) => this.errorMessage.set(err?.message ?? null));
  }

  report(messageId: string): void {
    this.roomHub.reportChatMessage(messageId).catch(() => {
      // Reporting is best-effort; a failed report isn't worth surfacing an error for.
    });
  }

  isReported(message: ChatMessageDto): boolean {
    return message.isReported || this.locallyReportedIds().has(message.id);
  }
}
