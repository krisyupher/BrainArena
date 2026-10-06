import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateRoomRequest, RoomDetail, RoomSummary } from '../models/room.model';
import { MatchResultsDto } from '../models/match.model';
import { AuthResponse } from '../models/user.model';

/**
 * guestAuth is non-null only when an anonymous caller created a Solitary room — RoomService
 * auto-created a throwaway guest account server-side and this carries its freshly-minted session.
 * Null for every other (already-authenticated) create.
 */
export interface CreateRoomResult {
  room: RoomDetail;
  guestAuth: AuthResponse | null;
}

@Injectable({ providedIn: 'root' })
export class RoomService {
  private readonly http = inject(HttpClient);

  getOpenRooms(): Observable<RoomSummary[]> {
    return this.http.get<RoomSummary[]>('/api/rooms');
  }

  getById(id: string): Observable<RoomDetail> {
    return this.http.get<RoomDetail>(`/api/rooms/${id}`);
  }

  getByShareCode(code: string): Observable<RoomDetail> {
    return this.http.get<RoomDetail>(`/api/rooms/by-code/${code}`);
  }

  create(request: CreateRoomRequest): Observable<CreateRoomResult> {
    return this.http.post<CreateRoomResult>('/api/rooms', request);
  }

  join(id: string): Observable<RoomDetail> {
    return this.http.post<RoomDetail>(`/api/rooms/${id}/join`, {});
  }

  getResults(id: string): Observable<MatchResultsDto> {
    return this.http.get<MatchResultsDto>(`/api/rooms/${id}/results`);
  }
}
