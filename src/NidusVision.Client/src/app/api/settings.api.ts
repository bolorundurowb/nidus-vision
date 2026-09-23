import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface SettingsDto {
  generalRetentionDays: number;
  detectionRetentionDays: number;
  maxStorageBytes: number | null;
  inferenceEnabled: boolean;
  sampleFps: number;
  confidenceThreshold: number;
}

export interface MetricsDto {
  cpuPercent: number;
  memoryUsedBytes: number;
  memoryTotalBytes: number;
  uptime: string;
  inferenceLatencyMs: number | null;
  storage: { usedBytes: number; totalBytes: number; generalBytes: number; detectionBytes: number; databaseBytes: number };
  version: string;
}

@Injectable({ providedIn: 'root' })
export class SettingsApi {
  private readonly http = inject(HttpClient);
  get() { return firstValueFrom(this.http.get<SettingsDto>('/api/settings')); }
  save(body: SettingsDto) { return firstValueFrom(this.http.put<SettingsDto>('/api/settings', body)); }
  metrics() { return firstValueFrom(this.http.get<MetricsDto>('/api/system/metrics')); }
}
