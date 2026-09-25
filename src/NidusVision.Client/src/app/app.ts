import { Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { CameraStore } from './camera.store';
import { CameraApi, ProbeResult, cameraWrite } from './api/camera.api';
import { AddCameraDialog } from './add-camera.dialog';
import { PageId } from './models';
import { AppIcon, AppIconName } from './ui/app-icon';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, AppIcon],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly router = inject(Router);
  protected readonly store = inject(CameraStore);
  protected readonly addDialog = inject(AddCameraDialog);
  protected readonly sidebarOpen = signal(true);
  protected readonly page = signal<PageId>('monitor');
  protected readonly loginScreen = signal(false);
  protected readonly probing = signal(false);
  protected readonly probeResult = signal<ProbeResult | null>(null);
  private readonly cameraApi = inject(CameraApi);
  protected readonly nav: ReadonlyArray<{ id: PageId; label: string; path: string; icon: AppIconName }> = [
    { id: 'monitor', label: 'Monitor Center', path: '/monitor', icon: 'layout-grid' },
    { id: 'recordings', label: 'Recordings', path: '/recordings', icon: 'archive' },
    { id: 'cameras', label: 'IP Cameras', path: '/cameras', icon: 'camera' },
    { id: 'settings', label: 'Settings', path: '/settings', icon: 'settings' },
  ];

  constructor() {
    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd)).subscribe(e => {
      const login = e.urlAfterRedirects.startsWith('/login');
      this.loginScreen.set(login);
      const id = this.nav.find(n => e.urlAfterRedirects.startsWith(n.path))?.id ?? 'monitor';
      this.page.set(id);
      if (!login) {
        void this.store.refresh();
      }
    });
  }

  protected current() {
    return this.nav.find(n => n.id === this.page()) ?? this.nav[0];
  }

  protected async addCamera(name: string, url: string, location: string, enabled: boolean, username: string, password: string): Promise<void> {
    const ok = await this.store.addCamera(name, url, location, enabled, username, password);
    if (!ok) {
      this.probeResult.set({ ok: false, message: 'Could not save the camera. Check the server connection and try again.' });
      return;
    }
    this.addDialog.open.set(false);
    this.probeResult.set(null);
    void this.router.navigateByUrl('/cameras');
  }

  protected async testConnection(url: string, location: string, enabled: boolean, username: string, password: string): Promise<void> {
    this.probing.set(true);
    this.probeResult.set(null);
    try {
      this.probeResult.set(await this.cameraApi.probe(cameraWrite({ name: 'Probe', url, location, enabled, username, password })));
    } catch {
      this.probeResult.set({ ok: false, message: 'Could not reach the server to test this camera.' });
    } finally {
      this.probing.set(false);
    }
  }
}
