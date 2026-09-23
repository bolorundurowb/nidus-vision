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
  thumbnailPath: string | null;
}

@Injectable({ providedIn: 'root' })
export class EventApi {
  private readonly http = inject(HttpClient);
  search(q: string) {
    return firstValueFrom(this.http.get<EventDto[]>('/api/events', { params: { q } }));
  }
  get(id: string) {
    return firstValueFrom(this.http.get<EventDto>(`/api/events/${id}`));
  }
  delete(id: string) {
    return firstValueFrom(this.http.delete(`/api/events/${id}`));
  }
}
