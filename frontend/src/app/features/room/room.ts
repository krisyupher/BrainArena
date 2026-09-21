import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { RoomService } from '../../core/services/room.service';
import { RoomHubService } from '../../core/services/room-hub.service';
import { RoomDetail } from '../../core/models/room.model';
import { Chat } from './chat/chat';

@Component({
  imports: [TranslocoPipe, Chat],
  selector: 'app-room',
  styleUrl: './room.scss',
  templateUrl: './room.html'
})
export class Room implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly roomService = inject(RoomService);
  private readonly roomHub = inject(RoomHubService);
  protected readonly auth = inject(AuthService);

  readonly room = signal<RoomDetail | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly startingMatch = signal(false);

  protected roomId!: string;
  private readonly subscriptions = new Subscription();
  /** True once we're either going into the match (must stay in the group) or have already left it explicitly. */
  private skipLeaveOnDestroy = false;

  ngOnInit(): void {
    this.roomId = this.route.snapshot.paramMap.get('id')!;

    this.loadRoom();
    this.roomHub.joinRoomGroup(this.roomId);

    this.subscriptions.add(this.roomHub.roomUpdated.subscribe(() => this.loadRoom()));
    // A match can already be running by the time our own JoinRoomGroup call lands — e.g. when
    // *this* player's own join is what filled the room and triggered auto-start, the server's
    // MatchStarting broadcast fires inside that REST request, before we've joined the SignalR
    // group to hear it. JoinRoomGroup responds with a resync in that case, so treat either event
    // as "go to match play" rather than relying on MatchStarting alone.
    this.subscriptions.add(this.roomHub.matchStarting.subscribe(() => this.goToMatch()));
    this.subscriptions.add(this.roomHub.matchResync.subscribe(() => this.goToMatch()));
    this.subscriptions.add(this.roomHub.matchStartFailed.subscribe((message) => this.errorMessage.set(message)));
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    if (!this.skipLeaveOnDestroy) {
      this.roomHub.leaveRoomGroup(this.roomId);
    }
  }

  get isHost(): boolean {
    const room = this.room();
    const userId = this.auth.currentUser()?.userId;
    return !!room && !!userId && room.hostUserId === userId;
  }

  get canStartNow(): boolean {
    const room = this.room();
    return !!room && room.status === 'Waiting' && room.players.length >= room.minPlayersToStart;
  }

  startNow(): void {
    this.errorMessage.set(null);
    this.startingMatch.set(true);
    this.roomHub
      .startNow(this.roomId)
      .catch((err: Error) => this.errorMessage.set(err?.message ?? null))
      .finally(() => this.startingMatch.set(false));
  }

  leave(): void {
    this.skipLeaveOnDestroy = true;
    this.roomHub.leaveRoomGroup(this.roomId).finally(() => this.router.navigateByUrl('/lobby'));
  }

  private loadRoom(): void {
    this.roomService.getById(this.roomId).subscribe((room) => {
      if (room.status === 'Finished') {
        this.skipLeaveOnDestroy = true;
        this.router.navigate(['/rooms', this.roomId, 'results']);
        return;
      }
      this.room.set(room);
    });
  }

  private goToMatch(): void {
    if (this.skipLeaveOnDestroy) {
      return; // already navigating
    }
    this.skipLeaveOnDestroy = true;
    this.router.navigate(['/rooms', this.roomId, 'play']);
  }
}
