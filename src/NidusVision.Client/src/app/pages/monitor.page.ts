import { Component, inject, signal } from '@angular/core';
import { CameraApi, TimelineRowDto } from '../api/camera.api';
import { CameraStore } from '../camera.store';
import { LiveTile } from '../ui/live-tile';
import { AppIcon, AppIconName } from '../ui/app-icon';

interface TimelineBar {
  left: number;
  width: number;
}

interface TimelineView {
  cameraId: string;
  cameraName: string;
  continuous: TimelineBar[];
  hits: TimelineBar[];
}

@Component({
  selector: 'app-monitor-page',
  imports: [LiveTile, AppIcon],
  template: `
    <div class="page">
      <div class="toolbar">
        <div>
          <p class="eyebrow">Monitor center</p>
          <h2 class="page-title">Live view</h2>
          <p class="muted">A real-time overview of your property.</p>
        </div>
        <div class="actions">
          <div class="seg">
            @for (n of [1,2,3]; track n) {
              <button type="button" [class.on]="grid() === n" (click)="grid.set(n)">{{ n }}×{{ n }}</button>
            }
          </div>
          <button
            type="button"
            class="btn outline sm refresh"
            [class.ok]="refreshResult() === 'ok'"
            [class.error]="refreshResult() === 'error'"
            [disabled]="refreshing()"
            (click)="refresh()"
          >
            <app-icon [name]="refreshIcon()" [class.spin]="refreshing()" />
            <span role="status" aria-live="polite">{{ refreshLabel() }}</span>
          </button>
        </div>
      </div>
      @if (store.loaded() && store.cameras().length === 0) {
        <section class="card no-cameras">
          <p class="muted">{{ store.loadFailed() ? 'Could not load cameras from the server.' : 'No cameras yet. Add one from IP Cameras to start recording.' }}</p>
        </section>
      } @else {
        <div class="feeds" [attr.data-grid]="grid()">
          @for (camera of store.cameras().slice(0, grid() * grid()); track camera.id) {
            <app-live-tile [camera]="camera" />
          }
        </div>
      }
      <section class="card timeline">
        <div class="tl-head">
          <div>
            <h3>Recording timeline</h3>
            <p class="muted">Continuous vs human detection</p>
          </div>
          <div class="legend"><span><i class="swatch sky"></i>Continuous</span><span><i class="swatch orange"></i>Human detected</span></div>
        </div>
        @if (timeline().length === 0) {
          <p class="muted empty">No recordings in the last 24 hours.</p>
        }
        @for (row of timeline(); track row.cameraId) {
          <div class="row">
            <span>{{ row.cameraName }}</span>
            <div class="track">
              @for (bar of row.continuous; track $index) {
                <div class="cont" [style.left.%]="bar.left" [style.width.%]="bar.width"></div>
              }
              @for (bar of row.hits; track $index) {
                <div class="hit" [style.left.%]="bar.left" [style.width.%]="bar.width"></div>
              }
            </div>
          </div>
        }
      </section>
    </div>
  `,
  styles: `
    .toolbar, .actions, .tl-head, .legend, .row, .top, .bot { display: flex; align-items: flex-end; justify-content: space-between; gap: 1rem; flex-wrap: wrap; }
    .seg { display: flex; border: 1px solid var(--border); border-radius: 0.5rem; padding: 0.2rem; background: var(--card); }
    .seg button { border: 0; background: transparent; color: var(--muted-foreground); padding: 0.35rem 0.7rem; border-radius: 0.35rem; font-size: 0.75rem; }
    .seg button.on { background: var(--primary); color: var(--primary-foreground); }
    /* Fixed width so the toolbar never reflows as the label changes between states. */
    .btn.refresh { min-width: 8.5rem; }
    .btn.refresh.ok { color: #059669; border-color: rgb(16 185 129 / 0.4); }
    .btn.refresh.error { color: #b91c1c; border-color: rgb(239 68 68 / 0.4); }
    app-icon.spin { animation: spin 0.9s linear infinite; }
    @keyframes spin { to { transform: rotate(360deg); } }
    .feeds { display: grid; gap: 1rem; }
    .feeds[data-grid='1'] { grid-template-columns: 1fr; }
    .feeds[data-grid='2'] { grid-template-columns: repeat(2, 1fr); }
    .feeds[data-grid='3'] { grid-template-columns: repeat(3, 1fr); }
    @media (max-width: 900px) { .feeds { grid-template-columns: 1fr !important; } }
    .feed { overflow: hidden; border-radius: 0.75rem; border: 1px solid var(--border); background: #020617; }
    .video { position: relative; aspect-ratio: 16/9; background: linear-gradient(180deg, #1e293b, #020617); }
    .feed.off { filter: grayscale(1); opacity: 0.5; }
    .top, .bot { position: absolute; left: 0.75rem; right: 0.75rem; color: #fff; font-size: 11px; }
    .top { top: 0.75rem; }
    .bot { bottom: 0.75rem; }
    .chip { background: rgb(0 0 0 / 0.5); padding: 0.2rem 0.5rem; border-radius: 0.35rem; }
    .timeline { padding: 1.25rem; }
    .no-cameras { padding: 1.25rem; }
    .no-cameras p { margin: 0; font-size: 0.875rem; }
    h3 { margin: 0; font-size: 1rem; font-weight: 500; }
    .row { margin-top: 0.75rem; display: flex; align-items: center; gap: 0.75rem; }
    .row > span { width: 5rem; font-size: 0.75rem; color: var(--muted-foreground); overflow: hidden; text-overflow: ellipsis; }
    .track { position: relative; height: 1.75rem; flex: 1; border-radius: 0.35rem; overflow: hidden; background: var(--muted); }
    .cont { position: absolute; top: 0; bottom: 0; background: rgb(56 189 248 / 0.3); }
    .empty { margin: 0.75rem 0 0; font-size: 0.75rem; }
    .hit { position: absolute; top: 0.25rem; bottom: 0.25rem; background: var(--orange); border-radius: 0.25rem; }
    .swatch { display: inline-block; width: 1.25rem; height: 0.5rem; border-radius: 2px; margin-right: 0.35rem; }
    .swatch.sky { background: rgb(56 189 248 / 0.6); }
    .swatch.orange { background: var(--orange); }
    .legend { font-size: 11px; color: var(--muted-foreground); gap: 1rem; }
  `,
})
export class MonitorPage {
  protected readonly store = inject(CameraStore);
  private readonly cameras = inject(CameraApi);
  protected readonly grid = signal(2);
  protected readonly refreshing = signal(false);
  protected readonly refreshResult = signal<'ok' | 'error' | null>(null);
  protected readonly timeline = signal<TimelineView[]>([]);
  private resultTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    void this.loadTimeline();
  }

  protected refreshLabel(): string {
    if (this.refreshing()) {
      return 'Refreshing…';
    }
    switch (this.refreshResult()) {
      case 'ok':
        return 'Feeds updated';
      case 'error':
        return 'Refresh failed';
      default:
        return 'Refresh';
    }
  }

  protected refreshIcon(): AppIconName {
    if (this.refreshing() || !this.refreshResult()) {
      return 'refresh-cw';
    }
    return this.refreshResult() === 'ok' ? 'check' : 'triangle-alert';
  }

  protected async refresh(): Promise<void> {
    if (this.refreshing()) {
      return;
    }
    clearTimeout(this.resultTimer);
    this.refreshResult.set(null);
    this.refreshing.set(true);
    try {
      const ok = await this.store.refresh();
      await this.loadTimeline();
      this.refreshResult.set(ok ? 'ok' : 'error');
    } finally {
      this.refreshing.set(false);
      this.resultTimer = setTimeout(() => this.refreshResult.set(null), 3000);
    }
  }

  private async loadTimeline(): Promise<void> {
    const to = new Date();
    const from = new Date(to.getTime() - 24 * 60 * 60 * 1000);
    try {
      const rows = await this.cameras.timeline(from.toISOString(), to.toISOString());
      this.timeline.set(rows.map(row => this.toView(row, from, to)));
    } catch {
      this.timeline.set([]);
    }
  }

  private toView(row: TimelineRowDto, from: Date, to: Date): TimelineView {
    const windowMs = to.getTime() - from.getTime();
    const bar = (start: string, end: string): TimelineBar => {
      const left = this.toPercent(new Date(start), from, windowMs);
      const right = this.toPercent(new Date(end), from, windowMs);
      return { left, width: Math.max(0.4, right - left) };
    };
    return {
      cameraId: row.cameraId,
      cameraName: row.cameraName,
      continuous: row.intervals.filter(i => !i.human).map(i => bar(i.start, i.end)),
      hits: row.intervals.filter(i => i.human).map(i => bar(i.start, i.end)),
    };
  }

  private toPercent(value: Date, windowStart: Date, windowMs: number): number {
    if (windowMs <= 0) {
      return 0;
    }
    return Math.min(100, Math.max(0, ((value.getTime() - windowStart.getTime()) / windowMs) * 100));
  }
}
