import { Component, input } from '@angular/core';
import { CameraStatus } from '../models';

@Component({
  selector: 'app-status-pill',
  template: `
    <span class="pill" [class]="status()">
      <i class="dot" [class]="status()"></i>
      {{ status() === 'recording' ? 'Recording' : status() === 'online' ? 'Online' : 'Offline' }}
    </span>
  `,
})
export class StatusPill {
  readonly status = input.required<CameraStatus>();
}
