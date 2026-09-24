import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthApi } from './api/auth.api';

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthApi);
  const router = inject(Router);
  try {
    const status = auth.status() ?? (await auth.refresh());
    if (!status.authenticated) {
      await router.navigateByUrl('/login');
      return false;
    }
    return true;
  } catch {
    await router.navigateByUrl('/login');
    return false;
  }
};
