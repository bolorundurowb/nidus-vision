import { Component, inject, signal } from '@angular/core';
import { CameraStore } from '../camera.store';
import { StatusPill } from '../ui/status-pill';
import { LiveTile } from '../ui/live-tile';

@Component({
  selector: 'app-monitor-page',
  imports: [LiveTile],
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
          <button type="button" class="btn outline sm">Refresh</button>
        </div>
      </div>
      <div class="feeds" [attr.data-grid]="grid()">
        @for (camera of store.cameras().slice(0, grid() * grid()); track camera.id) {
          <app-live-tile [camera]="camera" />
        }
      </div>
      <section class="card timeline">
        <div class="tl-head">
          <div>
            <h3>Recording timeline</h3>
            <p class="muted">Continuous vs human detection</p>
          </div>
          <div class="legend"><span><i class="swatch sky"></i>Continuous</span><span><i class="swatch orange"></i>Human detected</span></div>
        </div>
        @for (camera of store.cameras().slice(0, 3); track camera.id; let i = $index) {
          <div class="row">
            <span>{{ camera.name }}</span>
            <div class="track">
              <div class="cont"></div>
              <div class="hit" [style.left]="i === 0 ? '26%' : i === 1 ? '58%' : '12%'" [style.width]="i === 0 ? '9%' : i === 1 ? '13%' : '5%'"></div>
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
    h3 { margin: 0; font-size: 1rem; font-weight: 500; }
    .row { margin-top: 0.75rem; display: flex; align-items: center; gap: 0.75rem; }
    .row > span { width: 5rem; font-size: 0.75rem; color: var(--muted-foreground); overflow: hidden; text-overflow: ellipsis; }
    .track { position: relative; height: 1.75rem; flex: 1; border-radius: 0.35rem; overflow: hidden; background: var(--muted); }
    .cont { position: absolute; inset: 0; background: rgb(56 189 248 / 0.3); }
    .hit { position: absolute; top: 0.25rem; bottom: 0.25rem; background: var(--orange); border-radius: 0.25rem; }
    .swatch { display: inline-block; width: 1.25rem; height: 0.5rem; border-radius: 2px; margin-right: 0.35rem; }
    .swatch.sky { background: rgb(56 189 248 / 0.6); }
    .swatch.orange { background: var(--orange); }
    .legend { font-size: 11px; color: var(--muted-foreground); gap: 1rem; }
  `,
})
export class MonitorPage {
  protected readonly store = inject(CameraStore);
  protected readonly grid = signal(2);
}
