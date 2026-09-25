import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { EventApi, EventDto, RecordingDto } from '../api/event.api';
import { CameraStore } from '../camera.store';
import { AppIcon } from '../ui/app-icon';

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
        <label class="camera-filter">
          <select aria-label="Filter by type" [value]="kind()" (change)="setKind($any($event.target).value)">
            <option value="all">All types</option>
            <option value="event">Events</option>
            <option value="recording">Recordings</option>
          </select>
        </label>
        <div class="date-range" role="group" aria-label="Filter by date and time">
          <label>
            <span>From</span>
            <input
              type="datetime-local"
              aria-label="From date and time"
              [value]="fromDateTime()"
              [max]="toDateTime() || null"
              (change)="setFromDateTime($any($event.target).value)"
            >
          </label>
          <label>
            <span>To</span>
            <input
              type="datetime-local"
              aria-label="To date and time"
              [value]="toDateTime()"
              [min]="fromDateTime() || null"
              (change)="setToDateTime($any($event.target).value)"
            >
          </label>
        </div>
        <label class="camera-filter">
          <app-icon name="camera" />
          <select aria-label="Filter by camera" [value]="cameraId() ?? ''" (change)="setCamera($any($event.target).value)">
            <option value="">All cameras</option>
            @for (camera of store.cameras(); track camera.id) {
              <option [value]="camera.id">{{ camera.name }}</option>
            }
          </select>
        </label>
        @if (showRecordings()) {
          <label class="camera-filter">
            <select aria-label="Filter recordings by detections" [value]="detectionFilter()" (change)="setDetectionFilter($any($event.target).value)">
              <option value="all">All recordings</option>
              <option value="yes">With detections</option>
              <option value="no">Without detections</option>
            </select>
          </label>
        }
        @if (filtersActive()) {
          <button type="button" class="btn outline sm" (click)="clearFilters()">Clear filters</button>
        }
      </div>
      @if (dateRangeError()) {
        <p class="load-error" role="alert">{{ dateRangeError() }}</p>
      }
      @if (loading()) {
        <p class="muted status">Loading recordings and events…</p>
      }
      @if (error(); as message) {
        <p class="load-error" role="alert">{{ message }}</p>
      }
      @if (playbackError(); as message) {
        <p class="load-error" role="alert">{{ message }}</p>
      }
      @if (showEvents()) {
      @if (selected(); as event) {
        <section class="card player">
          <video controls [src]="'/api/events/' + event.id + '/clip.mp4'" (error)="playbackError.set('The event clip is unavailable.')"></video>
          <div class="player-bar">
            <strong>{{ event.cameraName }}</strong>
            <span>{{ event.startUtc | date:'medium' }}</span>
            <button type="button" class="btn outline sm" (click)="download(event)">
              <app-icon name="download" />Download
            </button>
            <button type="button" class="btn outline sm" (click)="remove(event)">
              <app-icon name="trash-2" />Delete
            </button>
          </div>
        </section>
      }
      <div class="grid events">
        @for (event of events(); track event.id) {
          <article class="card event" (click)="selectEvent(event)">
            <div class="thumb">
              @if (event.hasThumbnail) {
                <img [src]="'/api/events/' + event.id + '/thumbnail'" alt="">
              }
              <span>{{ duration(event.startUtc, event.endUtc) }}</span>
            </div>
            <div class="body">
              <div class="title-row">
                <div>
                  <h3>Person detected · {{ event.cameraName }}</h3>
                  <p class="muted">{{ when(event.startUtc) }}{{ quality(event.resolution) }}</p>
                </div>
              </div>
              <div class="foot">
                <span>Human detection</span>
                <button type="button" class="icon-btn" aria-label="Download event" (click)="download(event); $event.stopPropagation()">
                  <app-icon name="download" />
                </button>
              </div>
            </div>
          </article>
        }
      </div>
      @if (!loading() && events().length === 0) {
        <p class="muted status">No detection events found.</p>
      }
      @if (eventTotal() > 0) {
        <nav class="pagination" aria-label="Event pages">
          <span>{{ eventRange() }} of {{ eventTotal() }}</span>
          <div>
            <button type="button" class="btn outline sm" [disabled]="eventPage() === 1" (click)="setEventPage(eventPage() - 1)">Previous</button>
            <span>Page {{ eventPage() }} of {{ eventTotalPages() }}</span>
            <button type="button" class="btn outline sm" [disabled]="eventPage() >= eventTotalPages()" (click)="setEventPage(eventPage() + 1)">Next</button>
          </div>
        </nav>
      }

      }
      @if (showRecordings()) {
      <div class="section-title">
        <div>
          <p class="eyebrow">Continuous recording library</p>
          <h2 class="page-title">Recordings</h2>
        </div>
      </div>
      @if (selectedRecording(); as recording) {
        <section class="card player">
          @if (recording.available) {
            <video controls [src]="'/api/recordings/' + recording.id + '/video.mp4'" (error)="playbackError.set('The recording could not be played.')"></video>
          } @else {
            <p class="load-error">This recording is indexed, but its file is no longer available.</p>
          }
          <div class="player-bar">
            <strong>{{ recording.cameraName }}</strong>
            <span>{{ recording.startUtc | date:'medium' }}</span>
            @if (recording.available) {
              <button type="button" class="btn outline sm" (click)="downloadRecording(recording)">
                <app-icon name="download" />Download
              </button>
            }
          </div>
        </section>
      }
      <div class="grid events">
        @for (recording of recordings(); track recording.id) {
          <article class="card event" (click)="selectRecording(recording)">
            <div class="thumb">
              @if (recording.hasThumbnail && !thumbnailFailed().has(recording.id)) {
                <img [src]="'/api/recordings/' + recording.id + '/thumbnail'" alt="" (error)="markThumbnailFailed(recording.id)">
              }
              <span>{{ duration(recording.startUtc, recording.endUtc) }}</span>
              @if (recording.isActive) {
                <span class="live">Recording</span>
              }
            </div>
            <div class="body">
              <div class="title-row">
                <div>
                  <h3>{{ recording.hasHuman ? 'Human detected' : 'Continuous' }} · {{ recording.cameraName }}</h3>
                  <p class="muted">{{ when(recording.startUtc) }}{{ quality(recording.resolution) }}</p>
                </div>
                <span class="size">{{ recording.byteSize / 1048576 | number:'1.1-1' }} MB</span>
              </div>
              <div class="foot">
                <span>{{ recording.available ? (recording.isActive ? 'Recording now' : 'Continuous recording') : 'File missing' }}</span>
                @if (recording.available) {
                  <button
                    type="button"
                    class="icon-btn"
                    aria-label="Download recording"
                    (click)="downloadRecording(recording); $event.stopPropagation()"
                  >
                    <app-icon name="download" />
                  </button>
                }
              </div>
            </div>
          </article>
        }
      </div>
      @if (!loading() && recordings().length === 0) {
        <p class="muted status">No recordings found.</p>
      }
      @if (recordingTotal() > 0) {
        <nav class="pagination" aria-label="Recording pages">
          <span>{{ recordingRange() }} of {{ recordingTotal() }}</span>
          <div>
            <button type="button" class="btn outline sm" [disabled]="recordingPage() === 1" (click)="setRecordingPage(recordingPage() - 1)">Previous</button>
            <span>Page {{ recordingPage() }} of {{ recordingTotalPages() }}</span>
            <button type="button" class="btn outline sm" [disabled]="recordingPage() >= recordingTotalPages()" (click)="setRecordingPage(recordingPage() + 1)">Next</button>
          </div>
        </nav>
      }
      }
    </div>
  `,
  styles: `
    .filters { display: flex; flex-wrap: wrap; gap: 0.5rem; padding: 0.75rem; }
    .camera-filter { display: inline-flex; align-items: center; gap: 0.5rem; border: 1px solid var(--border); border-radius: 0.5rem; padding: 0.35rem 0.7rem; font-size: 0.75rem; background: var(--card); }
    .camera-filter select, .camera-filter input { border: 0; background: transparent; color: inherit; outline: 0; }
    .date-range { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .date-range label { display: inline-flex; align-items: center; gap: 0.45rem; border: 1px solid var(--border); border-radius: 0.5rem; padding: 0.35rem 0.7rem; background: var(--card); }
    .date-range span { color: var(--muted-foreground); font-size: 0.7rem; font-weight: 500; text-transform: uppercase; }
    .date-range input { border: 0; background: transparent; color: inherit; outline: 0; font: inherit; color-scheme: inherit; }
    .events { grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr)); }
    .thumb { aspect-ratio: 16/9; background: linear-gradient(#0f172a, #020617); position: relative; overflow: hidden; }
    .thumb img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .thumb span { position: absolute; left: 0.75rem; bottom: 0.75rem; background: rgb(0 0 0 / 0.5); color: #fff; font-size: 11px; padding: 0.2rem 0.4rem; border-radius: 0.25rem; }
    .thumb span.live { left: auto; right: 0.75rem; background: #dc2626; }
    .body { padding: 1rem; }
    h3 { margin: 0; font-size: 0.875rem; font-weight: 500; }
    .title-row { display: flex; justify-content: space-between; gap: 0.5rem; }
    .size { background: rgb(249 115 22 / 0.1); color: #ea580c; font-size: 10px; font-weight: 500; padding: 0.2rem 0.4rem; border-radius: 0.25rem; height: fit-content; white-space: nowrap; }
    .foot { display: flex; align-items: center; justify-content: space-between; border-top: 1px solid var(--border); margin-top: 0.75rem; padding-top: 0.5rem; font-size: 0.75rem; color: var(--muted-foreground); }
    .foot .icon-btn { display: inline-flex; border: 0; background: transparent; color: inherit; padding: 0.15rem; cursor: pointer; }
    .foot .icon-btn:hover { color: var(--foreground); }
    .player { padding: 1rem; }
    video { width: 100%; border-radius: 0.5rem; background: #000; }
    .player-bar { display: flex; gap: 0.75rem; align-items: center; margin-top: 0.75rem; }
    .event { cursor: pointer; }
    .section-title { margin-top: 1.5rem; }
    .status { padding: 0.75rem 0; }
    .load-error { color: #dc2626; padding: 0.75rem; margin: 0; }
    .pagination, .pagination div { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; }
    .pagination { font-size: 0.75rem; color: var(--muted-foreground); }
    @media (max-width: 40rem) { .pagination { align-items: flex-start; flex-direction: column; } }
  `,
  imports: [DatePipe, DecimalPipe, AppIcon],
})
export class EventsPage {
  private readonly api = inject(EventApi);
  protected readonly store = inject(CameraStore);
  private readonly pageSize = 12;
  protected readonly events = signal<EventDto[]>([]);
  protected readonly recordings = signal<RecordingDto[]>([]);
  protected readonly selected = signal<EventDto | null>(null);
  protected readonly selectedRecording = signal<RecordingDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly playbackError = signal<string | null>(null);
  protected readonly cameraId = signal<string | null>(null);
  protected readonly kind = signal<'all' | 'event' | 'recording'>('all');
  protected readonly fromDateTime = signal('');
  protected readonly toDateTime = signal('');
  protected readonly dateRangeError = signal<string | null>(null);
  protected readonly thumbnailFailed = signal<ReadonlySet<string>>(new Set());
  protected readonly eventPage = signal(1);
  protected readonly eventTotal = signal(0);
  protected readonly eventTotalPages = signal(1);
  protected readonly recordingPage = signal(1);
  protected readonly recordingTotal = signal(0);
  protected readonly recordingTotalPages = signal(1);
  protected readonly detectionFilter = signal<'all' | 'yes' | 'no'>('all');
  private loadSequence = 0;
  private pollHandle: ReturnType<typeof setInterval> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stopPolling());
    void this.load();
  }

  protected showEvents(): boolean {
    return this.kind() !== 'recording';
  }

  protected showRecordings(): boolean {
    return this.kind() !== 'event';
  }

  protected filtersActive(): boolean {
    return this.kind() !== 'all' || this.fromDateTime() !== '' || this.toDateTime() !== '' || this.cameraId() !== null || this.detectionFilter() !== 'all';
  }

  protected setKind(kind: string): void {
    this.kind.set(kind === 'event' || kind === 'recording' ? kind : 'all');
    this.resetPages();
    void this.load();
  }

  protected setFromDateTime(value: string): void {
    this.fromDateTime.set(value);
    this.applyDateRange();
  }

  protected setToDateTime(value: string): void {
    this.toDateTime.set(value);
    this.applyDateRange();
  }

  protected setCamera(cameraId: string): void {
    this.cameraId.set(cameraId || null);
    this.resetPages();
    void this.load();
  }

  protected setDetectionFilter(value: string): void {
    this.detectionFilter.set(value === 'yes' || value === 'no' ? value : 'all');
    this.resetPages();
    void this.load();
  }

  protected clearFilters(): void {
    this.kind.set('all');
    this.fromDateTime.set('');
    this.toDateTime.set('');
    this.dateRangeError.set(null);
    this.cameraId.set(null);
    this.detectionFilter.set('all');
    this.resetPages();
    void this.load();
  }

  protected setEventPage(page: number): void {
    this.eventPage.set(page);
    void this.load();
  }

  protected setRecordingPage(page: number): void {
    this.recordingPage.set(page);
    void this.load();
  }

  /** Segments that FFmpeg could not produce a poster frame for fall back to the empty tile. */
  protected markThumbnailFailed(recordingId: string): void {
    this.thumbnailFailed.update(failed => new Set(failed).add(recordingId));
  }

  protected duration(startUtc: string, endUtc: string): string {
    const seconds = Math.max(0, Math.round((new Date(endUtc).getTime() - new Date(startUtc).getTime()) / 1000));
    const pad = (value: number) => value.toString().padStart(2, '0');
    const hours = Math.floor(seconds / 3600);
    const body = `${pad(Math.floor((seconds % 3600) / 60))}:${pad(seconds % 60)}`;
    return hours > 0 ? `${hours}:${body}` : body;
  }

  protected when(startUtc: string): string {
    const at = new Date(startUtc);
    const time = at.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    if (at >= today) {
      return `Today at ${time}`;
    }
    if (at >= new Date(today.getTime() - 86_400_000)) {
      return `Yesterday at ${time}`;
    }
    return `${at.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} at ${time}`;
  }

  /** Cameras report "1920×1080"; the card shows the vertical resolution the way players do. */
  protected quality(resolution: string | null): string {
    const height = resolution?.split(/[×x]/)[1]?.trim();
    return height ? ` · ${height}p` : '';
  }

  protected eventRange(): string {
    return this.range(this.eventPage(), this.eventTotal());
  }

  protected recordingRange(): string {
    return this.range(this.recordingPage(), this.recordingTotal());
  }

  protected selectEvent(event: EventDto): void {
    this.playbackError.set(null);
    this.selected.set(event);
  }

  protected selectRecording(recording: RecordingDto): void {
    this.playbackError.set(null);
    this.selectedRecording.set(recording);
  }

  protected download(event: EventDto): void {
    window.open(`/api/events/${event.id}/clip.mp4`, '_blank');
  }

  protected downloadRecording(recording: RecordingDto): void {
    window.open(`/api/recordings/${recording.id}/video.mp4`, '_blank');
  }

  protected async remove(event: EventDto): Promise<void> {
    try {
      await this.api.delete(event.id);
    } catch {
      this.error.set('Could not delete the event. Check the server connection and try again.');
      return;
    }
    this.selected.set(null);
    if (this.events().length === 1 && this.eventPage() > 1) {
      this.eventPage.update(page => page - 1);
    }
    await this.load();
  }

  private async load(silent = false): Promise<void> {
    const sequence = ++this.loadSequence;
    if (!silent) {
      this.loading.set(true);
    }
    this.error.set(null);
    const range = this.dateRange();
    const detection = this.detectionFilter();
    const eventsPromise = this.showEvents()
      ? this.api.search({
          cameraId: this.cameraId(),
          page: this.eventPage(),
          pageSize: this.pageSize,
          ...range,
        })
      : Promise.resolve(null);
    const recordingsPromise = this.showRecordings()
      ? this.api.recordings({
          cameraId: this.cameraId(),
          page: this.recordingPage(),
          pageSize: this.pageSize,
          ...range,
          ...(detection === 'yes' ? { hasHuman: true } : detection === 'no' ? { hasHuman: false } : {}),
        })
      : Promise.resolve(null);
    const [events, recordings] = await Promise.allSettled([eventsPromise, recordingsPromise]);
    if (sequence !== this.loadSequence) return;

    if (events.status === 'fulfilled') {
      const page = events.value;
      this.events.set(page?.items ?? []);
      this.eventTotal.set(page?.totalCount ?? 0);
      this.eventTotalPages.set(page?.totalPages ?? 1);
    } else {
      this.events.set([]);
      this.eventTotal.set(0);
    }
    if (recordings.status === 'fulfilled') {
      const page = recordings.value;
      this.recordings.set(page?.items ?? []);
      this.recordingTotal.set(page?.totalCount ?? 0);
      this.recordingTotalPages.set(page?.totalPages ?? 1);
      const selected = this.selectedRecording();
      if (selected) {
        const updated = page?.items.find(item => item.id === selected.id);
        if (updated) {
          this.selectedRecording.set(updated);
        }
      }
    } else {
      this.recordings.set([]);
      this.recordingTotal.set(0);
    }

    const failures = [
      this.showEvents() && events.status === 'rejected' ? 'events' : null,
      this.showRecordings() && recordings.status === 'rejected' ? 'recordings' : null,
    ].filter(Boolean);
    if (failures.length > 0) {
      this.error.set(`Could not load ${failures.join(' and ')}. Check the server connection and sign in again.`);
    }
    if (!silent) {
      this.loading.set(false);
    }
    this.syncPolling();
  }

  private syncPolling(): void {
    const shouldPoll = this.showRecordings() && this.recordings().some(recording => recording.isActive);
    if (shouldPoll) {
      this.pollHandle ??= setInterval(() => void this.load(true), 10_000);
      return;
    }

    this.stopPolling();
  }

  private stopPolling(): void {
    if (this.pollHandle) {
      clearInterval(this.pollHandle);
      this.pollHandle = undefined;
    }
  }

  private dateRange(): { fromUtc?: string; toUtc?: string } {
    return {
      ...(this.fromDateTime() ? { fromUtc: new Date(this.fromDateTime()).toISOString() } : {}),
      ...(this.toDateTime() ? { toUtc: new Date(this.toDateTime()).toISOString() } : {}),
    };
  }

  private applyDateRange(): void {
    const from = this.fromDateTime();
    const to = this.toDateTime();
    if (from && to && new Date(from) >= new Date(to)) {
      this.dateRangeError.set('The “To” date and time must be later than “From”.');
      return;
    }

    this.dateRangeError.set(null);
    this.resetPages();
    void this.load();
  }

  private resetPages(): void {
    this.eventPage.set(1);
    this.recordingPage.set(1);
    this.selected.set(null);
    this.selectedRecording.set(null);
    this.playbackError.set(null);
  }

  private range(page: number, total: number): string {
    const start = (page - 1) * this.pageSize + 1;
    return `${start}–${Math.min(page * this.pageSize, total)}`;
  }
}
