import { Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { CameraStore } from './camera.store';
import { AddCameraDialog } from './add-camera.dialog';
import { PageId } from './models';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly router = inject(Router);
  protected readonly store = inject(CameraStore);
  protected readonly addDialog = inject(AddCameraDialog);
  protected readonly sidebarOpen = signal(true);
  protected readonly page = signal<PageId>('monitor');
  protected readonly nav = [
    { id: 'monitor' as const, label: 'Monitor Center', path: '/monitor' },
    { id: 'events' as const, label: 'Events & Library', path: '/events' },
    { id: 'cameras' as const, label: 'IP Cameras', path: '/cameras' },
    { id: 'settings' as const, label: 'Settings', path: '/settings' },
  ];

  constructor() {
    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd)).subscribe(e => {
      const id = this.nav.find(n => e.urlAfterRedirects.startsWith(n.path))?.id ?? 'monitor';
      this.page.set(id);
    });
  }

  protected current() {
    return this.nav.find(n => n.id === this.page()) ?? this.nav[0];
  }

  protected addCamera(name: string, url: string): void {
    this.store.addCamera(name, url);
    this.addDialog.open.set(false);
    void this.router.navigateByUrl('/cameras');
  }
}
