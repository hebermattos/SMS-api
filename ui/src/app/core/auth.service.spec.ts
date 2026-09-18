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
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([])] });
    auth = TestBed.inject(AuthService); http = TestBed.inject(HttpTestingController);
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });
  afterEach(() => { auth.logout(); http.verify(); });

  it('authenticates tenants without persisting the bearer in browser storage', () => {
    const storage = vi.spyOn(Storage.prototype, 'setItem');
    auth.loginTenant('client', 'secret').subscribe();
    const request = http.expectOne('/api/v1/auth/token');
    expect(request.request.body).toEqual({ clientId: 'client', clientSecret: 'secret' });
    expect(request.request.headers.has('Authorization')).toBe(false);
    const bearer = token(); request.flush({ access_token: bearer, token_type: 'Bearer' });
    expect(auth.bearer()).toBe(bearer); expect(auth.role()).toBe('tenant'); expect(storage).not.toHaveBeenCalled();
    storage.mockRestore();
  });

  it('opens a separate administrator session and clears it on logout', () => {
    auth.loginAdmin('admin-key').subscribe();
    const request = http.expectOne('/api/v1/admin/auth/token');
    expect(request.request.body).toEqual({ key: 'admin-key' });
    request.flush({ access_token: token() });
    expect(auth.role()).toBe('admin'); auth.logout(); expect(auth.bearer()).toBeNull(); expect(auth.role()).toBeNull();
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
