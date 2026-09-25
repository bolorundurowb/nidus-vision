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

export const CAMERA_SEED: CameraItem[] = [
  { id: '1', name: 'Front Yard', status: 'recording', resolution: '2560×1440', fps: 30, bitrate: '4.8 Mbps', retention: '23 days', location: 'Exterior', enabled: true, mainRtspUrl: 'rtsp://192.168.1.21:554/stream' },
  { id: '2', name: 'Driveway', status: 'recording', resolution: '1920×1080', fps: 25, bitrate: '3.2 Mbps', retention: '18 days', location: 'Exterior', enabled: true, mainRtspUrl: 'rtsp://192.168.1.22:554/stream' },
  { id: '3', name: 'Garage', status: 'online', resolution: '1920×1080', fps: 20, bitrate: '2.6 Mbps', retention: '31 days', location: 'Interior', enabled: true, mainRtspUrl: 'rtsp://192.168.1.23:554/stream' },
  { id: '4', name: 'Backyard', status: 'offline', resolution: '2560×1440', fps: 30, bitrate: null, retention: null, location: 'Exterior', enabled: false, mainRtspUrl: 'rtsp://192.168.1.24:554/stream' },
];
