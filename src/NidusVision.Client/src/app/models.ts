export type CameraStatus = 'recording' | 'online' | 'offline';
export type PageId = 'monitor' | 'recordings' | 'cameras' | 'settings';

export interface CameraItem {
  id: string;
  name: string;
  status: CameraStatus;
  resolution: string | null;
  fps: number | null;
  bitrate: string | null;
  retention: string | null;
  location: string;
  enabled: boolean;
  mainRtspUrl: string;
  hasCredentials?: boolean;
}

/** Shown wherever a camera stat has not been measured yet. */
export const STAT_PLACEHOLDER = '—';

export function statLabel(value: string | null | undefined): string {
  return value?.trim() || STAT_PLACEHOLDER;
}

export function fpsLabel(fps: number | null | undefined): string {
  return fps && fps > 0 ? `${fps} fps` : STAT_PLACEHOLDER;
}