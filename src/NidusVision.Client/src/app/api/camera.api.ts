import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CameraItem, CameraStatus } from '../models';
import { redactRtspUrl } from '../rtsp-url';

export interface CameraDto {
  id: string;
  name: string;
  location: string;
  enabled: boolean;
  mainRtspUrl: string;
  subRtspUrl: string | null;
  username: string | null;
  hasPassword: boolean;
  transport: string;
  status: string;
  roiJson: string | null;
}

export interface CameraWrite {
  name: string;
  location: string;
  enabled: boolean;
  mainRtspUrl: string;
  subRtspUrl: string | null;
  username: string | null;
  password: string | null;
  transport: string;
  roiJson: string | null;
}

export interface ProbeResult {
  ok: boolean;
  message: string;
  resolution?: string | null;
  fps?: number | null;
}

/** Builds a write payload; blank credentials mean "leave whatever is already stored". */
export function cameraWrite(input: {
  name: string;
  url: string;
  location?: string;
  username?: string | null;
  password?: string | null;
  roiJson?: string | null;
}): CameraWrite {
  return {
    name: input.name.trim() || 'New Camera',
    location: input.location?.trim() || 'Unassigned',
    enabled: true,
    mainRtspUrl: input.url.trim() || 'rtsp://192.168.1.20:554/stream',
    subRtspUrl: null,
    username: blankToNull(input.username),
    password: blankToNull(input.password),
    transport: 'tcp',
    roiJson: input.roiJson ?? null,
  };
}

function blankToNull(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

@Injectable({ providedIn: 'root' })
export class CameraApi {
  private readonly http = inject(HttpClient);

  list() {
    return firstValueFrom(this.http.get<CameraDto[]>('/api/cameras'));
  }

  create(body: CameraWrite) {
    return firstValueFrom(this.http.post<CameraDto>('/api/cameras', body));
  }

  update(id: string, body: CameraWrite) {
    return firstValueFrom(this.http.put<CameraDto>(`/api/cameras/${id}`, body));
  }

  delete(id: string) {
    return firstValueFrom(this.http.delete(`/api/cameras/${id}`));
  }

  probe(body: CameraWrite) {
    return firstValueFrom(this.http.post<ProbeResult>('/api/cameras/probe', body));
  }

  toItem(dto: CameraDto): CameraItem {
    return {
      id: dto.id,
      name: dto.name,
      status: (dto.status as CameraStatus) || 'offline',
      resolution: '—',
      fps: 0,
      bitrate: '—',
      retention: '—',
      location: dto.location,
      mainRtspUrl: redactRtspUrl(dto.mainRtspUrl, dto.hasPassword),
      roiJson: dto.roiJson,
      hasCredentials: dto.hasPassword,
    };
  }
}
