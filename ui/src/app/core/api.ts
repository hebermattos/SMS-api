import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, mergeMap, retry, throwError, timer } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const bearer = auth.bearer();
  const isApi = request.url.startsWith('/api/');
  const isLogin = request.url.endsWith('/auth/token');
  const authorized = bearer && isApi && !isLogin ? request.clone({ setHeaders: { Authorization: `Bearer ${bearer}` } }) : request;
  return next(authorized).pipe(
    retry({
      count: 1,
      delay: (error: HttpErrorResponse) => {
        if (isApi && !isLogin && error.status === 429) {
          const retryAfter = Number(error.headers.get('Retry-After'));
          const delayMs = Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter * 1000 : 60_000;
          return timer(delayMs);
        }
        return throwError(() => error);
      }
    }),
    catchError((error: HttpErrorResponse) => {
    if (isApi && !isLogin && error.status === 401 && bearer === auth.bearer()) auth.logout(true);
      return throwError(() => error);
    })
  );
};

export function errorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) return 'The request could not be completed. Please try again.';
  if (error.status === 0) return 'The API could not be reached. Check your connection and try again.';
  if (error.status === 401) return 'Invalid credentials or inactive access.';
  if (error.status === 403) return 'Your account is not allowed to perform this operation.';
  if (error.status === 404) return 'The requested record was not found.';
  if (error.status === 409) return 'A record with these identifiers already exists.';
  if (error.status === 400) return typeof error.error?.error === 'string' ? error.error.error : 'Check the fields and try again.';
  return 'The service is currently unavailable. Please try again later.';
}
