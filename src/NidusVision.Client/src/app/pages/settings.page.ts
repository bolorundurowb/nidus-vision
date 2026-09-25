import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { SettingsApi } from '../api/settings.api';
import { AppIcon } from '../ui/app-icon';

@Component({
  selector: 'app-settings-page',
  imports: [DatePipe, AppIcon],
  template: `
    <div class="page">
      <div>
        <p class="eyebrow">Admin controls</p>
        <h2 class="page-title">Settings & Storage</h2>
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
              <span>Recordings with detections <strong>{{ detection() }} days</strong></span>
              <input type="range" min="1" max="180" [value]="detection()" (input)="detection.set(+$any($event.target).value); persist()">
            </label>
            <label>
              <span>Maximum recording storage <strong>{{ storageLimitLabel() }}</strong></span>
              <input
                type="number"
                min="1"
                step="1"
                placeholder="No limit"
                [value]="storageLimitGb() ?? ''"
                (change)="setStorageLimit($any($event.target).value)"
              >
            </label>
            <p class="muted help">Leave blank for no storage cap. When the cap is reached, the oldest recordings are removed first.</p>
            <label>
              <span>Recordings folder</span>
              <input type="text" readonly [value]="recordingsDirectory()" aria-readonly="true">
            </label>
            <p class="muted help">Resolved from server configuration. Select the path to copy it.</p>
          </div>
          <div class="card pad">
            <div class="head">
              <div>
                <h3>System & hardware</h3>
                <p class="muted">Nidus Vision {{ version() }} · Self-hosted instance</p>
              </div>
              <app-icon name="activity" />
            </div>
            <div class="stats">
              <div><p class="muted">CPU usage</p><strong>{{ cpu() }}</strong><span class="ok">Healthy</span></div>
              <div><p class="muted">Memory</p><strong>{{ memory() }}</strong><span class="ok">Healthy</span></div>
              <div>
                <p class="muted">Uptime</p>
                <strong>{{ uptime() }}</strong>
                @if (startedAt(); as started) {
                  <span class="ok">Since {{ started | date:'MMM d' }}</span>
                } @else {
                  <span class="ok">Process</span>
                }
              </div>
            </div>
          </div>
        </section>
        <section class="card pad">
          <h3>Storage allocation</h3>
          <p class="muted">{{ usedLabel() }}</p>
          <div class="donut" [style.background]="donutGradient()">
            <div>
              @if (unlimited()) {
                <strong>Unlimited</strong><span class="muted">no storage cap</span>
              } @else {
                <strong>{{ usedPct() }}%</strong><span class="muted">of configured max</span>
              }
            </div>
          </div>
          <ul>
            <li><span>Recordings</span><strong>{{ generalVideo() }}</strong></li>
            <li><span>System database</span><strong>{{ systemDatabase() }}</strong></li>
            <li class="free"><span>{{ unlimited() ? 'Storage limit' : 'Remaining' }}</span><strong>{{ freeSpace() }}</strong></li>
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
    input[type=number], input[type=text] { width: 100%; box-sizing: border-box; border: 1px solid var(--border); border-radius: 0.4rem; padding: 0.55rem; background: var(--card); color: inherit; }
    input[type=text][readonly] { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 0.8rem; cursor: text; }
    .help { margin: 0.5rem 0 0; font-size: 0.75rem; }
    .head { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
    .head p { margin: 0.35rem 0 0; }
    .head app-icon { color: var(--emerald); }
    .stats { display: grid; grid-template-columns: repeat(3, 1fr); gap: 0.75rem; margin-top: 1.25rem; }
    .stats > div { border: 1px solid var(--border); border-radius: 0.5rem; padding: 0.75rem 0.9rem; }
    .stats p { margin: 0; }
    .stats strong { display: block; margin: 0.3rem 0 0.2rem; font-size: 1.25rem; font-weight: 600; letter-spacing: -0.01em; }
    .ok { display: block; color: #059669; font-size: 0.75rem; }
    .donut { width: 11rem; height: 11rem; margin: 1.25rem auto; border-radius: 999px; display: grid; place-items: center; }
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
  protected readonly storageLimitGb = signal<number | null>(null);
  protected readonly storageLimitLabel = signal('No limit');
  protected readonly recordingsDirectory = signal('Unavailable');
  private readonly inferenceEnabled = signal(true);
  private readonly sampleFps = signal(1);
  private readonly confidenceThreshold = signal(0.6);
  protected readonly version = signal('0.8.2');
  protected readonly usedLabel = signal('Storage metrics unavailable');
  protected readonly usedPct = signal(0);
  protected readonly unlimited = signal(true);
  protected readonly cpu = signal('—');
  protected readonly memory = signal('—');
  protected readonly uptime = signal('—');
  protected readonly startedAt = signal<Date | null>(null);
  protected readonly generalVideo = signal('—');
  protected readonly systemDatabase = signal('—');
  protected readonly freeSpace = signal('—');
  protected readonly donutGradient = signal('conic-gradient(var(--muted) 0 100%)');

  constructor() {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      const settings = await this.api.get();
      this.general.set(settings.generalRetentionDays);
      this.detection.set(settings.detectionRetentionDays);
      this.setStorageLimitValue(settings.maxStorageBytes);
      if (settings.recordingsDirectory) {
        this.recordingsDirectory.set(settings.recordingsDirectory);
      }
      this.inferenceEnabled.set(settings.inferenceEnabled);
      this.sampleFps.set(settings.sampleFps);
      this.confidenceThreshold.set(settings.confidenceThreshold);
      const metrics = await this.api.metrics();
      this.version.set(metrics.version);
      this.cpu.set(`${metrics.cpuPercent.toFixed(0)}%`);
      this.memory.set(`${this.gbValue(metrics.memoryUsedBytes)} / ${this.gbValue(metrics.memoryTotalBytes)} GB`);
      this.setUptime(metrics.uptime);
      const configuredMax = settings.maxStorageBytes;
      this.unlimited.set(configuredMax === null);
      if (metrics.storage.usedBytes >= 0) {
        const total = configuredMax ?? 0;
        this.usedPct.set(total > 0 ? Math.min(100, Math.round((metrics.storage.usedBytes / total) * 100)) : 0);
        this.usedLabel.set(configuredMax === null
          ? `${this.gb(metrics.storage.usedBytes)} used · unlimited`
          : `${this.gb(metrics.storage.usedBytes)} of ${this.gb(configuredMax)} configured`);
        if (configuredMax === null) {
          this.donutGradient.set('conic-gradient(var(--muted) 0 100%)');
        } else {
          const g = (metrics.storage.recordingBytes / total) * 100;
          const db = (metrics.storage.databaseBytes / total) * 100;
          const gEnd = Math.min(100, g);
          const dbEnd = Math.min(100, gEnd + db);
          this.donutGradient.set(
            `conic-gradient(#3b82f6 0 ${gEnd}%, #94a3b8 ${gEnd}% ${dbEnd}%, var(--muted) ${dbEnd}% 100%)`,
          );
        }
        this.generalVideo.set(this.gb(metrics.storage.recordingBytes));
        this.systemDatabase.set(this.gb(metrics.storage.databaseBytes));
        this.freeSpace.set(configuredMax === null ? 'Unlimited' : this.gb(Math.max(0, configuredMax - metrics.storage.usedBytes)));
      }
    } catch {
      /* mock */
    }
  }

  protected persist(): void {
    void this.api.save({
      generalRetentionDays: this.general(),
      detectionRetentionDays: this.detection(),
      maxStorageBytes: this.storageLimitGb() === null ? null : this.storageLimitGb()! * 1024 ** 3,
      inferenceEnabled: this.inferenceEnabled(),
      sampleFps: this.sampleFps(),
      confidenceThreshold: this.confidenceThreshold(),
    }).catch(() => undefined);
  }

  protected setStorageLimit(value: string): void {
    const gigabytes = value === '' ? null : Number(value);
    if (gigabytes !== null && (!Number.isFinite(gigabytes) || gigabytes < 1)) {
      return;
    }

    this.storageLimitGb.set(gigabytes);
    this.storageLimitLabel.set(gigabytes === null ? 'No limit' : `${gigabytes} GB`);
    this.persist();
  }

  private setStorageLimitValue(bytes: number | null): void {
    const gigabytes = bytes === null ? null : bytes / 1024 ** 3;
    this.storageLimitGb.set(gigabytes);
    this.storageLimitLabel.set(gigabytes === null ? 'No limit' : `${gigabytes} GB`);
  }

  private setUptime(value: string): void {
    const elapsedMs = SettingsPage.parseTimeSpan(value);
    if (elapsedMs === null) {
      this.uptime.set(value);
      this.startedAt.set(null);
      return;
    }

    const minutes = Math.floor(elapsedMs / 60_000);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);
    if (days >= 1) {
      this.uptime.set(SettingsPage.plural(days, 'day'));
    } else if (hours >= 1) {
      this.uptime.set(SettingsPage.plural(hours, 'hour'));
    } else {
      this.uptime.set(SettingsPage.plural(minutes, 'minute'));
    }

    this.startedAt.set(new Date(Date.now() - elapsedMs));
  }

  /** Parses the `[d.]hh:mm:ss[.fffffff]` form the server serialises TimeSpan with. */
  private static parseTimeSpan(value: string): number | null {
    const match = /^(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(value);
    if (!match) {
      return null;
    }

    const [, days, hours, minutes, seconds, fraction] = match;
    return (
      ((Number(days ?? 0) * 24 + Number(hours)) * 60 + Number(minutes)) * 60_000 +
      Number(seconds) * 1000 +
      Math.round(Number(`0.${fraction ?? 0}`) * 1000)
    );
  }

  private static plural(value: number, unit: string): string {
    return `${value} ${unit}${value === 1 ? '' : 's'}`;
  }

  private gbValue(bytes: number): string {
    return (bytes / 1024 ** 3).toFixed(1).replace(/\.0$/, '');
  }

  private gb(bytes: number): string {
    return `${(bytes / 1024 ** 3).toFixed(1)} GB`;
  }
}
