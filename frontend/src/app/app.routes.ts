import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'lobby' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login)
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register)
  },
  {
    // Browsable without an account — only creating/joining a room as a player requires login,
    // enforced server-side and reflected in this page's own UI gating.
    path: 'lobby',
    loadComponent: () => import('./features/lobby/lobby').then((m) => m.Lobby)
  },
  {
    // Viewable without an account (spectator mode) — playing still requires login, enforced
    // server-side and reflected in this page's own UI gating (see Room.isParticipant).
    path: 'rooms/:id',
    loadComponent: () => import('./features/room/room').then((m) => m.Room)
  },
  {
    path: 'rooms/:id/play',
    loadComponent: () => import('./features/room/match-play/match-play').then((m) => m.MatchPlay)
  },
  {
    path: 'rooms/:id/results',
    canActivate: [authGuard],
    loadComponent: () => import('./features/room/match-results/match-results').then((m) => m.MatchResults)
  },
  {
    // Browsable without an account, same rule as the room lobby — joining/starting still needs login.
    path: 'tournaments',
    loadComponent: () => import('./features/tournaments/tournament-lobby/tournament-lobby').then((m) => m.TournamentLobby)
  },
  {
    path: 'tournaments/:id',
    loadComponent: () =>
      import('./features/tournaments/tournament-detail/tournament-detail').then((m) => m.TournamentDetail)
  },
  {
    path: 'admin',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/admin').then((m) => m.Admin)
  },
  { path: '**', redirectTo: 'lobby' }
];
