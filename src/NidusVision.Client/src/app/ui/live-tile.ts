import { afterNextRender, Component, DestroyRef, ElementRef, inject, input, signal, viewChild } from '@angular/core';
import { CameraItem, fpsLabel, statLabel } from '../models';
import { downloadVideoFrame } from '../video-frame';
import { AppIcon } from './app-icon';
import { StatusPill } from './status-pill';

@Component({
  selector: 'app-live-tile',
  imports: [StatusPill, AppIcon],
  template: `
    <article class="feed" [class.off]="camera().status === 'offline' && !!error()">
      <div class="video">
        <video #video muted autoplay playsinline></video>
        @if (error(); as message) {
          <p class="error">{{ message }}</p>
        }
        <div class="top"><span class="chip">{{ camera().name }}</span><app-status-pill [status]="camera().status" /></div>
        <div class="bot">
          <span>{{ statLabel(camera().resolution) }} · {{ fpsLabel(camera().fps) }}</span>
          <span class="live-meta">
            <button type="button" class="shot" aria-label="Download screenshot" title="Download screenshot" (click)="snapshot()">
              <app-icon name="camera" />
            </button>
            Live · {{ clock() }}
          </span>
        </div>
      </div>
    </article>
  `,
  styles: `
    .feed { overflow: hidden; border-radius: 0.75rem; border: 1px solid var(--border); background: #020617; }
    .video { position: relative; aspect-ratio: 16/9; background: linear-gradient(180deg, #1e293b, #020617); }
    video { width: 100%; height: 100%; object-fit: cover; }
    .feed.off { filter: grayscale(1); opacity: 0.5; }
    .top, .bot { position: absolute; left: 0.75rem; right: 0.75rem; color: #fff; font-size: 11px; }
    .top { top: 0.75rem; }
    .bot { bottom: 0.75rem; display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; }
    .bot span { white-space: nowrap; }
    .live-meta { display: inline-flex; align-items: center; gap: 0.4rem; }
    .shot {
      display: inline-grid; place-items: center; width: 1.6rem; height: 1.6rem; padding: 0;
      border: 0; border-radius: 999px; color: #fff; background: rgb(0 0 0 / 0.45); cursor: pointer;
      opacity: 0; pointer-events: none; transition: opacity 0.15s ease;
    }
    .video:hover .shot, .shot:focus-visible { opacity: 1; pointer-events: auto; }
    .shot app-icon { width: 0.9rem; height: 0.9rem; }
    .chip { background: rgb(0 0 0 / 0.5); padding: 0.2rem 0.5rem; border-radius: 0.35rem; }
    .error {
      position: absolute; inset: 2.75rem 1rem; margin: 0; display: grid; place-content: center;
      text-align: center; color: #fca5a5; font-size: 0.75rem; line-height: 1.4;
    }
  `,
})
export class LiveTile {
  readonly camera = input.required<CameraItem>();
  protected readonly error = signal<string | null>(null);
  protected readonly clock = signal(formatClock(new Date()));
  protected readonly statLabel = statLabel;
  protected readonly fpsLabel = fpsLabel;
  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');
  private readonly abort = new AbortController();
  private objectUrl: string | null = null;
  private media: MediaSource | null = null;
  private reader: ReadableStreamDefaultReader<Uint8Array> | null = null;
  private clockHandle?: ReturnType<typeof setInterval>;

  constructor() {
    this.clockHandle = setInterval(() => this.clock.set(formatClock(new Date())), 1000);
    inject(DestroyRef).onDestroy(() => {
      if (this.clockHandle) {
        clearInterval(this.clockHandle);
      }
      void this.teardown();
    });
    afterNextRender(() => void this.attach());
  }

  protected snapshot(): void {
    if (!downloadVideoFrame(this.video()?.nativeElement, this.camera().name, new Date())) {
      this.error.set('Could not capture a screenshot from this camera yet.');
    }
  }

  private async attach(): Promise<void> {
    const el = this.video()?.nativeElement;
    if (!el) {
      return;
    }
    if (typeof MediaSource === 'undefined') {
      this.error.set('This browser cannot play live streams (no Media Source Extensions).');
      return;
    }

    const mime = 'video/mp4; codecs="avc1.42E01E"';
    if (!MediaSource.isTypeSupported(mime)) {
      this.error.set('This browser cannot decode the camera video codec (H.264 expected).');
      return;
    }

    const media = new MediaSource();
    this.media = media;
    this.objectUrl = URL.createObjectURL(media);
    el.src = this.objectUrl;
    await new Promise<void>(resolve => media.addEventListener('sourceopen', () => resolve(), { once: true }));
    if (this.abort.signal.aborted || media.readyState !== 'open') {
      return;
    }

    let buffer: SourceBuffer;
    try {
      buffer = media.addSourceBuffer(mime);
    } catch {
      this.error.set('This browser cannot decode the camera video codec (H.264 expected).');
      return;
    }

    try {
      const response = await fetch(`/api/cameras/${this.camera().id}/live`, {
        credentials: 'include',
        signal: this.abort.signal,
      });
      if (!response.ok || !response.body) {
        this.error.set(await this.readError(response));
        return;
      }

      this.reader = response.body.getReader();
      const queue: Uint8Array[] = [];
      const pump = async () => {
        while (queue.length && !buffer.updating && media.readyState === 'open') {
          await this.pruneBuffer(buffer, el);
          if (buffer.updating || media.readyState !== 'open') {
            return;
          }
          buffer.appendBuffer(queue.shift()!.slice());
          await new Promise<void>(resolve => buffer.addEventListener('updateend', () => resolve(), { once: true }));
        }
      };
      let received = 0;
      while (!this.abort.signal.aborted) {
        const { done, value } = await this.reader.read();
        if (done) break;
        if (value) {
          received += value.byteLength;
          queue.push(value);
          await pump();
        }
      }
      if (received === 0 && !this.abort.signal.aborted) {
        this.error.set('The camera stream ended without sending any video.');
      }
    } catch (err) {
      if (!this.abort.signal.aborted) {
        this.error.set('The live stream was interrupted.');
      }
      void err;
    }
  }

  private async pruneBuffer(buffer: SourceBuffer, el: HTMLVideoElement): Promise<void> {
    if (buffer.updating || !buffer.buffered.length) {
      return;
    }
    const end = buffer.buffered.end(buffer.buffered.length - 1);
    const start = buffer.buffered.start(0);
    const keepFrom = Math.max(0, (el.currentTime || end) - 10);
    if (end - start > 15 && start < keepFrom) {
      buffer.remove(start, keepFrom);
      await new Promise<void>(resolve => buffer.addEventListener('updateend', () => resolve(), { once: true }));
    }
  }

  private async teardown(): Promise<void> {
    this.abort.abort();
    try {
      await this.reader?.cancel();
    } catch {
      /* already closed */
    }
    this.reader = null;
    const el = this.video()?.nativeElement;
    if (el) {
      el.removeAttribute('src');
      el.load();
    }
    if (this.media && this.media.readyState === 'open') {
      try {
        this.media.endOfStream();
      } catch {
        /* source already closed */
      }
    }
    this.media = null;
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }

  private async readError(response: Response): Promise<string> {
    try {
      const body = (await response.json()) as { message?: string };
      if (body.message) {
        return body.message;
      }
    } catch {
      /* non-JSON error body */
    }
    return `Live stream unavailable (HTTP ${response.status}).`;
  }
}

function formatClock(at: Date): string {
  const pad = (value: number) => value.toString().padStart(2, '0');
  return `${pad(at.getHours())}:${pad(at.getMinutes())}:${pad(at.getSeconds())}`;
}
