import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface EventDto {
  id: string;
  cameraId: string;
  cameraName: string;
  startUtc: string;
  endUtc: string;
  confidence: number;
  hasThumbnail: boolean;
}

export interface RecordingDto {
  id: string;
  cameraId: string;
  cameraName: string;
  startUtc: string;
  endUtc: string;
  byteSize: number;
  hasHuman: boolean;
  available: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface LibraryQuery {
  cameraId: string | null;
  page: number;
  pageSize: number;
  minConfidence?: number;
  fromUtc?: string;
  toUtc?: string;
}

@Injectable({ providedIn: 'root' })
export class EventApi {
  private readonly http = inject(HttpClient);
  search(query: LibraryQuery) {
    return firstValueFrom(this.http.get<PagedResult<EventDto>>('/api/events', { params: toParams(query) }));
  }
  get(id: string) {
    return firstValueFrom(this.http.get<EventDto>(`/api/events/${id}`));
  }
  delete(id: string) {
    return firstValueFrom(this.http.delete(`/api/events/${id}`));
  }
  recordings(query: LibraryQuery) {
    return firstValueFrom(this.http.get<PagedResult<RecordingDto>>('/api/recordings', { params: toParams(query) }));
  }
}

function toParams(query: LibraryQuery): Record<string, string | number> {
  const params: Record<string, string | number> = {
    page: query.page,
    pageSize: query.pageSize,
  };
  if (query.cameraId) params['cameraId'] = query.cameraId;
  if (query.minConfidence !== undefined && query.minConfidence > 0) {
    params['minConfidence'] = query.minConfidence;
  }
  if (query.fromUtc) params['fromUtc'] = query.fromUtc;
  if (query.toUtc) params['toUtc'] = query.toUtc;
  return params;
}
