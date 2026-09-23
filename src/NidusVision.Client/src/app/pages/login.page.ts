import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthApi } from '../api/auth.api';

@Component({
  selector: 'app-login-page',
  imports: [FormsModule],
  template: `
    <div class="login">
      <div class="card pad">
        <p class="eyebrow">Nidus Vision</p>
        <h2 class="page-title">{{ configured() ? 'Sign in' : 'Set admin password' }}</h2>
        <p class="muted">{{ configured() ? 'Local instance' : 'Choose a password of at least 8 characters.' }}</p>
        <label>Password<input class="input" type="password" [(ngModel)]="password" /></label>
        @if (error()) { <p class="err">{{ error() }}</p> }
        <button type="button" class="btn full" (click)="submit()">{{ configured() ? 'Sign in' : 'Save password' }}</button>
      </div>
    </div>
  `,
  styles: `
    .login { min-height: 100vh; display: grid; place-items: center; padding: 1.5rem; }
    .pad { width: min(24rem, 100%); padding: 1.5rem; display: flex; flex-direction: column; gap: 0.75rem; }
    label { font-size: 0.75rem; font-weight: 500; display: flex; flex-direction: column; gap: 0.4rem; }
    .err { color: #dc2626; font-size: 0.8rem; margin: 0; }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthApi);
  private readonly router = inject(Router);
  protected password = '';
  protected readonly configured = signal(true);
  protected readonly error = signal('');

  constructor() {
    void this.auth.refresh().then(s => this.configured.set(s.configured)).catch(() => this.configured.set(false));
  }

  protected async submit(): Promise<void> {
    this.error.set('');
    try {
      if (this.configured()) {
        await this.auth.login(this.password);
      } else {
        await this.auth.setup(this.password);
      }
      await this.router.navigateByUrl('/monitor');
    } catch {
      this.error.set('Could not authenticate.');
    }
  }
}
