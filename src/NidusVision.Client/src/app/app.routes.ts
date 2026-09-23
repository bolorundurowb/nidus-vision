import { Routes } from '@angular/router';
import { authGuard } from './auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login.page').then(m => m.LoginPage) },
  { path: '', pathMatch: 'full', redirectTo: 'monitor' },
  { path: 'monitor', canActivate: [authGuard], loadComponent: () => import('./pages/monitor.page').then(m => m.MonitorPage) },
  { path: 'events', canActivate: [authGuard], loadComponent: () => import('./pages/events.page').then(m => m.EventsPage) },
  { path: 'cameras', canActivate: [authGuard], loadComponent: () => import('./pages/cameras.page').then(m => m.CamerasPage) },
  { path: 'settings', canActivate: [authGuard], loadComponent: () => import('./pages/settings.page').then(m => m.SettingsPage) },
  { path: '**', redirectTo: 'monitor' },
];
