import { Injectable, signal } from '@angular/core';
import { HubConnectionBuilder } from '@microsoft/signalr';

export interface DetectionToast {
  eventId: string;
  cameraName: string;
  confidence: number;
  at: string;
}

@Injectable({ providedIn: 'root' })
export class DetectionAlerts {
  readonly items = signal<DetectionToast[]>([]);
  readonly open = signal(false);

  constructor() {
    const connection = new HubConnectionBuilder().withUrl('/hubs/detections').withAutomaticReconnect().build();
    connection.on('alert', (msg: DetectionToast) => {
      this.items.update(list => [msg, ...list].slice(0, 20));
      this.open.set(true);
    });
    void connection.start().catch(() => undefined);
  }
}
