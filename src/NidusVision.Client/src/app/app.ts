import { Component, inject, signal, viewChild } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { CameraStore } from './camera.store';
import { CameraApi, ProbeResult, cameraWrite } from './api/camera.api';
import { CameraForm } from './camera-form';
import { AddCameraDialog } from './add-camera.dialog';
import { PageId } from './models';
import { AppIcon, AppIconName } from './ui/app-icon';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, AppIcon, CameraForm],
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
  private readonly addForm = viewChild(CameraForm);
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

  protected async addCamera(): Promise<void> {
    const form = this.addForm()?.value();
    if (!form) {
      return;
    }
    const ok = await this.store.addCamera(cameraWrite(form));
    if (!ok) {
      this.probeResult.set({ ok: false, message: 'Could not save the camera. Check the server connection and try again.' });
      return;
    }
    this.addDialog.open.set(false);
    this.probeResult.set(null);
    void this.router.navigateByUrl('/cameras');
  }

  protected async testConnection(): Promise<void> {
    const form = this.addForm()?.value();
    if (!form) {
      return;
    }
    this.probing.set(true);
    this.probeResult.set(null);
    try {
      this.probeResult.set(await this.cameraApi.probe(cameraWrite({ ...form, name: form.name || 'Probe' })));
    } catch {
      this.probeResult.set({ ok: false, message: 'Could not reach the server to test this camera.' });
    } finally {
      this.probing.set(false);
    }
  }
}
