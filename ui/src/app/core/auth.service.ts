import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, CanActivateFn } from '@angular/router';
import { tap } from 'rxjs';
import { PortalContext, PortalPermissionRole, PortalRole, TokenResponse } from './models';

@Injectable({ providedIn: 'root' })
export class AuthService implements OnDestroy {
  private readonly storageKey = 'sms-ui-session';
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private token: string | null = null;
  private expiresAt = 0;
  private timer?: ReturnType<typeof setTimeout>;
  readonly role = signal<PortalRole | null>(null);
  readonly context = signal<PortalContext | null>(null);
  readonly permissionRole = signal<PortalPermissionRole | null>(null);
  readonly identity = signal('');
  readonly expired = signal(false);

  constructor() {
    try {
      const saved = sessionStorage.getItem(this.storageKey);
      if (!saved) return;
      const session = JSON.parse(saved);
      if (!session || typeof session.token !== 'string' || typeof session.identity !== 'string')
        throw new Error('Invalid session.');
      this.accept(session.token, session.identity, session.context ?? (session.role === 'admin' ? 'platform' : 'tenant'));
    } catch {
      this.clearStoredSession();
    }
  }

  ngOnDestroy() { clearTimeout(this.timer); }

  loginTenant(clientId: string, clientSecret: string) {
    return this.http.post<TokenResponse>('/api/v1/auth/token', { clientId, clientSecret })
      .pipe(tap(value => this.accept(value.access_token, clientId, 'tenant')));
  }

  loginAdmin(username: string, password: string) {
    return this.http.post<TokenResponse>('/api/v1/admin/auth/token', { username, password })
      .pipe(tap(value => this.accept(value.access_token, username, 'platform')));
  }

  loginPortal(username: string, password: string, context: PortalContext, tenantCode?: string) {
    const request = context === 'tenant'
      ? { username, password, context, tenantCode }
      : { username, password, context };
    return this.http.post<TokenResponse>('/api/v1/portal/auth/token', request)
      .pipe(tap(value => this.accept(value.access_token, username, context)));
  }

  bearer(): string | null { return Date.now() < this.expiresAt ? this.token : null; }

  logout(expired = false) {
    clearTimeout(this.timer);
    this.clearStoredSession();
    this.token = null;
    this.expiresAt = 0;
    this.role.set(null);
    this.context.set(null);
    this.permissionRole.set(null);
    this.identity.set('');
    this.expired.set(expired);
    void this.router.navigateByUrl('/login');
  }

  private accept(token: string, identity: string, fallbackContext: PortalContext = 'tenant') {
    if (token.split('.').length !== 3) throw new Error('Invalid session.');
    const part = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const claims = JSON.parse(atob(part.padEnd(Math.ceil(part.length / 4) * 4, '='))) as {
      exp: number; context?: PortalContext; role?: PortalPermissionRole; platform_admin?: string;
    };
    if (!Number.isFinite(claims.exp) || claims.exp * 1000 <= Date.now()) throw new Error('Invalid session.');

    const context = claims.context ?? (claims.platform_admin === 'true' ? 'platform' : fallbackContext);
    const permissionRole = claims.role ?? (claims.platform_admin === 'true' ? 'administrator' : 'user');
    const portalRole: PortalRole = context === 'platform' ? 'admin' : 'tenant';

    clearTimeout(this.timer);
    this.token = token;
    this.expiresAt = claims.exp * 1000;
    this.role.set(portalRole);
    this.context.set(context);
    this.permissionRole.set(permissionRole);
    this.identity.set(identity);
    this.expired.set(false);
    this.timer = setTimeout(() => this.logout(true), this.expiresAt - Date.now());

    try {
      sessionStorage.setItem(this.storageKey, JSON.stringify({
        token, identity, role: portalRole, context, permissionRole
      }));
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
