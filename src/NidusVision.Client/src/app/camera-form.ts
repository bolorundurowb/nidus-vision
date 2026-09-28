import { Component, effect, input, signal } from '@angular/core';

export interface CameraFormValue {
  name: string;
  url: string;
  location: string;
  enabled: boolean;
  recordingEnabled: boolean;
  transport: string;
  username: string;
  password: string;
}

export function emptyCameraForm(): CameraFormValue {
  return {
    name: '',
    url: '',
    location: 'Interior',
    enabled: true,
    recordingEnabled: true,
    transport: 'tcp',
    username: '',
    password: '',
  };
}

@Component({
  selector: 'app-camera-form',
  template: `
    <section class="form-section">
      <p class="section-label">Identity</p>
      <div class="stack">
        <label>Camera name<input class="input" [value]="name()" (input)="name.set($any($event.target).value)" placeholder="e.g. Side Gate"></label>
        <label>RTSP stream URL<input class="input mono" [value]="url()" (input)="url.set($any($event.target).value)" placeholder="rtsp://192.168.1.20:554/stream"></label>
      </div>
    </section>
    <div class="rule"></div>
    <section class="form-section">
      <p class="section-label">Network</p>
      <div class="field-pair">
        <label>Location
          <span class="select-wrap">
            <select class="input" [value]="location()" (change)="location.set($any($event.target).value)">
              <option value="Interior">Interior</option>
              <option value="Exterior">Exterior</option>
            </select>
          </span>
        </label>
        <label>Transport
          <span class="select-wrap">
            <select class="input" [value]="transport()" (change)="transport.set($any($event.target).value)">
              <option value="tcp">TCP</option>
              <option value="udp">UDP</option>
            </select>
          </span>
        </label>
      </div>
    </section>
    <div class="rule"></div>
    <section class="form-section">
      <p class="section-label">Behaviour</p>
      <div class="stack">
        <div class="option-row">
          <div>
            <span class="option-title">Camera enabled</span>
            <p>When off, this camera is inactive, including live view.</p>
          </div>
          <button type="button" class="switch" role="switch" [class.on]="enabled()" [attr.aria-checked]="enabled()" aria-label="Camera enabled" (click)="enabled.set(!enabled())"><span></span></button>
        </div>
        <div class="option-row">
          <div>
            <span class="option-title">Record continuously</span>
            <p>When off, live view stays available and new footage is not saved.</p>
          </div>
          <button type="button" class="switch" role="switch" [class.on]="recordingEnabled()" [attr.aria-checked]="recordingEnabled()" aria-label="Record continuously" (click)="recordingEnabled.set(!recordingEnabled())"><span></span></button>
        </div>
      </div>
    </section>
    <div class="rule"></div>
    <section class="form-section">
      <p class="section-label">Credentials</p>
      <div class="field-pair">
        <label>Username<input class="input" autocomplete="off" [value]="username()" (input)="username.set($any($event.target).value)" placeholder="Optional"></label>
        <label>Password<input class="input" type="password" autocomplete="new-password" [value]="password()" (input)="password.set($any($event.target).value)" placeholder="Optional"></label>
      </div>
      <ng-content />
    </section>
  `,
})
export class CameraForm {
  readonly initial = input<CameraFormValue>(emptyCameraForm());
  protected readonly name = signal('');
  protected readonly url = signal('');
  protected readonly location = signal('Interior');
  protected readonly enabled = signal(true);
  protected readonly recordingEnabled = signal(true);
  protected readonly transport = signal('tcp');
  protected readonly username = signal('');
  protected readonly password = signal('');
  constructor() {
    effect(() => {
      const value = this.initial();
      this.name.set(value.name);
      this.url.set(value.url);
      this.location.set(value.location === 'Exterior' ? 'Exterior' : 'Interior');
      this.enabled.set(value.enabled);
      this.recordingEnabled.set(value.recordingEnabled);
      this.transport.set(value.transport === 'udp' ? 'udp' : 'tcp');
      this.username.set(value.username);
      this.password.set(value.password);
    });
  }

  clearCredentials(): void {
    this.username.set('');
    this.password.set('');
  }

  value(): CameraFormValue {
    return {
      name: this.name(),
      url: this.url(),
      location: this.location(),
      enabled: this.enabled(),
      recordingEnabled: this.recordingEnabled(),
      transport: this.transport() === 'udp' ? 'udp' : 'tcp',
      username: this.username(),
      password: this.password(),
    };
  }
}
