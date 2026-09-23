import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AddCameraDialog {
  readonly open = signal(false);
}
