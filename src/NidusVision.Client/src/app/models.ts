export type CameraStatus = 'recording' | 'online' | 'offline';
export type PageId = 'monitor' | 'events' | 'cameras' | 'settings';

export interface CameraItem {
  id: string;
  name: string;
  status: CameraStatus;
  resolution: string;
  fps: number;
  bitrate: string;
  retention: string;
  location: string;
  mainRtspUrl: string;
  roiJson?: string | null;
}

export const CAMERA_SEED: CameraItem[] = [
  { id: '1', name: 'Front Yard', status: 'recording', resolution: '2560×1440', fps: 30, bitrate: '4.8 Mbps', retention: '23 days', location: 'Exterior', mainRtspUrl: 'rtsp://192.168.1.21:554/stream' },
  { id: '2', name: 'Driveway', status: 'recording', resolution: '1920×1080', fps: 25, bitrate: '3.2 Mbps', retention: '18 days', location: 'Exterior', mainRtspUrl: 'rtsp://192.168.1.22:554/stream' },
  { id: '3', name: 'Garage', status: 'online', resolution: '1920×1080', fps: 20, bitrate: '2.6 Mbps', retention: '31 days', location: 'Interior', mainRtspUrl: 'rtsp://192.168.1.23:554/stream' },
  { id: '4', name: 'Backyard', status: 'offline', resolution: '2560×1440', fps: 30, bitrate: '—', retention: '—', location: 'Exterior', mainRtspUrl: 'rtsp://192.168.1.24:554/stream' },
];
