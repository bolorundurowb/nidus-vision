import { Injectable, computed, inject, signal } from '@angular/core';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { CameraApi, CameraWrite, cameraWrite } from './api/camera.api';
import { CameraItem, CameraStatus } from './models';

@Injectable({ providedIn: 'root' })
export class CameraStore {
  private readonly api = inject(CameraApi);
  readonly cameras = signal<CameraItem[]>([]);
  readonly loaded = signal(false);
  /** True after the last list request failed; the list is left empty rather than guessed. */
  readonly loadFailed = signal(false);
  readonly recordingCount = computed(() => this.cameras().filter(c => c.status === 'recording').length);
  readonly selectedId = signal<string | null>(null);

  constructor() {
    this.connectHub();
  }

  private connectHub(): void {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/status')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on('status', (msg: { cameraId: string; status: CameraStatus; recordingCount: number }) => {
      this.cameras.update(list => list.map(c => c.id === msg.cameraId ? { ...c, status: msg.status } : c));
    });
    void connection.start().catch(() => undefined);
  }

  async refresh(): Promise<boolean> {
    try {
      const list = await this.api.list();
      this.cameras.set(list.map(c => this.api.toItem(c)));
      this.loadFailed.set(false);
      return true;
    } catch {
      this.cameras.set([]);
      this.loadFailed.set(true);
      return false;
    } finally {
      this.loaded.set(true);
    }
  }

  async addCamera(name: string, url: string, location: string, enabled: boolean, username?: string, password?: string): Promise<boolean> {
    const body: CameraWrite = cameraWrite({ name, url, location, enabled, username, password });
    try {
      const created = await this.api.create(body);
      this.cameras.update(list => [...list, this.api.toItem(created)]);
      return true;
    } catch {
      return false;
    }
  }
}
