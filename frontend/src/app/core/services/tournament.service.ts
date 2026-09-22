import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateTournamentRequest, TournamentDetail, TournamentSummary } from '../models/tournament.model';

@Injectable({ providedIn: 'root' })
export class TournamentService {
  private readonly http = inject(HttpClient);

  getOpenTournaments(): Observable<TournamentSummary[]> {
    return this.http.get<TournamentSummary[]>('/api/tournaments');
  }

  getById(id: string): Observable<TournamentDetail> {
    return this.http.get<TournamentDetail>(`/api/tournaments/${id}`);
  }

  create(request: CreateTournamentRequest): Observable<TournamentDetail> {
    return this.http.post<TournamentDetail>('/api/tournaments', request);
  }

  join(id: string): Observable<TournamentDetail> {
    return this.http.post<TournamentDetail>(`/api/tournaments/${id}/join`, {});
  }

  leave(id: string): Observable<void> {
    return this.http.post<void>(`/api/tournaments/${id}/leave`, {});
  }

  start(id: string): Observable<void> {
    return this.http.post<void>(`/api/tournaments/${id}/start`, {});
  }
}
