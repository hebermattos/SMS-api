import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const bearer = auth.bearer();
  const isApi = request.url.startsWith('/api/');
  const isLogin = request.url.endsWith('/auth/token');
  const authorized = bearer && isApi && !isLogin ? request.clone({ setHeaders: { Authorization: `Bearer ${bearer}` } }) : request;
  return next(authorized).pipe(catchError((error: HttpErrorResponse) => {
    if (isApi && !isLogin && error.status === 401 && bearer === auth.bearer()) auth.logout(true);
    return throwError(() => error);
  }));
};

export function errorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) return 'Não foi possível concluir. Tente novamente.';
  if (error.status === 0) return 'Não foi possível conectar à API. Verifique sua conexão e tente novamente.';
  if (error.status === 401) return 'Credenciais inválidas ou acesso inativo.';
  if (error.status === 403) return 'Seu acesso não permite esta operação.';
  if (error.status === 404) return 'O cadastro não foi encontrado.';
  if (error.status === 429) return 'Muitas tentativas. Aguarde um minuto e tente novamente.';
  if (error.status === 409) return 'Já existe um cadastro com estes identificadores.';
  if (error.status === 400) return typeof error.error?.error === 'string' ? error.error.error : 'Confira os campos e tente novamente.';
  return 'O serviço está indisponível no momento. Tente novamente mais tarde.';
}
