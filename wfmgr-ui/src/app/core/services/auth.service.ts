import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AuthUser {
  userId: string;
  displayName: string;
  role: string;
}

export interface DevTokenRequest {
  userId: string;
  displayName?: string;
  role: string;
}

export interface ClaimInfo {
  type: string;
  value: string;
}

export interface DevTokenResponse {
  token: string;
  expiresAt: string;
  issuer: string;
  audience: string;
  claims: ClaimInfo[];
}

const TOKEN_KEY = 'wfmgr_auth_token';
const USER_KEY = 'wfmgr_auth_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  readonly currentUser = signal<AuthUser | null>(this.loadUser());
  readonly isAuthenticated = signal<boolean>(!!this.loadToken());

  getToken(): string | null {
    if (typeof window === 'undefined') {
      return null;
    }
    return localStorage.getItem(TOKEN_KEY);
  }

  requestDevToken(request: DevTokenRequest): Observable<DevTokenResponse> {
    return this.http.post<DevTokenResponse>(`${this.baseUrl}/api/auth/dev-token`, request).pipe(
      tap((response) => {
        this.storeToken(response.token, {
          userId: request.userId,
          displayName: request.displayName ?? request.userId,
          role: request.role,
        });
      })
    );
  }

  logout(): void {
    if (typeof window === 'undefined') {
      return;
    }
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
  }

  private storeToken(token: string, user: AuthUser): void {
    if (typeof window === 'undefined') {
      return;
    }
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
    this.currentUser.set(user);
    this.isAuthenticated.set(true);
  }

  private loadToken(): string | null {
    if (typeof window === 'undefined') {
      return null;
    }
    return localStorage.getItem(TOKEN_KEY);
  }

  private loadUser(): AuthUser | null {
    if (typeof window === 'undefined') {
      return null;
    }
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) {
      return null;
    }
    try {
      return JSON.parse(raw) as AuthUser;
    } catch {
      return null;
    }
  }
}
