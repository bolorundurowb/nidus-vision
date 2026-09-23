import { Injectable, computed, signal } from '@angular/core';
import { CAMERA_SEED, CameraItem } from './models';

@Injectable({ providedIn: 'root' })
export class CameraStore {
  readonly cameras = signal<CameraItem[]>(CAMERA_SEED);
  readonly recordingCount = computed(() => this.cameras().filter(c => c.status === 'recording').length);

  addCamera(name: string, url: string): void {
    const id = String(this.cameras().length + 1);
    this.cameras.update(list => [
      ...list,
      {
        id,
        name: name || 'New Camera',
        status: 'offline',
        resolution: '1920×1080',
        fps: 25,
        bitrate: '—',
        retention: '—',
        location: 'Unassigned',
        mainRtspUrl: url || `rtsp://192.168.1.${20 + Number(id)}:554/stream`,
      },
    ]);
  }
}
