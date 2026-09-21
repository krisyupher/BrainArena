import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateRoomRequest, RoomDetail, RoomSummary } from '../models/room.model';
import { MatchResultsDto } from '../models/match.model';

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

  create(request: CreateRoomRequest): Observable<RoomDetail> {
    return this.http.post<RoomDetail>('/api/rooms', request);
  }

  join(id: string): Observable<RoomDetail> {
    return this.http.post<RoomDetail>(`/api/rooms/${id}/join`, {});
  }

  getResults(id: string): Observable<MatchResultsDto> {
    return this.http.get<MatchResultsDto>(`/api/rooms/${id}/results`);
  }
}
