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
  afterEach(() => { vi.useRealTimers(); auth.logout(); http.verify(); });
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
    expect(sessionStorage.getItem('sms-ui-session')).toBeNull();
  });
  it('retries rate-limited API requests after Retry-After without surfacing 429', async () => {
    vi.useFakeTimers();
    let result: unknown;
    let failure: unknown;
    client.get('/api/v1/messages').subscribe({ next: value => result = value, error: error => failure = error });

    const first = http.expectOne('/api/v1/messages');
    first.flush({}, { status: 429, statusText: 'Too Many Requests', headers: { 'Retry-After': '2' } });

    expect(failure).toBeUndefined();
    expect(result).toBeUndefined();

    await vi.advanceTimersByTimeAsync(1999);
    http.expectNone('/api/v1/messages');
    await vi.advanceTimersByTimeAsync(1);

    const retryRequest = http.expectOne('/api/v1/messages');
    retryRequest.flush([{ id: 'message-1' }]);
    expect(result).toEqual([{ id: 'message-1' }]);
    expect(failure).toBeUndefined();
  });

  it('does not retry non-rate-limit errors', () => {
    let status: number | undefined;
    client.get('/api/v1/messages').subscribe({ error: (error: HttpErrorResponse) => status = error.status });
    http.expectOne('/api/v1/messages').flush({}, { status: 500, statusText: 'Server Error' });
    expect(status).toBe(500);
  });

  it('never renders provider or server error bodies', () => {
    const error = new HttpErrorResponse({ status: 502, error: 'provider-secret-body' });
    expect(errorMessage(error)).not.toContain('provider-secret-body');
  });
});
