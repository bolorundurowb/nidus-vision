import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { SettingsApi, SettingsDto } from '../api/settings.api';
import { AppIcon } from '../ui/app-icon';

type SaveState = 'idle' | 'saving' | 'saved' | 'error';

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
            <div class="head">
              <h3>Retention policy</h3>
              @switch (saveState()) {
                @case ('saving') { <span class="save-state muted" role="status">Saving…</span> }
                @case ('saved') { <span class="save-state ok" role="status">Saved</span> }
                @case ('error') { <span class="save-state error" role="alert">Not saved</span> }
              }
            </div>
            <p class="muted">When the storage cap is reached, recordings without detections go first, then older footage.</p>
            @if (loadError(); as error) {
              <div class="banner error" role="alert">
                <span>{{ error }} The controls are locked so the current server values are not overwritten.</span>
                <button type="button" class="btn outline sm" (click)="retryLoad()">Retry</button>
              </div>
            }
            @if (saveError(); as error) {
              <div class="banner error" role="alert">
                <span>{{ error }}</span>
                <button type="button" class="btn outline sm" (click)="retrySave()">Retry</button>
              </div>
            }
            <label>
              <span>General video retention <strong>{{ general() }} days</strong></span>
              <input
                type="range"
                min="1"
                [max]="generalMax()"
                [value]="general()"
                [disabled]="!loaded()"
                (input)="general.set(+$any($event.target).value)"
                (change)="queueSave()"
              >
            </label>
            <label>
              <span>Recordings with detections <strong>{{ detection() }} days</strong></span>
              <input
                type="range"
                min="1"
                [max]="detectionMax()"
                [value]="detection()"
                [disabled]="!loaded()"
                (input)="detection.set(+$any($event.target).value)"
                (change)="queueSave()"
              >
            </label>
            <label>
              <span>Maximum recording storage <strong>{{ storageLimitLabel() }}</strong></span>
              <input
                type="number"
                min="1"
                step="1"
                placeholder="No limit"
                [value]="storageLimitGb() ?? ''"
                [disabled]="!loaded()"
                [attr.aria-invalid]="storageLimitError() ? true : null"
                (change)="setStorageLimit($any($event.target))"
              >
            </label>
            @if (storageLimitError(); as error) {
              <p class="help error" role="alert">{{ error }}</p>
            }
            <p class="muted help">Leave blank for no storage cap. When the cap is reached, recordings without detections are removed first, then the oldest remaining footage.</p>
            <label>
              <span>Segment duration <strong>{{ segmentDurationMinutes() }} min</strong></span>
              <input
                type="number"
                min="5"
                max="30"
                step="5"
                [value]="segmentDurationMinutes()"
                [disabled]="!loaded()"
                [attr.aria-invalid]="segmentDurationError() ? true : null"
                (change)="setSegmentDuration($any($event.target))"
              >
            </label>
            @if (segmentDurationError(); as error) {
              <p class="help error" role="alert">{{ error }}</p>
            }
            <p class="muted help">Length of each recording segment. Shorter segments finalize faster and lose less on a crash, but create more files.</p>
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
                <p class="muted licence">
                  <a href="https://github.com/bolorundurowb/nidus-vision" target="_blank" rel="noopener">Source code</a> (MIT).
                  Person detection uses the Ultralytics YOLOv8n model, licensed under
                  <a href="https://github.com/bolorundurowb/nidus-vision/blob/main/THIRD-PARTY-NOTICES.md" target="_blank" rel="noopener">AGPL-3.0</a>.
                </p>
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
    .licence { font-size: 0.75rem; }
    .save-state { font-size: 0.75rem; white-space: nowrap; }
    .error { color: var(--red); }
    .banner { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; margin-top: 1rem; padding: 0.6rem 0.75rem; border: 1px solid currentColor; border-radius: 0.5rem; font-size: 0.8rem; }
    .banner span { color: var(--foreground); }
    input:disabled { opacity: 0.55; cursor: not-allowed; }
    .licence a { color: inherit; text-decoration: underline; }
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
  private static readonly SaveDelayMs = 400;

  private readonly api = inject(SettingsApi);
  protected readonly general = signal(30);
  protected readonly detection = signal(90);
  protected readonly generalMax = computed(() => Math.max(90, this.general()));
  protected readonly detectionMax = computed(() => Math.max(180, this.detection()));
  protected readonly storageLimitGb = signal<number | null>(null);
  protected readonly storageLimitLabel = signal('No limit');
  protected readonly storageLimitError = signal<string | null>(null);
  protected readonly segmentDurationMinutes = signal(15);
  protected readonly segmentDurationError = signal<string | null>(null);
  protected readonly recordingsDirectory = signal('Unavailable');
  /** False until the server's values are loaded, so a failed load can't overwrite them with defaults. */
  protected readonly loaded = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saveState = signal<SaveState>('idle');
  protected readonly saveError = signal<string | null>(null);
  private readonly inferenceEnabled = signal(true);
  private readonly sampleFps = signal(1);
  private readonly confidenceThreshold = signal(0.6);
  protected readonly version = signal('1.0.0');
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

  private saveTimer: ReturnType<typeof setTimeout> | null = null;
  private saving = false;
  private saveAgain = false;
  private savedTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      if (this.saveTimer !== null) {
        clearTimeout(this.saveTimer);
        // Leaving the page must not drop a change the user already made.
        void this.flush();
      }

      if (this.savedTimer !== null) {
        clearTimeout(this.savedTimer);
      }
    });
    void this.load();
  }

  protected retryLoad(): void {
    void this.load();
  }

  protected retrySave(): void {
    this.queueSave(0);
  }

  private async load(): Promise<void> {
    this.loadError.set(null);
    try {
      const settings = await this.api.get();
      this.general.set(settings.generalRetentionDays);
      this.detection.set(settings.detectionRetentionDays);
      this.setStorageLimitValue(settings.maxStorageBytes);
      this.segmentDurationMinutes.set(settings.segmentDurationSeconds / 60);
      if (settings.recordingsDirectory) {
        this.recordingsDirectory.set(settings.recordingsDirectory);
      }
      this.inferenceEnabled.set(settings.inferenceEnabled);
      this.sampleFps.set(settings.sampleFps);
      this.confidenceThreshold.set(settings.confidenceThreshold);
      this.loaded.set(true);
    } catch (error) {
      this.loaded.set(false);
      this.loadError.set(`Could not load the current settings: ${SettingsPage.describe(error)}.`);
      return;
    }

    // Metrics are informational. A failure here must not lock the retention controls.
    try {
      await this.loadMetrics();
    } catch {
      this.usedLabel.set('Storage metrics unavailable');
    }
  }

  private async loadMetrics(): Promise<void> {
    const metrics = await this.api.metrics();
    this.version.set(metrics.version);
    this.cpu.set(`${metrics.cpuPercent.toFixed(0)}%`);
    this.memory.set(`${this.gbValue(metrics.memoryUsedBytes)} / ${this.gbValue(metrics.memoryTotalBytes)} GB`);
    this.setUptime(metrics.uptime);
    const configuredMax = this.storageLimitGb() === null ? null : this.storageLimitGb()! * 1024 ** 3;
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
  }

  /**
   * Debounces and serializes saves. Sliders only save on `change` (release or keyboard commit),
   * at most one PUT is in flight, and a change made during a save triggers one more save with
   * the latest values. Out-of-order responses can't leave an older value on the server.
   */
  protected queueSave(delayMs = SettingsPage.SaveDelayMs): void {
    if (!this.loaded()) {
      return;
    }

    if (this.saveTimer !== null) {
      clearTimeout(this.saveTimer);
    }

    this.saveTimer = setTimeout(() => {
      this.saveTimer = null;
      void this.flush();
    }, delayMs);
  }

  private async flush(): Promise<void> {
    if (this.saving) {
      this.saveAgain = true;
      return;
    }

    this.saving = true;
    this.saveState.set('saving');
    this.saveError.set(null);
    try {
      do {
        this.saveAgain = false;
        await this.api.save(this.snapshot());
      } while (this.saveAgain);
      this.saveState.set('saved');
      this.clearSavedLater();
    } catch (error) {
      this.saveState.set('error');
      this.saveError.set(`Could not save the retention settings: ${SettingsPage.describe(error)}.`);
    } finally {
      this.saving = false;
    }
  }

  private snapshot(): SettingsDto {
    const limit = this.storageLimitGb();
    return {
      generalRetentionDays: this.general(),
      detectionRetentionDays: this.detection(),
      maxStorageBytes: limit === null ? null : Math.round(limit * 1024 ** 3),
      inferenceEnabled: this.inferenceEnabled(),
      sampleFps: this.sampleFps(),
      confidenceThreshold: this.confidenceThreshold(),
      segmentDurationSeconds: this.segmentDurationMinutes() * 60,
    };
  }

  private clearSavedLater(): void {
    if (this.savedTimer !== null) {
      clearTimeout(this.savedTimer);
    }

    this.savedTimer = setTimeout(() => {
      this.savedTimer = null;
      if (this.saveState() === 'saved') {
        this.saveState.set('idle');
      }
    }, 2000);
  }

  protected setStorageLimit(input: HTMLInputElement): void {
    const value = input.value.trim();
    const gigabytes = value === '' ? null : Number(value);
    if (gigabytes !== null && (!Number.isFinite(gigabytes) || gigabytes < 1)) {
      this.storageLimitError.set('Enter at least 1 GB, or leave the field blank for no cap.');
      return;
    }

    this.storageLimitError.set(null);
    this.storageLimitGb.set(gigabytes);
    this.storageLimitLabel.set(gigabytes === null ? 'No limit' : `${gigabytes} GB`);
    this.queueSave(0);
  }

  protected setSegmentDuration(input: HTMLInputElement): void {
    const value = input.value.trim();
    const minutes = value === '' ? null : Number(value);
    if (minutes !== null && (!Number.isFinite(minutes) || minutes < 5 || minutes > 30)) {
      this.segmentDurationError.set('Enter a duration between 5 and 30 minutes.');
      return;
    }

    this.segmentDurationError.set(null);
    this.segmentDurationMinutes.set(minutes ?? 15);
    this.queueSave(0);
  }

  private static describe(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const message = (error.error as { message?: unknown } | null)?.message;
      if (typeof message === 'string' && message.length > 0) {
        return message;
      }

      return error.status === 0 ? 'the server could not be reached' : `the server returned ${error.status}`;
    }

    return 'unexpected error';
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
