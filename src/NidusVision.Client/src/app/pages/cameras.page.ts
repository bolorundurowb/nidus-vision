import { Component, inject } from '@angular/core';
import { CameraStore } from '../camera.store';
import { StatusPill } from '../ui/status-pill';
import { AddCameraDialog } from '../add-camera.dialog';

@Component({
  selector: 'app-cameras-page',
  imports: [StatusPill],
  template: `
    <div class="page">
      <div class="toolbar">
        <div>
          <p class="eyebrow">Device setup</p>
          <h2 class="page-title">IP cameras</h2>
          <p class="muted">Manage streams, connectivity, and retention per device.</p>
        </div>
        <button type="button" class="btn" (click)="openAdd()">Add camera</button>
      </div>
      <div class="grid cams">
        @for (camera of store.cameras(); track camera.id) {
          <article class="card cam">
            <h3>{{ camera.name }}</h3>
            <p class="muted">{{ camera.location }} · RTSP</p>
            <app-status-pill [status]="camera.status" />
            <dl>
              <div><dt>Bitrate</dt><dd>{{ camera.bitrate }}</dd></div>
              <div><dt>Framerate</dt><dd>{{ camera.fps }} fps</dd></div>
              <div><dt>Retention</dt><dd>{{ camera.retention }}</dd></div>
            </dl>
            <button type="button" class="btn outline sm full">Configure</button>
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
                  <td class="mono">{{ c.mainRtspUrl }}</td>
                  <td>{{ c.resolution }}</td>
                  <td><app-status-pill [status]="c.status" /></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    </div>
  `,
  styles: `
    .toolbar { display: flex; justify-content: space-between; align-items: flex-end; gap: 1rem; flex-wrap: wrap; }
    .cams { grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr)); }
    .cam { padding: 1rem; display: flex; flex-direction: column; gap: 0.5rem; }
    h3 { margin: 0; font-weight: 500; }
    dl { margin: 0; border-top: 1px solid var(--border); padding-top: 0.75rem; font-size: 0.75rem; }
    dl div { display: flex; justify-content: space-between; margin-bottom: 0.35rem; }
    dt { color: var(--muted-foreground); }
    .diag { padding: 1rem; }
    .table-wrap { overflow-x: auto; }
    table { width: 100%; min-width: 640px; border-collapse: collapse; font-size: 0.875rem; text-align: left; }
    th { color: var(--muted-foreground); font-size: 0.75rem; font-weight: 500; padding: 0.75rem 0.5rem; }
    td { padding: 0.75rem 0.5rem; border-top: 1px solid var(--border); }
    .mono { font-family: ui-monospace, monospace; font-size: 0.75rem; color: var(--muted-foreground); }
  `,
})
export class CamerasPage {
  protected readonly store = inject(CameraStore);
  private readonly addDialog = inject(AddCameraDialog);

  protected openAdd(): void {
    this.addDialog.open.set(true);
  }
}
