import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, CanActivateFn } from '@angular/router';
import { tap } from 'rxjs';
import { PortalRole, TokenResponse } from './models';

@Injectable({ providedIn: 'root' })
export class AuthService implements OnDestroy {
  private readonly storageKey = 'sms-console-session';
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private token: string | null = null;
  private expiresAt = 0;
  private timer?: ReturnType<typeof setTimeout>;
  readonly role = signal<PortalRole | null>(null);
  readonly identity = signal('');
  readonly expired = signal(false);

  constructor() {
    try {
      const saved = sessionStorage.getItem(this.storageKey);
      if (!saved) return;
      const session = JSON.parse(saved);
      if (!session || typeof session.token !== 'string' || typeof session.identity !== 'string'
        || !['tenant', 'admin'].includes(session.role)) throw new Error('Invalid session.');
      this.accept(session.token, session.role, session.identity);
    } catch {
      this.clearStoredSession();
    }
  }

  ngOnDestroy() { clearTimeout(this.timer); }

  loginTenant(clientId: string, clientSecret: string) {
    return this.http.post<TokenResponse>('/api/v1/auth/token', { clientId, clientSecret })
      .pipe(tap(value => this.accept(value.access_token, 'tenant', clientId)));
  }
  loginAdmin(key: string) {
    return this.http.post<TokenResponse>('/api/v1/admin/auth/token', { key })
      .pipe(tap(value => this.accept(value.access_token, 'admin', 'Administrator')));
  }
  bearer(): string | null { return Date.now() < this.expiresAt ? this.token : null; }

  logout(expired = false) {
    clearTimeout(this.timer);
    this.clearStoredSession();
    this.token = null;
    this.expiresAt = 0;
    this.role.set(null);
    this.identity.set('');
    this.expired.set(expired);
    void this.router.navigateByUrl('/login');
  }

  private accept(token: string, role: PortalRole, identity: string) {
    // Decode expiry only for UX. All authorization is performed by the API.
    if (token.split('.').length !== 3) throw new Error('Invalid session.');
    const part = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const claims = JSON.parse(atob(part.padEnd(Math.ceil(part.length / 4) * 4, '='))) as { exp: number };
    if (!Number.isFinite(claims.exp) || claims.exp * 1000 <= Date.now()) throw new Error('Invalid session.');
    clearTimeout(this.timer);
    this.token = token;
    this.expiresAt = claims.exp * 1000;
    this.role.set(role);
    this.identity.set(identity);
    this.expired.set(false);
    this.timer = setTimeout(() => this.logout(true), this.expiresAt - Date.now());
    try {
      sessionStorage.setItem(this.storageKey, JSON.stringify({ token, role, identity }));
    } catch {
      // Storage may be blocked; the current in-memory session still works.
    }
  }

  private clearStoredSession() {
    try { sessionStorage.removeItem(this.storageKey); } catch { /* Browser storage may be blocked. */ }
  }
}

export const roleGuard: CanActivateFn = route => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.bearer()) return router.parseUrl('/login');
  return auth.role() === route.data['role'] || router.parseUrl(auth.role() === 'admin' ? '/admin/tenants' : '/app');
};
