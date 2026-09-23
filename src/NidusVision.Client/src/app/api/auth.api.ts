import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface AuthStatus {
  configured: boolean;
  authenticated: boolean;
}

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  readonly status = signal<AuthStatus | null>(null);

  async refresh(): Promise<AuthStatus> {
    const status = await firstValueFrom(this.http.get<AuthStatus>('/api/auth/status'));
    this.status.set(status);
    return status;
  }

  async setup(password: string): Promise<AuthStatus> {
    const status = await firstValueFrom(this.http.post<AuthStatus>('/api/auth/setup', { password }));
    this.status.set(status);
    return status;
  }

  async login(password: string): Promise<AuthStatus> {
    const status = await firstValueFrom(this.http.post<AuthStatus>('/api/auth/login', { password }));
    this.status.set(status);
    return status;
  }

  async logout(): Promise<void> {
    await firstValueFrom(this.http.post('/api/auth/logout', {}));
    this.status.set({ configured: this.status()?.configured ?? true, authenticated: false });
  }
}
