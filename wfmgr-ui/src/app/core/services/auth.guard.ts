import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Guards a route behind authentication.  Unauthenticated users are redirected
 * to the /login page.
 */
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return router.parseUrl('/login');
};

/**
 * Guards a route behind the Admin role.  Unauthenticated users are redirected
 * to /login; authenticated non-admin users are redirected to the dashboard.
 */
export const adminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    return router.parseUrl('/login');
  }

  const user = authService.currentUser();
  if (user?.role === 'Admin') {
    return true;
  }

  // Authenticated but not Admin — redirect to dashboard.
  return router.parseUrl('/');
};
