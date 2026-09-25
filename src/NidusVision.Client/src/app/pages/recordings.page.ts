import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, ElementRef, inject, signal, viewChild } from '@angular/core';
import { RecordingApi, RecordingDto } from '../api/recording.api';
import { CameraStore } from '../camera.store';
import { AppIcon } from '../ui/app-icon';
import { downloadVideoFrame } from '../video-frame';

@Component({
  selector: 'app-recordings-page',
  imports: [DatePipe, DecimalPipe, AppIcon],
  host: { '(document:keydown.escape)': 'closePlayer()' },
  template: `
    <div class="page">
      <div>
        <p class="eyebrow">Recording library</p>
        <h2 class="page-title">Recordings</h2>
        <p class="muted">Find and review footage across your cameras.</p>
      </div>

      <div class="card filters">
        <label><span>Location</span>
          <select [value]="location()" (change)="setLocation($any($event.target).value)">
            <option value="">All locations</option>
            <option value="Interior">Interior</option>
            <option value="Exterior">Exterior</option>
          </select>
        </label>
        <label><span>Camera</span>
          <select [value]="cameraId()" (change)="setCamera($any($event.target).value)">
            <option value="">All Cameras</option>
            @for (camera of filteredCameras(); track camera.id) {
              <option [value]="camera.id">{{ camera.name }}</option>
            }
          </select>
        </label>
        <label><span>Detections</span>
          <select [value]="detections()" (change)="setDetections($any($event.target).value)">
            <option value="all">All recordings</option>
            <option value="yes">With detections</option>
            <option value="no">Without detections</option>
          </select>
        </label>
        <label><span>Date</span>
          <input type="date" [value]="date()" (change)="setDate($any($event.target).value)">
        </label>
        @if (filtersActive()) {
          <button type="button" class="btn outline sm" (click)="clearFilters()">Clear filters</button>
        }
      </div>

      @if (loading()) { <p class="muted status">Loading recordings…</p> }
      @if (error(); as message) { <p class="load-error" role="alert">{{ message }}</p> }
      @if (playbackError(); as message) { <p class="load-error" role="alert">{{ message }}</p> }

      @if (selected(); as recording) {
        <section class="card player">
          @if (recording.available) {
            <div class="stage">
              <video
                #video
                controls
                [src]="'/api/recordings/' + recording.id + '/video.mp4'"
                (loadedmetadata)="syncVideo()"
                (timeupdate)="syncVideo()"
                (play)="playing.set(true)"
                (pause)="playing.set(false)"
                (ended)="playing.set(false)"
                (error)="playbackError.set('The recording could not be played.')"
              ></video>
              <button type="button" class="shot" aria-label="Download screenshot" title="Download screenshot" (click)="snapshot(recording)">
                <app-icon name="camera" />
              </button>
            </div>
            <div class="timeline-wrap">
              <div class="bands" aria-hidden="true">
                @for (band of detectionBands(recording); track $index) {
                  <i [style.left.%]="band.left" [style.width.%]="band.width"></i>
                }
              </div>
              <input
                class="timeline"
                type="range"
                min="0"
                [max]="durationSeconds() || 0"
                step="0.01"
                [value]="currentSeconds()"
                aria-label="Recording position"
                (input)="seek($any($event.target).value)"
              >
            </div>
            <div class="controls">
              <button type="button" class="btn outline sm" (click)="togglePlayback()">{{ playing() ? 'Pause' : 'Play' }}</button>
              <span>{{ clock(currentSeconds()) }} / {{ clock(durationSeconds()) }}</span>
              <span class="legend"><i></i>Detection</span>
            </div>
          } @else {
            <p class="load-error">This recording is indexed, but its file is no longer available.</p>
          }
          <div class="player-bar">
            <div><strong>{{ recording.cameraName }}</strong><span>{{ recording.location }} · {{ recording.startUtc | date:'medium' }}</span></div>
            <div class="player-actions">
              @if (recording.available) {
                <button type="button" class="btn outline sm" (click)="download(recording)"><app-icon name="download" />Download</button>
              }
              <button type="button" class="btn outline sm" (click)="closePlayer()"><app-icon name="x" />Close</button>
            </div>
          </div>
        </section>
      }

      <div class="grid recordings">
        @for (recording of recordings(); track recording.id) {
          <article class="card recording" (click)="select(recording)">
            <div class="thumb">
              @if (recording.hasThumbnail && !thumbnailFailed().has(recording.id)) {
                <img [src]="'/api/recordings/' + recording.id + '/thumbnail'" alt="" (error)="markThumbnailFailed(recording.id)">
              }
              <span>{{ durationLabel(recording.startUtc, recording.endUtc) }}</span>
              @if (recording.isActive) { <span class="live">Recording</span> }
            </div>
            <div class="body">
              <div class="title-row">
                <div>
                  <h3>{{ recording.cameraName }}</h3>
                  <p class="muted">{{ recording.location }} · {{ when(recording.startUtc) }}{{ quality(recording.resolution) }}</p>
                </div>
                <span class="size">{{ recording.byteSize / 1048576 | number:'1.1-1' }} MB</span>
              </div>
              <div class="foot">
                <span>{{ recording.detectionIntervals?.length || 0 }} detection intervals</span>
                @if (recording.available) {
                  <button type="button" class="icon-btn" aria-label="Download recording" (click)="download(recording); $event.stopPropagation()"><app-icon name="download" /></button>
                }
              </div>
            </div>
          </article>
        }
      </div>
      @if (!loading() && recordings().length === 0) { <p class="muted status">No recordings found.</p> }
      @if (total() > 0) {
        <nav class="pagination" aria-label="Recording pages">
          <span>{{ range() }} of {{ total() }}</span>
          <div>
            <button type="button" class="btn outline sm" [disabled]="page() === 1" (click)="setPage(page() - 1)">Previous</button>
            <span>Page {{ page() }} of {{ totalPages() }}</span>
            <button type="button" class="btn outline sm" [disabled]="page() >= totalPages()" (click)="setPage(page() + 1)">Next</button>
          </div>
        </nav>
      }
    </div>
  `,
  styles: `
    .filters { display: flex; flex-wrap: wrap; align-items: end; gap: .65rem; padding: .75rem; }
    .filters label { display: grid; gap: .25rem; color: var(--muted-foreground); font-size: .68rem; font-weight: 600; text-transform: uppercase; }
    .filters select, .filters input { min-height: 2.25rem; border: 1px solid var(--border); border-radius: .45rem; padding: .35rem .65rem; background: var(--card); color: var(--foreground); font: inherit; text-transform: none; }
    .recordings { grid-template-columns: repeat(4, minmax(0, 1fr)); }
    @media (max-width: 80rem) { .recordings { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
    @media (max-width: 64rem) { .recordings { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 40rem) { .recordings { grid-template-columns: 1fr; } }
    .recording { cursor: pointer; }
    .thumb { aspect-ratio: 16/9; background: linear-gradient(#0f172a, #020617); position: relative; overflow: hidden; }
    .thumb img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .thumb span { position: absolute; left: .75rem; bottom: .75rem; background: rgb(0 0 0 / .55); color: #fff; font-size: 11px; padding: .2rem .4rem; border-radius: .25rem; }
    .thumb span.live { left: auto; right: .75rem; background: #dc2626; }
    .body { padding: 1rem; }
    h3 { margin: 0; font-size: .875rem; font-weight: 500; }
    .title-row, .foot, .player-bar, .controls, .pagination, .pagination div { display: flex; align-items: center; justify-content: space-between; gap: .75rem; }
    .title-row > div, .player-bar > div { min-width: 0; }
    .title-row p { margin: .25rem 0 0; }
    .size { background: rgb(249 115 22 / .1); color: #ea580c; font-size: 10px; padding: .2rem .4rem; border-radius: .25rem; white-space: nowrap; }
    .foot { border-top: 1px solid var(--border); margin-top: .75rem; padding-top: .5rem; font-size: .75rem; color: var(--muted-foreground); }
    .foot .icon-btn { border: 0; background: transparent; color: inherit; cursor: pointer; }
    .player { padding: 1rem; }
    .stage { position: relative; }
    video { display: block; width: 100%; max-height: 65vh; border-radius: .5rem .5rem 0 0; background: #000; }
    .shot {
      position: absolute; right: .75rem; bottom: 3.1rem; z-index: 2;
      display: inline-grid; place-items: center; width: 2rem; height: 2rem; padding: 0;
      border: 0; border-radius: 999px; color: #fff; background: rgb(0 0 0 / 0.5); cursor: pointer;
      opacity: 0; pointer-events: none; transition: opacity 0.15s ease;
    }
    .stage:hover .shot, .shot:focus-visible { opacity: 1; pointer-events: auto; }
    .shot app-icon { width: 1rem; height: 1rem; }
    .timeline-wrap { position: relative; height: 1.4rem; margin-top: .45rem; }
    .bands { position: absolute; inset: .38rem 0; border-radius: 999px; overflow: hidden; background: var(--muted); pointer-events: none; }
    .bands i { position: absolute; top: 0; bottom: 0; min-width: 2px; background: #f97316; }
    .timeline { position: absolute; inset: 0; width: 100%; margin: 0; opacity: .72; cursor: pointer; accent-color: var(--primary); }
    .controls { justify-content: flex-start; color: var(--muted-foreground); font-size: .75rem; }
    .legend { margin-left: auto; display: inline-flex; align-items: center; gap: .35rem; }
    .legend i { width: .65rem; height: .65rem; border-radius: .15rem; background: #f97316; }
    .player-bar { margin-top: .75rem; }
    .player-actions { display: flex; align-items: center; gap: .5rem; }
    .player-bar strong, .player-bar span { display: block; }
    .player-bar span { margin-top: .2rem; color: var(--muted-foreground); font-size: .75rem; }
    .status { padding: .75rem 0; }
    .load-error { color: #dc2626; padding: .75rem; margin: 0; }
    .pagination { font-size: .75rem; color: var(--muted-foreground); }
    @media (max-width: 40rem) { .pagination { align-items: flex-start; flex-direction: column; } }
  `,
})
export class RecordingsPage {
  private readonly api = inject(RecordingApi);
  protected readonly store = inject(CameraStore);
  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');
  private readonly pageSize = 20;
  protected readonly recordings = signal<RecordingDto[]>([]);
  protected readonly selected = signal<RecordingDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly playbackError = signal<string | null>(null);
  protected readonly location = signal('');
  protected readonly cameraId = signal('');
  protected readonly detections = signal<'all' | 'yes' | 'no'>('all');
  protected readonly date = signal('');
  protected readonly page = signal(1);
  protected readonly total = signal(0);
  protected readonly totalPages = signal(1);
  protected readonly thumbnailFailed = signal<ReadonlySet<string>>(new Set());
  protected readonly currentSeconds = signal(0);
  protected readonly durationSeconds = signal(0);
  protected readonly playing = signal(false);
  private pollHandle?: ReturnType<typeof setInterval>;
  private loadSequence = 0;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stopPolling());
    void this.load();
  }

  protected filteredCameras() {
    return this.store.cameras().filter(camera => !this.location() || camera.location === this.location());
  }

  protected filtersActive() { return !!this.location() || !!this.cameraId() || this.detections() !== 'all' || !!this.date(); }
  protected setLocation(value: string) {
    this.location.set(value);
    if (!this.filteredCameras().some(camera => camera.id === this.cameraId())) this.cameraId.set('');
    this.resetAndLoad();
  }
  protected setCamera(value: string) { this.cameraId.set(value); this.resetAndLoad(); }
  protected setDetections(value: string) { this.detections.set(value === 'yes' || value === 'no' ? value : 'all'); this.resetAndLoad(); }
  protected setDate(value: string) { this.date.set(value); this.resetAndLoad(); }
  protected clearFilters() {
    this.location.set(''); this.cameraId.set(''); this.detections.set('all'); this.date.set('');
    this.resetAndLoad();
  }
  protected setPage(page: number) { this.page.set(page); this.selected.set(null); void this.load(); }
  protected select(recording: RecordingDto) {
    this.playbackError.set(null); this.currentSeconds.set(0); this.durationSeconds.set(0); this.playing.set(false); this.selected.set(recording);
  }
  protected closePlayer() {
    this.video()?.nativeElement.pause();
    this.selected.set(null);
    this.playing.set(false);
    this.playbackError.set(null);
    this.currentSeconds.set(0);
    this.durationSeconds.set(0);
  }
  protected markThumbnailFailed(id: string) { this.thumbnailFailed.update(failed => new Set(failed).add(id)); }
  protected download(recording: RecordingDto) { window.open(`/api/recordings/${recording.id}/video.mp4`, '_blank'); }
  protected snapshot(recording: RecordingDto): void {
    const at = new Date(new Date(recording.startUtc).getTime() + this.currentSeconds() * 1000);
    if (!downloadVideoFrame(this.video()?.nativeElement, recording.cameraName, at)) {
      this.playbackError.set('Could not capture a screenshot from this recording yet.');
    }
  }

  protected syncVideo() {
    const video = this.video()?.nativeElement;
    if (!video) return;
    this.currentSeconds.set(Number.isFinite(video.currentTime) ? video.currentTime : 0);
    this.durationSeconds.set(Number.isFinite(video.duration) ? video.duration : this.recordingDuration(this.selected()));
  }
  protected seek(value: string) {
    const video = this.video()?.nativeElement;
    if (video) video.currentTime = Number(value);
    this.currentSeconds.set(Number(value));
  }
  protected togglePlayback() {
    const video = this.video()?.nativeElement;
    if (!video) return;
    if (video.paused) void video.play().catch(() => this.playbackError.set('Playback could not be started.'));
    else video.pause();
  }
  protected detectionBands(recording: RecordingDto) {
    const start = new Date(recording.startUtc).getTime();
    const duration = Math.max(1, new Date(recording.endUtc).getTime() - start);
    return (recording.detectionIntervals ?? []).map(interval => {
      const intervalStart = Math.max(start, new Date(interval.startUtc).getTime());
      const intervalEnd = Math.min(start + duration, new Date(interval.endUtc).getTime());
      return { left: ((intervalStart - start) / duration) * 100, width: Math.max(0, ((intervalEnd - intervalStart) / duration) * 100) };
    }).filter(band => band.width > 0);
  }
  protected clock(seconds: number) {
    const safe = Math.max(0, Math.floor(seconds || 0));
    const pad = (value: number) => String(value).padStart(2, '0');
    const hours = Math.floor(safe / 3600);
    return hours ? `${hours}:${pad(Math.floor((safe % 3600) / 60))}:${pad(safe % 60)}` : `${Math.floor(safe / 60)}:${pad(safe % 60)}`;
  }
  protected durationLabel(start: string, end: string) { return this.clock(Math.round((new Date(end).getTime() - new Date(start).getTime()) / 1000)); }
  protected when(value: string) {
    const date = new Date(value);
    return `${date.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} at ${date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })}`;
  }
  protected quality(resolution: string | null) {
    const height = resolution?.split(/[×x]/)[1]?.trim();
    return height ? ` · ${height}p` : '';
  }
  protected range() {
    const start = (this.page() - 1) * this.pageSize + 1;
    return `${start}–${Math.min(this.page() * this.pageSize, this.total())}`;
  }

  private resetAndLoad() { this.page.set(1); this.selected.set(null); void this.load(); }
  private recordingDuration(recording: RecordingDto | null) {
    return recording ? Math.max(0, (new Date(recording.endUtc).getTime() - new Date(recording.startUtc).getTime()) / 1000) : 0;
  }
  private dateRange() {
    if (!this.date()) return {};
    const from = new Date(`${this.date()}T00:00:00`);
    const to = new Date(from); to.setDate(to.getDate() + 1);
    return { fromUtc: from.toISOString(), toUtc: to.toISOString() };
  }
  private async load(silent = false) {
    const sequence = ++this.loadSequence;
    if (!silent) this.loading.set(true);
    this.error.set(null);
    const detection = this.detections();
    try {
      const result = await this.api.search({
        location: this.location() || undefined,
        cameraId: this.cameraId() || undefined,
        ...(detection === 'yes' ? { hasDetections: true } : detection === 'no' ? { hasDetections: false } : {}),
        ...this.dateRange(),
        page: this.page(),
        pageSize: this.pageSize,
      });
      if (sequence !== this.loadSequence) return;
      this.recordings.set(result.items ?? []);
      this.total.set(result.totalCount ?? 0);
      this.totalPages.set(result.totalPages ?? 1);
      const selected = this.selected();
      if (selected) {
        const updated = result.items?.find(recording => recording.id === selected.id);
        if (updated) this.selected.set(updated);
      }
    } catch {
      if (sequence !== this.loadSequence) return;
      this.recordings.set([]);
      this.total.set(0);
      this.error.set('Could not load recordings. Check the server connection and sign in again.');
    } finally {
      if (sequence === this.loadSequence && !silent) this.loading.set(false);
    }
    this.syncPolling();
  }
  private syncPolling() {
    if (this.recordings().some(recording => recording.isActive)) this.pollHandle ??= setInterval(() => void this.load(true), 10_000);
    else this.stopPolling();
  }
  private stopPolling() { if (this.pollHandle) clearInterval(this.pollHandle); this.pollHandle = undefined; }
}
