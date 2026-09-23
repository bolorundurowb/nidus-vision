import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { EventApi, EventDto } from '../api/event.api';

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
        <input class="input" placeholder="Search events..." aria-label="Search events" (input)="onSearch($any($event.target).value)">
        <button type="button" class="btn outline sm">Filters</button>
        <button type="button" class="btn outline sm">All cameras</button>
      </div>
      @if (selected(); as event) {
        <section class="card player">
          <video controls [src]="'/api/events/' + event.id + '/clip.mp4'"></video>
          <div class="player-bar">
            <strong>{{ event.cameraName }}</strong>
            <span>{{ (event.confidence * 100) | number:'1.0-0' }}%</span>
            <button type="button" class="btn outline sm" (click)="download(event)">Download</button>
            <button type="button" class="btn outline sm" (click)="remove(event)">Delete</button>
          </div>
        </section>
      }
      <div class="grid events">
        @for (event of events(); track event.id) {
          <article class="card event" (click)="selected.set(event)">
            <div class="thumb"><span>{{ event.startUtc | date:'HH:mm:ss' }}</span></div>
            <div class="body">
              <div class="title-row">
                <div>
                  <h3>Person detected · {{ event.cameraName }}</h3>
                  <p class="muted">{{ event.startUtc | date:'short' }}</p>
                </div>
                <span class="conf">{{ (event.confidence * 100) | number:'1.0-0' }}%</span>
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
    .player { padding: 1rem; }
    video { width: 100%; border-radius: 0.5rem; background: #000; }
    .player-bar { display: flex; gap: 0.75rem; align-items: center; margin-top: 0.75rem; }
    .event { cursor: pointer; }
  `,
  imports: [DatePipe, DecimalPipe],
})
export class EventsPage {
  private readonly api = inject(EventApi);
  protected readonly events = signal<EventDto[]>([]);
  protected readonly selected = signal<EventDto | null>(null);

  constructor() {
    void this.load('');
  }

  protected onSearch(q: string): void {
    void this.load(q);
  }

  protected download(event: EventDto): void {
    window.open(`/api/events/${event.id}/clip.mp4`, '_blank');
  }

  protected async remove(event: EventDto): Promise<void> {
    await this.api.delete(event.id).catch(() => undefined);
    this.events.update(list => list.filter(e => e.id !== event.id));
    this.selected.set(null);
  }

  private async load(q: string): Promise<void> {
    try {
      this.events.set(await this.api.search(q));
    } catch {
      this.events.set([]);
    }
  }
}
