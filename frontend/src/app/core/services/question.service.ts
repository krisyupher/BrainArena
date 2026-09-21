import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { QuestionDto, QuestionImportResult, QuestionUpsertRequest } from '../models/question.model';
import { RoomTopic } from '../models/room.model';

@Injectable({ providedIn: 'root' })
export class QuestionService {
  private readonly http = inject(HttpClient);

  getAll(topic?: RoomTopic): Observable<QuestionDto[]> {
    const params = topic ? { params: { topic } } : {};
    return this.http.get<QuestionDto[]>('/api/questions', params);
  }

  create(request: QuestionUpsertRequest): Observable<QuestionDto> {
    return this.http.post<QuestionDto>('/api/questions', request);
  }

  update(id: string, request: QuestionUpsertRequest): Observable<QuestionDto> {
    return this.http.put<QuestionDto>(`/api/questions/${id}`, request);
  }

  import(requests: QuestionUpsertRequest[]): Observable<QuestionImportResult> {
    return this.http.post<QuestionImportResult>('/api/questions/import', requests);
  }
}
