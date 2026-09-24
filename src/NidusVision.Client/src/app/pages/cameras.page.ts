import { Component, inject, signal } from '@angular/core';
import { CameraApi, ProbeResult, cameraWrite } from '../api/camera.api';
import { CameraStore } from '../camera.store';
import { StatusPill } from '../ui/status-pill';
import { AddCameraDialog } from '../add-camera.dialog';
import { CameraItem, fpsLabel, statLabel } from '../models';
import { AppIcon } from '../ui/app-icon';

interface RoiPoint {
  x: number;
  y: number;
}

@Component({
  selector: 'app-cameras-page',
  imports: [StatusPill, AppIcon],
  template: `
    <div class="page">
      <div class="toolbar">
        <div>
          <p class="eyebrow">Device setup</p>
          <h2 class="page-title">IP cameras</h2>
          <p class="muted">Manage streams, connectivity, and retention per device.</p>
        </div>
        <button type="button" class="btn pill" (click)="openAdd()">
          <app-icon name="plus" />Add camera
        </button>
      </div>
      <div class="grid cams">
        @for (camera of store.cameras(); track camera.id) {
          <article class="card cam">
            <div class="cam-top">
              <span class="cam-icon"><app-icon name="camera" /></span>
              <button type="button" class="icon-btn" (click)="configure(camera.id)" aria-label="Configure {{ camera.name }}">
                <app-icon name="ellipsis-vertical" />
              </button>
            </div>
            <h3>{{ camera.name }}</h3>
            <p class="muted">{{ camera.location }} · RTSP</p>
            <app-status-pill [status]="camera.status" />
            <dl>
              <div><dt>Resolution</dt><dd>{{ statLabel(camera.resolution) }}</dd></div>
              <div><dt>Bitrate</dt><dd>{{ statLabel(camera.bitrate) }}</dd></div>
              <div><dt>Framerate</dt><dd>{{ fpsLabel(camera.fps) }}</dd></div>
              <div><dt>Retention</dt><dd>{{ statLabel(camera.retention) }}</dd></div>
            </dl>
            <button type="button" class="btn outline sm full" (click)="configure(camera.id)">
              <app-icon name="settings" />Configure
            </button>
          </article>
        }
      </div>
      <section class="card diag">
        <h3>Stream diagnostics</h3>
        <p class="muted">Last connectivity check: just now</p>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Camera</th><th>Stream URL</th><th>Resolution</th><th>Status</th></tr></thead>
            <tbody>
              @for (c of store.cameras(); track c.id) {
                <tr>
                  <td>{{ c.name }}</td>
                  <td class="url mono">{{ c.mainRtspUrl }}</td>
                  <td>{{ statLabel(c.resolution) }}</td>
                  <td><app-status-pill [status]="c.status" /></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
      @if (editing(); as cam) {
        <div class="modal-scrim" (click)="close()">
          <div class="modal card" (click)="$event.stopPropagation()">
            <div class="modal-head">
              <div>
                <h2>Configure {{ cam.name }}</h2>
                <p class="muted">Stream details, credentials, and detection area.</p>
              </div>
              <button type="button" class="icon-btn" (click)="close()" aria-label="Close dialog">
                <app-icon name="x" />
              </button>
            </div>
            <label>Name<input class="input" [value]="cam.name" (input)="cam.name = $any($event.target).value"></label>
            <label>RTSP URL<input class="input mono" [value]="cam.mainRtspUrl" (input)="cam.mainRtspUrl = $any($event.target).value"></label>
            <label>Location<input class="input" [value]="cam.location" (input)="cam.location = $any($event.target).value"></label>
            <div class="field-pair">
              <label>
                Username
                <input class="input" autocomplete="off" [value]="username()" (input)="username.set($any($event.target).value)">
              </label>
              <label>
                Password
                <input class="input" type="password" autocomplete="new-password" [value]="password()" (input)="password.set($any($event.target).value)">
              </label>
            </div>
            @if (cam.hasCredentials && !removeCredentials()) {
              <button type="button" class="btn outline sm" (click)="clearStoredCredentials()">
                Remove stored credentials
              </button>
            }
            @if (probeResult(); as result) {
              <p class="hint" [class.ok]="result.ok" [class.error]="!result.ok">{{ result.message }}</p>
            } @else if (removeCredentials()) {
              <p class="hint">Stored username and password will be removed on save.</p>
            } @else {
              <p class="hint">
                {{ cam.hasCredentials ? 'Credentials are stored. Leave blank to keep them.' : 'This camera has no stored credentials yet.' }}
              </p>
            }
            <div class="roi-block">
              <div class="roi-head">
                <span>Region of interest</span>
                <button type="button" class="btn outline sm" (click)="roiPoints.set([])">
                  <app-icon name="x" />Clear
                </button>
              </div>
              <p class="muted small">Click inside the frame to add polygon points.</p>
              <svg class="roi" viewBox="0 0 320 180" (click)="addPoint($event)">
                @if (roiPoints().length > 1) {
                  <polygon [attr.points]="polygon()" />
                }
                @for (point of roiPoints(); track $index) {
                  <circle [attr.cx]="point.x * 320" [attr.cy]="point.y * 180" r="4" />
                }
              </svg>
            </div>
            <div class="modal-actions">
              <button type="button" class="btn outline" (click)="close()">
                <app-icon name="x" />Cancel
              </button>
              <button type="button" class="btn outline" [disabled]="probing()" (click)="testConnection(cam)">
                <app-icon name="plug" />{{ probing() ? 'Testing…' : 'Test connection' }}
              </button>
              <button type="button" class="btn" (click)="save(cam)">
                <app-icon name="save" />Save
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .toolbar { display: flex; justify-content: space-between; align-items: flex-end; gap: 1rem; flex-wrap: wrap; }
    .cams { grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr)); }
    .cam { padding: 1rem; display: flex; flex-direction: column; gap: 0.5rem; }
    .cam-top { display: flex; align-items: flex-start; justify-content: space-between; }
    .cam-icon { width: 2.25rem; height: 2.25rem; border-radius: 0.5rem; background: var(--muted); color: var(--foreground); display: grid; place-items: center; }
    .cam .icon-btn { margin: -0.25rem -0.35rem 0 0; color: var(--muted-foreground); }
    h3 { margin: 0; font-weight: 500; }
    dl { margin: 0; border-top: 1px solid var(--border); padding-top: 0.75rem; font-size: 0.75rem; }
    dl div { display: flex; justify-content: space-between; margin-bottom: 0.35rem; }
    dt { color: var(--muted-foreground); }
    .diag { padding: 1rem; }
    .table-wrap { overflow-x: auto; }
    table { width: 100%; min-width: 640px; border-collapse: collapse; font-size: 0.875rem; text-align: left; }
    th { color: var(--muted-foreground); font-size: 0.75rem; font-weight: 500; padding: 0.75rem 0.5rem; }
    td { padding: 0.75rem 0.5rem; border-top: 1px solid var(--border); }
    td.url { color: var(--muted-foreground); }
    .small { font-size: 0.75rem; margin: 0; }
    .roi-block { display: flex; flex-direction: column; gap: 0.4rem; }
    .roi-head { display: flex; align-items: center; justify-content: space-between; font-size: 0.75rem; font-weight: 500; }
    .roi { width: 100%; aspect-ratio: 16 / 9; max-height: 11rem; background: #0f172a; border-radius: 0.5rem; cursor: crosshair; }
    .roi polygon { fill: rgb(249 115 22 / 0.25); stroke: var(--orange); stroke-width: 2; }
    .roi circle { fill: var(--orange); }
    .icon-btn { border: 0; background: transparent; color: inherit; padding: 0.25rem 0.4rem; border-radius: 0.35rem; display: inline-grid; place-items: center; }
  `,
})
export class CamerasPage {
  protected readonly store = inject(CameraStore);
  private readonly addDialog = inject(AddCameraDialog);
  private readonly api = inject(CameraApi);
  protected readonly editing = signal<CameraItem | null>(null);
  protected readonly roiPoints = signal<RoiPoint[]>([]);
  protected readonly username = signal('');
  protected readonly password = signal('');
  protected readonly probing = signal(false);
  protected readonly probeResult = signal<ProbeResult | null>(null);
  protected readonly removeCredentials = signal(false);
  protected readonly statLabel = statLabel;
  protected readonly fpsLabel = fpsLabel;

  protected openAdd(): void {
    this.addDialog.open.set(true);
  }

  protected configure(id: string): void {
    const camera = this.store.cameras().find(c => c.id === id);
    this.username.set('');
    this.password.set('');
    this.probeResult.set(null);
    this.removeCredentials.set(false);
    this.roiPoints.set(camera ? parseRoi(camera.roiJson) : []);
    this.editing.set(camera ? { ...camera } : null);
  }

  protected clearStoredCredentials(): void {
    this.username.set('');
    this.password.set('');
    this.removeCredentials.set(true);
  }

  protected close(): void {
    this.editing.set(null);
    this.probeResult.set(null);
  }

  protected polygon(): string {
    return this.roiPoints().map(p => `${p.x * 320},${p.y * 180}`).join(' ');
  }

  protected addPoint(event: MouseEvent): void {
    const rect = (event.currentTarget as SVGSVGElement).getBoundingClientRect();
    this.roiPoints.update(points => [
      ...points,
      { x: (event.clientX - rect.left) / rect.width, y: (event.clientY - rect.top) / rect.height },
    ]);
  }

  protected async testConnection(camera: CameraItem): Promise<void> {
    this.probing.set(true);
    this.probeResult.set(null);
    try {
      const result = await this.api.probe(this.writeBody(camera));
      this.probeResult.set(result);
      if (result.ok) {
        this.applyStreamInfo(camera.id, result);
      }
    } catch {
      this.probeResult.set({ ok: false, message: 'Could not reach the server to test this camera.' });
    } finally {
      this.probing.set(false);
    }
  }

  protected async save(camera: CameraItem): Promise<void> {
    const roiJson = this.roiPoints().length ? JSON.stringify(this.roiPoints()) : null;
    const updated: CameraItem = { ...camera, roiJson };
    try {
      const saved = await this.api.update(camera.id, this.writeBody(updated));
      this.store.cameras.update(list => list.map(c => (c.id === camera.id ? this.api.toItem(saved) : c)));
    } catch {
      this.store.cameras.update(list => list.map(c => (c.id === camera.id ? updated : c)));
    }
    this.close();
  }

  /** A probe reports the live stream details before ingest has had a chance to persist them. */
  private applyStreamInfo(id: string, result: ProbeResult): void {
    if (!result.resolution && !result.fps) {
      return;
    }
    this.store.cameras.update(list => list.map(c => (c.id === id
      ? { ...c, resolution: result.resolution ?? c.resolution, fps: result.fps ?? c.fps }
      : c)));
  }

  private writeBody(camera: CameraItem) {
    return cameraWrite({
      name: camera.name,
      url: camera.mainRtspUrl,
      location: camera.location,
      username: this.username(),
      password: this.password(),
      roiJson: camera.roiJson ?? null,
      clearCredentials: this.removeCredentials(),
    });
  }
}

function parseRoi(roiJson: string | null | undefined): RoiPoint[] {
  if (!roiJson) {
    return [];
  }
  try {
    const parsed = JSON.parse(roiJson) as RoiPoint[];
    return Array.isArray(parsed) ? parsed.filter(p => typeof p?.x === 'number' && typeof p?.y === 'number') : [];
  } catch {
    return [];
  }
}
