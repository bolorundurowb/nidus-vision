import { Component, inject, signal } from '@angular/core';
import { SettingsApi } from '../api/settings.api';

@Component({
  selector: 'app-settings-page',
  template: `
    <div class="page">
      <div>
        <p class="eyebrow">Admin controls</p>
        <h2 class="page-title">Settings & storage</h2>
        <p class="muted">Keep Nidus Vision predictable, lean, and easy to manage.</p>
      </div>
      <div class="layout">
        <section class="stack">
          <div class="card pad">
            <h3>Retention policy</h3>
            <p class="muted">Automatically remove oldest recordings at the limit.</p>
            <label>
              <span>General video retention <strong>{{ general() }} days</strong></span>
              <input type="range" min="1" max="90" [value]="general()" (input)="general.set(+$any($event.target).value); persist()">
            </label>
            <label>
              <span>Human detection events <strong>{{ detection() }} days</strong></span>
              <input type="range" min="7" max="180" [value]="detection()" (input)="detection.set(+$any($event.target).value); persist()">
            </label>
          </div>
          <div class="card pad">
            <h3>System & hardware</h3>
            <p class="muted">Nidus Vision {{ version() }} · Self-hosted instance</p>
            <div class="stats">
              <div><p class="muted">CPU usage</p><strong>12%</strong><span class="ok">Healthy</span></div>
              <div><p class="muted">Memory</p><strong>2.4 / 8 GB</strong><span class="ok">Healthy</span></div>
              <div><p class="muted">Uptime</p><strong>45 days</strong><span class="ok">Since Aug 9</span></div>
            </div>
          </div>
        </section>
        <section class="card pad">
          <h3>Storage allocation</h3>
          <p class="muted">{{ usedLabel() }}</p>
          <div class="donut"><div><strong>{{ usedPct() }}%</strong><span class="muted">used</span></div></div>
          <ul>
            <li><span>General video</span><strong>1.24 TB</strong></li>
            <li><span>Human events</span><strong>414 GB</strong></li>
            <li><span>System database</span><strong>96 GB</strong></li>
            <li class="free"><span>Free space</span><strong>1.7 TB</strong></li>
          </ul>
        </section>
      </div>
    </div>
  `,
  styles: `
    .layout { display: grid; gap: 1.25rem; grid-template-columns: 1.3fr 0.7fr; }
    @media (max-width: 960px) { .layout { grid-template-columns: 1fr; } }
    .stack { display: flex; flex-direction: column; gap: 1.25rem; }
    .pad { padding: 1.25rem; }
    h3 { margin: 0; font-weight: 500; }
    label { display: block; margin-top: 1.25rem; font-size: 0.875rem; }
    label span { display: flex; justify-content: space-between; margin-bottom: 0.4rem; }
    input[type=range] { width: 100%; accent-color: var(--primary); }
    .stats { display: grid; grid-template-columns: repeat(3, 1fr); gap: 0.75rem; margin-top: 1rem; }
    .stats div { border: 1px solid var(--border); border-radius: 0.5rem; padding: 0.75rem; }
    .ok { color: #059669; font-size: 11px; }
    .donut { width: 11rem; height: 11rem; margin: 1.25rem auto; border-radius: 999px; background: conic-gradient(#3b82f6 0 54%, #f97316 54% 72%, #94a3b8 72% 77%, var(--muted) 77% 100%); display: grid; place-items: center; }
    .donut > div { width: 8rem; height: 8rem; border-radius: 999px; background: var(--card); display: flex; flex-direction: column; align-items: center; justify-content: center; }
    ul { list-style: none; padding: 0; margin: 0; font-size: 0.75rem; }
    li { display: flex; justify-content: space-between; margin: 0.5rem 0; }
    .free { border-top: 1px solid var(--border); padding-top: 0.75rem; color: var(--muted-foreground); }
  `,
})
export class SettingsPage {
  private readonly api = inject(SettingsApi);
  protected readonly general = signal(30);
  protected readonly detection = signal(90);
  protected readonly version = signal('0.8.2');
  protected readonly usedLabel = signal('2.3 TB of 4 TB used');
  protected readonly usedPct = signal(57);

  constructor() {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      const settings = await this.api.get();
      this.general.set(settings.generalRetentionDays);
      this.detection.set(settings.detectionRetentionDays);
      const metrics = await this.api.metrics();
      this.version.set(metrics.version);
      if (metrics.storage.totalBytes > 0) {
        this.usedPct.set(Math.round((metrics.storage.usedBytes / metrics.storage.totalBytes) * 100));
        this.usedLabel.set(`${this.gb(metrics.storage.usedBytes)} of ${this.gb(metrics.storage.totalBytes)} used`);
      }
    } catch {
      /* mock */
    }
  }

  protected persist(): void {
    void this.api.save({
      generalRetentionDays: this.general(),
      detectionRetentionDays: this.detection(),
      maxStorageBytes: null,
      inferenceEnabled: true,
      sampleFps: 1,
      confidenceThreshold: 0.6,
    }).catch(() => undefined);
  }

  private gb(bytes: number): string {
    return `${(bytes / 1024 ** 3).toFixed(1)} GB`;
  }
}
