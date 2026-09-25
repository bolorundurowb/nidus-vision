import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface DetectionIntervalDto {
  id: string;
  startUtc: string;
  endUtc: string;
  confidence: number;
}

export interface RecordingDto {
  id: string;
  cameraId: string;
  cameraName: string;
  location: string;
  startUtc: string;
  endUtc: string;
  byteSize: number;
  available: boolean;
  hasThumbnail: boolean;
  resolution: string | null;
  isActive: boolean;
  detectionIntervals?: DetectionIntervalDto[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface RecordingQuery {
  location?: string;
  cameraId?: string;
  fromUtc?: string;
  toUtc?: string;
  hasDetections?: boolean;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class RecordingApi {
  private readonly http = inject(HttpClient);

  search(query: RecordingQuery) {
    return firstValueFrom(this.http.get<PagedResult<RecordingDto>>('/api/recordings', { params: toParams(query) }));
  }
}

function toParams(query: RecordingQuery): Record<string, string | number> {
  const params: Record<string, string | number> = { page: query.page, pageSize: query.pageSize };
  if (query.location) params['location'] = query.location;
  if (query.cameraId) params['cameraId'] = query.cameraId;
  if (query.fromUtc) params['fromUtc'] = query.fromUtc;
  if (query.toUtc) params['toUtc'] = query.toUtc;
  if (query.hasDetections !== undefined) params['hasHuman'] = String(query.hasDetections);
  return params;
}
