import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'monitor' },
  { path: 'monitor', loadComponent: () => import('./pages/monitor.page').then(m => m.MonitorPage) },
  { path: 'events', loadComponent: () => import('./pages/events.page').then(m => m.EventsPage) },
  { path: 'cameras', loadComponent: () => import('./pages/cameras.page').then(m => m.CamerasPage) },
  { path: 'settings', loadComponent: () => import('./pages/settings.page').then(m => m.SettingsPage) },
  { path: '**', redirectTo: 'monitor' },
];
