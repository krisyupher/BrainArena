import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AuthResponse } from '../models/user.model';

const STORAGE_KEY = 'brainarena.auth';

interface StoredAuth {
  token: string;
  userId: string;
  displayName: string;
  role: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly authState = signal<StoredAuth | null>(readStoredAuth());

  readonly currentUser = computed(() => this.authState());
  readonly isAuthenticated = computed(() => this.authState() !== null);
  readonly isAdmin = computed(() => this.authState()?.role === 'Admin');
  readonly token = computed(() => this.authState()?.token ?? null);

  register(email: string, password: string, displayName: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/register', { email, password, displayName })
      .pipe(tap((response) => this.applyAuth(response)));
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/login', { email, password })
      .pipe(tap((response) => this.applyAuth(response)));
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.authState.set(null);
    this.router.navigateByUrl('/login');
  }

  private applyAuth(response: AuthResponse): void {
    const stored: StoredAuth = {
      token: response.token,
      userId: response.userId,
      displayName: response.displayName,
      role: response.role
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
    this.authState.set(stored);
  }
}

function readStoredAuth(): StoredAuth | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as StoredAuth) : null;
  } catch {
    return null;
  }
}
