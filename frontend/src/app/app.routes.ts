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
    path: 'lobby',
    canActivate: [authGuard],
    loadComponent: () => import('./features/lobby/lobby').then((m) => m.Lobby)
  },
  {
    path: 'rooms/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./features/room/room').then((m) => m.Room)
  },
  {
    path: 'rooms/:id/play',
    canActivate: [authGuard],
    loadComponent: () => import('./features/room/match-play/match-play').then((m) => m.MatchPlay)
  },
  {
    path: 'rooms/:id/results',
    canActivate: [authGuard],
    loadComponent: () => import('./features/room/match-results/match-results').then((m) => m.MatchResults)
  },
  {
    path: 'admin',
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./features/admin/admin').then((m) => m.Admin)
  },
  { path: '**', redirectTo: 'lobby' }
];
