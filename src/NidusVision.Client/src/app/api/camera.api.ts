import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CameraItem, CameraStatus } from '../models';

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
    return firstValueFrom(this.http.post<{ ok: boolean; message: string }>('/api/cameras/probe', body));
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
      mainRtspUrl: dto.mainRtspUrl,
    };
  }
}
