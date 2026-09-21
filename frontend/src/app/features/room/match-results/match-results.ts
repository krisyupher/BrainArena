import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../../core/services/auth.service';
import { RoomService } from '../../../core/services/room.service';
import { RoomHubService } from '../../../core/services/room-hub.service';
import { MatchResultsDto } from '../../../core/models/match.model';
import { Chat } from '../chat/chat';

@Component({
  imports: [TranslocoPipe, RouterLink, Chat],
  selector: 'app-match-results',
  styleUrl: './match-results.scss',
  templateUrl: './match-results.html'
})
export class MatchResults implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly roomService = inject(RoomService);
  private readonly roomHub = inject(RoomHubService);
  protected readonly auth = inject(AuthService);

  readonly results = signal<MatchResultsDto | null>(null);
  readonly loading = signal(true);
  protected roomId!: string;

  ngOnInit(): void {
    this.roomId = this.route.snapshot.paramMap.get('id')!;

    // Joins this room's SignalR group so chat (waiting room + results screen only) works live here too.
    this.roomHub.joinRoomGroup(this.roomId);

    this.roomService.getResults(this.roomId).subscribe({
      next: (results) => {
        this.results.set(results);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
