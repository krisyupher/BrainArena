import { Injectable, inject } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from './auth.service';
import {
  MatchEndedEvent,
  MatchResyncEvent,
  MatchSpectatorSyncEvent,
  QuestionRevealedEvent,
  QuestionStartedEvent
} from '../models/match.model';
import { ChatMessageDto } from '../models/chat.model';
import { ReactionSentEvent } from '../models/reaction.model';

/**
 * Wraps the single SignalR hub connection shared app-wide by the lobby, waiting room, match
 * flow, and chat. The connection itself is owned by the root App component (connected once the
 * user is authenticated, disconnected on logout) — this service just exposes typed events and hub
 * method calls; components join/leave specific room groups as they navigate.
 */
@Injectable({ providedIn: 'root' })
export class RoomHubService {
  private readonly auth = inject(AuthService);
  private connection: signalR.HubConnection | null = null;
  private connectPromise: Promise<void> | null = null;

  readonly roomListChanged = new Subject<void>();
  readonly roomUpdated = new Subject<void>();
  readonly matchStarting = new Subject<{ matchId: string; countdownSeconds: number }>();
  readonly matchStartFailed = new Subject<string>();
  readonly questionStarted = new Subject<QuestionStartedEvent>();
  readonly answerAccepted = new Subject<string>();
  readonly questionRevealed = new Subject<QuestionRevealedEvent>();
  readonly matchEnded = new Subject<MatchEndedEvent>();
  readonly matchResync = new Subject<MatchResyncEvent>();
  readonly matchSpectatorSync = new Subject<MatchSpectatorSyncEvent>();
  readonly chatMessageReceived = new Subject<ChatMessageDto>();
  readonly chatMessageReported = new Subject<string>();
  readonly reactionSent = new Subject<ReactionSentEvent>();
  /** Every connection auto-joins the "tournament-lobby" group server-side, so this fires for any open/in-progress tournament change without an explicit join call. */
  readonly tournamentListChanged = new Subject<void>();
  /** Payload-free, like roomUpdated — fires for whichever tournament(s) this connection has joined via joinTournamentGroup. */
  readonly tournamentUpdated = new Subject<void>();

  connect(): Promise<void> {
    if (this.connection) {
      return Promise.resolve();
    }
    if (this.connectPromise) {
      return this.connectPromise;
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/room', { accessTokenFactory: () => this.auth.token() ?? '' })
      .withAutomaticReconnect()
      .build();

    connection.on('RoomListChanged', () => this.roomListChanged.next());
    connection.on('RoomUpdated', () => this.roomUpdated.next());
    connection.on('MatchStarting', (matchId: string, countdownSeconds: number) =>
      this.matchStarting.next({ matchId, countdownSeconds })
    );
    connection.on('MatchStartFailed', (message: string) => this.matchStartFailed.next(message));
    connection.on('QuestionStarted', (payload: QuestionStartedEvent) => this.questionStarted.next(payload));
    connection.on('AnswerAccepted', (matchQuestionId: string) => this.answerAccepted.next(matchQuestionId));
    connection.on('QuestionRevealed', (payload: QuestionRevealedEvent) => this.questionRevealed.next(payload));
    connection.on('MatchEnded', (payload: MatchEndedEvent) => this.matchEnded.next(payload));
    connection.on('MatchResync', (payload: MatchResyncEvent) => this.matchResync.next(payload));
    connection.on('MatchSpectatorSync', (payload: MatchSpectatorSyncEvent) => this.matchSpectatorSync.next(payload));
    connection.on('ChatMessageReceived', (payload: ChatMessageDto) => this.chatMessageReceived.next(payload));
    connection.on('ChatMessageReported', (messageId: string) => this.chatMessageReported.next(messageId));
    connection.on('ReactionSent', (payload: ReactionSentEvent) => this.reactionSent.next(payload));
    connection.on('TournamentListChanged', () => this.tournamentListChanged.next());
    connection.on('TournamentUpdated', () => this.tournamentUpdated.next());

    this.connectPromise = connection
      .start()
      .then(() => {
        this.connection = connection;
        this.connectPromise = null;
      })
      .catch((err) => {
        this.connectPromise = null;
        throw err;
      });

    return this.connectPromise;
  }

  async disconnect(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    await connection?.stop();
  }

  async joinRoomGroup(roomId: string): Promise<void> {
    await this.connect();
    await this.connection!.invoke('JoinRoomGroup', roomId);
  }

  async leaveRoomGroup(roomId: string): Promise<void> {
    if (!this.connection) {
      return;
    }
    await this.connection.invoke('LeaveRoomGroup', roomId);
  }

  /** Anonymous-safe alternative to joinRoomGroup for a visitor watching without playing. */
  async joinAsSpectator(roomId: string): Promise<void> {
    await this.connect();
    await this.connection!.invoke('JoinAsSpectator', roomId);
  }

  async leaveAsSpectator(roomId: string): Promise<void> {
    if (!this.connection) {
      return;
    }
    await this.connection.invoke('LeaveSpectatorGroup', roomId);
  }

  startNow(roomId: string): Promise<void> {
    return this.connection!.invoke('StartNow', roomId);
  }

  submitOptionAnswer(roomId: string, matchQuestionId: string, selectedOptionIndex: number): Promise<void> {
    return this.connection!.invoke('SubmitAnswer', roomId, matchQuestionId, selectedOptionIndex, null);
  }

  submitNumericAnswer(roomId: string, matchQuestionId: string, numericAnswer: number): Promise<void> {
    return this.connection!.invoke('SubmitAnswer', roomId, matchQuestionId, null, numericAnswer);
  }

  sendChatMessage(roomId: string, text: string): Promise<void> {
    return this.connection!.invoke('SendChatMessage', roomId, text);
  }

  reportChatMessage(messageId: string): Promise<void> {
    return this.connection!.invoke('ReportChatMessage', messageId);
  }

  sendReaction(roomId: string, targetUserId: string, emoji: string): Promise<void> {
    return this.connection!.invoke('SendReaction', roomId, targetUserId, emoji);
  }

  async joinTournamentGroup(tournamentId: string): Promise<void> {
    await this.connect();
    await this.connection!.invoke('JoinTournamentGroup', tournamentId);
  }

  async leaveTournamentGroup(tournamentId: string): Promise<void> {
    if (!this.connection) {
      return;
    }
    await this.connection.invoke('LeaveTournamentGroup', tournamentId);
  }
}
