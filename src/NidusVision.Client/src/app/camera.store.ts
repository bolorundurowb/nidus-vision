import { Injectable, computed, inject, signal } from '@angular/core';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { CameraApi, CameraWrite, cameraWrite } from './api/camera.api';
import { CAMERA_SEED, CameraItem, CameraStatus } from './models';
import { redactRtspUrl } from './rtsp-url';

@Injectable({ providedIn: 'root' })
export class CameraStore {
  private readonly api = inject(CameraApi);
  readonly cameras = signal<CameraItem[]>(CAMERA_SEED);
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
      return true;
    } catch {
      // Keep mock seed when the API is offline (ng serve without backend).
      return false;
    }
  }

  async addCamera(name: string, url: string, username?: string, password?: string): Promise<void> {
    const body: CameraWrite = cameraWrite({ name, url, username, password });
    try {
      const created = await this.api.create(body);
      this.cameras.update(list => [...list, this.api.toItem(created)]);
    } catch {
      this.cameras.update(list => [
        ...list,
        {
          id: crypto.randomUUID(),
          name: body.name,
          status: 'offline',
          resolution: '1920×1080',
          fps: 25,
          bitrate: '—',
          retention: '—',
          location: body.location,
          mainRtspUrl: redactRtspUrl(body.mainRtspUrl),
        },
      ]);
    }
  }
}
