import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { RoomService } from '../../core/services/room.service';
import { RoomHubService } from '../../core/services/room-hub.service';
import { RoomSummary } from '../../core/models/room.model';
import { CreateRoomForm } from './create-room-form/create-room-form';

@Component({
  imports: [TranslocoPipe, CreateRoomForm],
  selector: 'app-lobby',
  styleUrl: './lobby.scss',
  templateUrl: './lobby.html'
})
export class Lobby implements OnInit, OnDestroy {
  private readonly roomService = inject(RoomService);
  private readonly roomHub = inject(RoomHubService);
  private readonly router = inject(Router);

  readonly rooms = signal<RoomSummary[]>([]);
  readonly loading = signal(true);
  readonly showCreateForm = signal(false);

  private hubSubscription?: Subscription;

  ngOnInit(): void {
    this.loadRooms();
    this.hubSubscription = this.roomHub.roomListChanged.subscribe(() => this.loadRooms());
  }

  ngOnDestroy(): void {
    this.hubSubscription?.unsubscribe();
  }

  join(room: RoomSummary): void {
    this.roomService.join(room.id).subscribe(() => this.router.navigate(['/rooms', room.id]));
  }

  onRoomCreated(roomId: string): void {
    this.showCreateForm.set(false);
    this.router.navigate(['/rooms', roomId]);
  }

  private loadRooms(): void {
    this.roomService.getOpenRooms().subscribe({
      next: (rooms) => {
        this.rooms.set(rooms);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
