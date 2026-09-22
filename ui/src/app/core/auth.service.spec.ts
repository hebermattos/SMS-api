import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, provideRouter } from '@angular/router';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { AuthService, roleGuard } from './auth.service';
import { authInterceptor } from './api';

describe('Portal sessions', () => {
  let auth: AuthService; let http: HttpTestingController;
  const token = () => `header.${btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 900 }))}.signature`;
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([])] });
    auth = TestBed.inject(AuthService); http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });
  afterEach(() => { auth?.ngOnDestroy(); http?.verify(); vi.restoreAllMocks(); vi.useRealTimers(); sessionStorage.clear(); TestBed.resetTestingModule(); });

  it('persists only session details in tab storage, never the client secret', () => {
    auth.loginTenant('client', 'secret').subscribe();
    const request = http.expectOne('/api/v1/auth/token');
    expect(request.request.body).toEqual({ clientId: 'client', clientSecret: 'secret' });
    expect(request.request.headers.has('Authorization')).toBe(false);
    const bearer = token(); request.flush({ access_token: bearer, token_type: 'Bearer' });
    expect(auth.bearer()).toBe(bearer); expect(auth.role()).toBe('tenant');
    expect(JSON.parse(sessionStorage.getItem('sms-ui-session')!)).toEqual({ token: bearer, role: 'tenant', identity: 'client', context: 'tenant', permissionRole: 'user' });
    expect(localStorage.getItem('sms-ui-session')).toBeNull();
  });

  it('opens a separate administrator session and clears it on logout', () => {
    auth.loginAdmin('admin', 'admin-password').subscribe();
    const request = http.expectOne('/api/v1/admin/auth/token');
    expect(request.request.body).toEqual({ username: 'admin', password: 'admin-password' });
    request.flush({ access_token: token() });
    expect(auth.role()).toBe('admin');
    expect(sessionStorage.getItem('sms-ui-session')).not.toContain('admin-password');
    auth.logout(); http.expectOne('/api/v1/admin/auth/logout').flush(null); expect(auth.bearer()).toBeNull(); expect(auth.role()).toBeNull();
    expect(sessionStorage.getItem('sms-ui-session')).toBeNull();
  });

  it.each(['tenant', 'admin'] as const)('restores a %s session before route guards after reload', role => {
    const bearer = token();
    if (role === 'tenant') auth.loginTenant('client', 'secret').subscribe();
    else auth.loginAdmin('admin', 'admin-password').subscribe();
    http.expectOne(role === 'tenant' ? '/api/v1/auth/token' : '/api/v1/admin/auth/token').flush({ access_token: bearer });
    http.verify();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([])] });
    auth = TestBed.inject(AuthService); http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    expect(auth.bearer()).toBe(bearer);
    expect(auth.role()).toBe(role);
    expect(auth.identity()).toBe(role === 'tenant' ? 'client' : 'admin');
    const route = new ActivatedRouteSnapshot(); route.data = { role };
    expect(TestBed.runInInjectionContext(() => roleGuard(route, {} as RouterStateSnapshot))).toBe(true);
    http.expectNone('/api/v1/auth/token');
    http.expectNone('/api/v1/admin/auth/token');
  });

  it.each([
    'not-json', 'null',
    JSON.stringify({ token: 'invalid', role: 'tenant', identity: 'client' }),
    JSON.stringify({ token: 'h.e30.s', role: 'tenant', identity: 'client' }),
    JSON.stringify({ token: `h.${btoa(JSON.stringify({ exp: 1 }))}.s`, role: 'tenant', identity: 'client' }),
    JSON.stringify({ token: 'h.e30.s', role: 'unknown', identity: 'client' })
  ])('discards invalid or expired stored sessions: %s', saved => {
    sessionStorage.setItem('sms-ui-session', saved);
    const restored = TestBed.runInInjectionContext(() => new AuthService());
    expect(restored.bearer()).toBeNull(); expect(restored.role()).toBeNull();
    expect(sessionStorage.getItem('sms-ui-session')).toBeNull();
    restored.ngOnDestroy();
  });

  it('expires a restored session at its original expiry time', () => {
    vi.useFakeTimers();
    const bearer = `h.${btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 60 }))}.s`;
    sessionStorage.setItem('sms-ui-session', JSON.stringify({ token: bearer, role: 'tenant', identity: 'client' }));
    const restored = TestBed.runInInjectionContext(() => new AuthService());
    vi.runOnlyPendingTimers();
    const refresh = http.expectOne('/api/v1/portal/auth/refresh');
    expect(refresh.request.body).toEqual({});
    refresh.flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(restored.bearer()).toBeNull(); expect(restored.expired()).toBe(true);
    expect(sessionStorage.getItem('sms-ui-session')).toBeNull();
    restored.ngOnDestroy();
  });

  it('allows in-memory login when browser storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('Blocked'); });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('Blocked'); });
    vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => { throw new Error('Blocked'); });
    const restored = TestBed.runInInjectionContext(() => new AuthService());
    restored.loginTenant('client', 'secret').subscribe();
    const bearer = token(); http.expectOne('/api/v1/auth/token').flush({ access_token: bearer });
    expect(restored.bearer()).toBe(bearer);
    expect(() => restored.logout()).not.toThrow(); http.expectOne('/api/v1/portal/auth/logout').flush(null);
    restored.ngOnDestroy();
  });

  it('keeps failed logins unauthenticated', () => {
    auth.loginTenant('client', 'wrong').subscribe({ error: () => undefined });
    http.expectOne('/api/v1/auth/token').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.bearer()).toBeNull(); expect(auth.role()).toBeNull();
  });

  it('routes tenants away from administrator pages', () => {
    auth.loginTenant('client', 'secret').subscribe(); http.expectOne('/api/v1/auth/token').flush({ access_token: token() });
    const route = new ActivatedRouteSnapshot(); route.data = { role: 'admin' };
    const result = TestBed.runInInjectionContext(() => roleGuard(route, {} as RouterStateSnapshot));
    expect(String(result)).toBe('/app');
    route.data = { role: 'tenant' };
    expect(TestBed.runInInjectionContext(() => roleGuard(route, {} as RouterStateSnapshot))).toBe(true);
  });

  it('routes anonymous visitors to login', () => {
    const route = new ActivatedRouteSnapshot(); route.data = { role: 'tenant' };
    expect(String(TestBed.runInInjectionContext(() => roleGuard(route, {} as RouterStateSnapshot)))).toBe('/login');
  });
});
