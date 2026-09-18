import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { AuthService } from './auth.service';
import { authInterceptor, errorMessage } from './api';

describe('Authenticated requests', () => {
  let auth: AuthService; let http: HttpTestingController; let client: HttpClient;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([])] });
    auth = TestBed.inject(AuthService); http = TestBed.inject(HttpTestingController); client = TestBed.inject(HttpClient);
    vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    auth.loginTenant('client', 'secret').subscribe();
    http.expectOne('/api/v1/auth/token').flush({ access_token: `h.${btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 900 }))}.s` });
  });
  afterEach(() => { auth.logout(); http.verify(); });
  it('sends credentials only to relative API URLs', () => {
    client.get('/api/v1/messages').subscribe();
    const api = http.expectOne('/api/v1/messages'); expect(api.request.headers.get('Authorization')).toBe(`Bearer ${auth.bearer()}`); api.flush([]);
    client.get('https://example.invalid/resource').subscribe();
    const external = http.expectOne('https://example.invalid/resource'); expect(external.request.headers.has('Authorization')).toBe(false); external.flush({});
  });
  it('ends the session when the API revokes access', () => {
    client.get('/api/v1/messages').subscribe({ error: () => undefined });
    http.expectOne('/api/v1/messages').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(auth.role()).toBeNull(); expect(auth.expired()).toBe(true);
    expect(sessionStorage.getItem('sms-console-session')).toBeNull();
  });
  it('never renders provider or server error bodies', () => {
    const error = new HttpErrorResponse({ status: 502, error: 'provider-secret-body' });
    expect(errorMessage(error)).not.toContain('provider-secret-body');
    expect(errorMessage(new HttpErrorResponse({ status: 429 }))).toContain('minute');
  });
});
