import { Component, inject, signal, viewChild } from '@angular/core';
import { CameraApi, ProbeResult, cameraWrite } from '../api/camera.api';
import { CameraForm, CameraFormValue } from '../camera-form';
import { CameraStore } from '../camera.store';
import { StatusPill } from '../ui/status-pill';
import { AddCameraDialog } from '../add-camera.dialog';
import { CameraItem, fpsLabel, statLabel } from '../models';
import { AppIcon } from '../ui/app-icon';

@Component({
  selector: 'app-cameras-page',
  imports: [StatusPill, AppIcon, CameraForm],
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
      @if (store.loaded() && store.cameras().length === 0) {
        <section class="card no-cameras">
          <p class="muted">{{ store.loadFailed() ? 'Could not load cameras from the server.' : 'No cameras yet. Use Add camera to connect an RTSP stream.' }}</p>
        </section>
      }
      <div class="grid cams">
        @for (camera of store.cameras(); track camera.id) {
          <article class="card cam">
            <div class="cam-top">
              <span class="cam-icon"><app-icon name="camera" /></span>
            </div>
            <h3>{{ camera.name }}</h3>
            <p class="muted">{{ camera.location }} · RTSP</p>
            <app-status-pill [status]="camera.status" [enabled]="camera.enabled" [recordingEnabled]="camera.recordingEnabled" />
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
        <div class="table-wrap">
          <table>
            <thead><tr><th>Camera</th><th>Stream URL</th><th>Resolution</th><th>Status</th></tr></thead>
            <tbody>
              @for (c of store.cameras(); track c.id) {
                <tr>
                  <td>{{ c.name }}</td>
                  <td class="url mono">{{ c.mainRtspUrl }}</td>
                  <td>{{ statLabel(c.resolution) }}</td>
                  <td><app-status-pill [status]="c.status" [enabled]="c.enabled" [recordingEnabled]="c.recordingEnabled" /></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
      @if (editing(); as cam) {
        <div class="modal-scrim" (click)="close()">
          <div class="modal card camera-dialog" (click)="$event.stopPropagation()">
            <div class="camera-dialog-close">
              <button type="button" class="icon-btn" (click)="close()" aria-label="Close dialog">
                <app-icon name="x" />
              </button>
            </div>
            <div class="camera-dialog-title">
              <h2>Configure {{ cam.name }}</h2>
              <p class="muted">Stream details, location, and credentials.</p>
            </div>
            @if (confirmingDelete()) {
              <div class="delete-confirm">
                <strong>Delete this camera?</strong>
                <p>This removes {{ cam.name }} and its stored footage. A file still held open by capture is removed once that process exits.</p>
                @if (probeResult(); as result) {
                  <p class="hint error">{{ result.message }}</p>
                }
                <div class="actions">
                  <button type="button" class="btn outline" (click)="confirmingDelete.set(false)">Cancel</button>
                  <button type="button" class="btn danger-fill" (click)="remove(cam)">Delete camera</button>
                </div>
              </div>
            } @else if (draft(); as initial) {
              <app-camera-form [initial]="initial">
                @if (probeResult(); as result) {
                  <p class="hint" [class.ok]="result.ok" [class.error]="!result.ok">{{ result.message }}</p>
                } @else {
                  <div class="cred-note">
                    <span>{{ removeCredentials() ? 'Stored username and password will be removed on save.' : (cam.hasCredentials ? 'Credentials are stored. Leave blank to keep them.' : 'This camera has no stored credentials yet.') }}</span>
                    @if (cam.hasCredentials && !removeCredentials()) {
                      <button type="button" class="text-link" (click)="clearStoredCredentials()">Remove</button>
                    }
                  </div>
                }
              </app-camera-form>
              <div class="camera-dialog-footer">
                <button type="button" class="trash-btn" (click)="confirmingDelete.set(true)" aria-label="Delete camera" title="Delete camera">
                  <app-icon name="trash-2" />
                </button>
                <div class="actions">
                  <button type="button" class="btn outline" [disabled]="probing()" (click)="testConnection(cam)">
                    <app-icon name="plug" />{{ probing() ? 'Testing…' : 'Test connection' }}
                  </button>
                  <button type="button" class="btn" (click)="save(cam)">
                    <app-icon name="save" />Save
                  </button>
                </div>
              </div>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .toolbar { display: flex; justify-content: space-between; align-items: flex-end; gap: 1rem; flex-wrap: wrap; }
    .cams { grid-template-columns: repeat(auto-fill, minmax(18rem, 1fr)); }
    .cam { padding: 1rem; display: flex; flex-direction: column; gap: 0.5rem; }
    .cam-top { display: flex; align-items: flex-start; justify-content: space-between; }
    .cam-icon { width: 2.25rem; height: 2.25rem; border-radius: 0.5rem; background: var(--muted); color: var(--foreground); display: grid; place-items: center; }
    .cam .icon-btn { margin: -0.25rem -0.35rem 0 0; color: var(--muted-foreground); }
    h3 { margin: 0; font-weight: 500; }
    dl { margin: 0; border-top: 1px solid var(--border); padding-top: 0.75rem; font-size: 0.75rem; }
    dl div { display: flex; justify-content: space-between; margin-bottom: 0.35rem; }
    dt { color: var(--muted-foreground); }
    .diag { padding: 1rem; }
    .no-cameras { padding: 1rem; }
    .no-cameras p { margin: 0; font-size: 0.875rem; }
    .table-wrap { overflow-x: auto; }
    table { width: 100%; min-width: 640px; border-collapse: collapse; font-size: 0.875rem; text-align: left; }
    th { color: var(--muted-foreground); font-size: 0.75rem; font-weight: 500; padding: 0.75rem 0.5rem; }
    td { padding: 0.75rem 0.5rem; border-top: 1px solid var(--border); }
    td.url { color: var(--muted-foreground); }
    .icon-btn { border: 0; background: transparent; color: inherit; padding: 0.25rem 0.4rem; border-radius: 0.35rem; display: inline-grid; place-items: center; }
  `,
})
export class CamerasPage {
  protected readonly store = inject(CameraStore);
  private readonly addDialog = inject(AddCameraDialog);
  private readonly api = inject(CameraApi);
  private readonly form = viewChild(CameraForm);
  protected readonly editing = signal<CameraItem | null>(null);
  protected readonly draft = signal<CameraFormValue | null>(null);
  protected readonly confirmingDelete = signal(false);
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
    this.probeResult.set(null);
    this.removeCredentials.set(false);
    this.confirmingDelete.set(false);
    this.draft.set(camera ? {
      name: camera.name,
      url: camera.mainRtspUrl,
      location: camera.location,
      enabled: camera.enabled,
      recordingEnabled: camera.recordingEnabled,
      transport: camera.transport,
      username: '',
      password: '',
    } : null);
    this.editing.set(camera ? { ...camera } : null);
  }

  protected clearStoredCredentials(): void {
    this.form()?.clearCredentials();
    this.removeCredentials.set(true);
  }

  protected close(): void {
    this.editing.set(null);
    this.draft.set(null);
    this.probeResult.set(null);
    this.confirmingDelete.set(false);
  }

  protected async testConnection(camera: CameraItem): Promise<void> {
    const form = this.form()?.value();
    if (!form) {
      return;
    }
    this.probing.set(true);
    this.probeResult.set(null);
    try {
      const result = await this.api.probe(this.writeBody(form));
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
    const form = this.form()?.value();
    if (!form) {
      return;
    }
    try {
      const saved = await this.api.update(camera.id, this.writeBody(form));
      this.store.cameras.update(list => list.map(c => (c.id === camera.id ? this.api.toItem(saved) : c)));
      this.close();
    } catch {
      this.probeResult.set({ ok: false, message: 'Could not save this camera. Check the server connection and try again.' });
    }
  }

  protected async remove(camera: CameraItem): Promise<void> {
    try {
      await this.api.delete(camera.id);
      this.store.cameras.update(list => list.filter(item => item.id !== camera.id));
      this.close();
    } catch {
      this.confirmingDelete.set(false);
      this.probeResult.set({ ok: false, message: 'Could not delete this camera.' });
    }
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

  private writeBody(form: CameraFormValue) {
    return cameraWrite({
      name: form.name,
      url: form.url,
      location: form.location,
      enabled: form.enabled,
      recordingEnabled: form.recordingEnabled,
      transport: form.transport,
      username: form.username,
      password: form.password,
      clearCredentials: this.removeCredentials(),
    });
  }
}
