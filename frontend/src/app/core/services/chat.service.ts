import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ChatMessageDto } from '../models/chat.model';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);

  getHistory(roomId: string): Observable<ChatMessageDto[]> {
    return this.http.get<ChatMessageDto[]>(`/api/rooms/${roomId}/chat`);
  }
}
