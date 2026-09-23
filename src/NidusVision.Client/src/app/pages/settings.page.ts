import { Component, signal } from '@angular/core';

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
              <input type="range" min="1" max="90" [value]="general()" (input)="general.set(+$any($event.target).value)">
            </label>
            <label>
              <span>Human detection events <strong>{{ detection() }} days</strong></span>
              <input type="range" min="7" max="180" [value]="detection()" (input)="detection.set(+$any($event.target).value)">
            </label>
          </div>
          <div class="card pad">
            <h3>System & hardware</h3>
            <p class="muted">Nidus Vision 0.8.2 · Self-hosted instance</p>
            <div class="stats">
              <div><p class="muted">CPU usage</p><strong>12%</strong><span class="ok">Healthy</span></div>
              <div><p class="muted">Memory</p><strong>2.4 / 8 GB</strong><span class="ok">Healthy</span></div>
              <div><p class="muted">Uptime</p><strong>45 days</strong><span class="ok">Since Aug 9</span></div>
            </div>
          </div>
        </section>
        <section class="card pad">
          <h3>Storage allocation</h3>
          <p class="muted">2.3 TB of 4 TB used</p>
          <div class="donut"><div><strong>57%</strong><span class="muted">used</span></div></div>
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
  protected readonly general = signal(30);
  protected readonly detection = signal(90);
}
