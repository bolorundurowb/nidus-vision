import { Injectable, computed, inject, signal } from '@angular/core';
import { CameraApi, CameraWrite } from './api/camera.api';
import { CAMERA_SEED, CameraItem } from './models';

@Injectable({ providedIn: 'root' })
export class CameraStore {
  private readonly api = inject(CameraApi);
  readonly cameras = signal<CameraItem[]>(CAMERA_SEED);
  readonly recordingCount = computed(() => this.cameras().filter(c => c.status === 'recording').length);
  readonly selectedId = signal<string | null>(null);

  async refresh(): Promise<void> {
    try {
      const list = await this.api.list();
      this.cameras.set(list.map(c => this.api.toItem(c)));
    } catch {
      // Keep mock seed when the API is offline (ng serve without backend).
    }
  }

  async addCamera(name: string, url: string): Promise<void> {
    const body: CameraWrite = {
      name: name || 'New Camera',
      location: 'Unassigned',
      enabled: true,
      mainRtspUrl: url || 'rtsp://192.168.1.20:554/stream',
      subRtspUrl: null,
      username: null,
      password: null,
      transport: 'tcp',
      roiJson: null,
    };
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
          mainRtspUrl: body.mainRtspUrl,
        },
      ]);
    }
  }
}
