import { Component, input } from '@angular/core';
import { CameraStatus } from '../models';

@Component({
  selector: 'app-status-pill',
  template: `
    <span class="pill" [class]="tone()">
      <i class="dot" [class]="tone()"></i>
      {{ label() }}
    </span>
  `,
})
export class StatusPill {
  readonly status = input.required<CameraStatus>();
  readonly enabled = input(true);
  readonly recordingEnabled = input(true);

  protected tone(): string {
    return this.enabled() && !this.recordingEnabled() ? 'live' : this.status();
  }

  protected label(): string {
    if (this.enabled() && !this.recordingEnabled()) {
      return 'Live only';
    }

    if (this.status() === 'recording') {
      return 'Recording';
    }

    return this.status() === 'online' ? 'Online' : 'Offline';
  }
}
