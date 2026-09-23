import { Component } from '@angular/core';

@Component({
  selector: 'app-events-page',
  template: `
    <div class="page">
      <div>
        <p class="eyebrow">Event player & library</p>
        <h2 class="page-title">Events</h2>
        <p class="muted">Find and review recorded moments across your cameras.</p>
      </div>
      <div class="card filters">
        <input class="input" placeholder="Search events..." aria-label="Search events" (input)="query = $any($event.target).value">
        <button type="button" class="btn outline sm">Filters</button>
        <button type="button" class="btn outline sm">All cameras</button>
      </div>
      <div class="grid events">
        @for (event of filtered(); track $index) {
          <article class="card event">
            <div class="thumb"><span>00:{{ $index % 2 ? '42' : '18' }}</span></div>
            <div class="body">
              <div class="title-row">
                <div>
                  <h3>{{ event }}</h3>
                  <p class="muted">Today at 14:{{ 32 + $index * 3 }} · 1080p</p>
                </div>
                <span class="conf">{{ $index % 2 ? '87%' : '98%' }}</span>
              </div>
              <div class="foot"><span>Human detection</span><span>Download</span></div>
            </div>
          </article>
        }
      </div>
    </div>
  `,
  styles: `
    .filters { display: flex; flex-wrap: wrap; gap: 0.5rem; padding: 0.75rem; }
    .filters .input { flex: 1; min-width: 12rem; }
    .events { grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr)); }
    .thumb { aspect-ratio: 16/9; background: linear-gradient(#0f172a, #020617); position: relative; }
    .thumb span { position: absolute; left: 0.75rem; bottom: 0.75rem; background: rgb(0 0 0 / 0.5); color: #fff; font-size: 11px; padding: 0.2rem 0.4rem; border-radius: 0.25rem; }
    .body { padding: 1rem; }
    h3 { margin: 0; font-size: 0.875rem; font-weight: 500; }
    .title-row { display: flex; justify-content: space-between; gap: 0.5rem; }
    .conf { background: rgb(249 115 22 / 0.1); color: #ea580c; font-size: 10px; font-weight: 500; padding: 0.2rem 0.4rem; border-radius: 0.25rem; height: fit-content; }
    .foot { display: flex; justify-content: space-between; border-top: 1px solid var(--border); margin-top: 0.75rem; padding-top: 0.5rem; font-size: 0.75rem; color: var(--muted-foreground); }
  `,
})
export class EventsPage {
  protected query = '';
  private readonly events = [
    'Person detected · Front Yard',
    'Person detected · Driveway',
    'Vehicle detected · Front Yard',
    'Person detected · Garage',
    'Motion detected · Backyard',
    'Person detected · Driveway',
  ];

  protected filtered() {
    const q = this.query.trim().toLowerCase();
    return q ? this.events.filter(e => e.toLowerCase().includes(q)) : this.events;
  }
}
