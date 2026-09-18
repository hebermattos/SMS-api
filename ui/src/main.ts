import { Component, LOCALE_ID, provideZonelessChangeDetection } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, RouterOutlet, Routes, withInMemoryScrolling } from '@angular/router';
import { registerLocaleData } from '@angular/common';
import localePt from '@angular/common/locales/pt';
import { authInterceptor } from './app/core/api';
import { roleGuard } from './app/core/auth.service';
import { ShellComponent } from './app/shell.component';

registerLocaleData(localePt);
const routes: Routes = [
  { path: 'entrar', loadComponent: () => import('./app/pages/login.component').then(m => m.LoginComponent) },
  {
    path: 'admin', component: ShellComponent, canActivate: [roleGuard], data: { role: 'admin' }, children: [
      { path: 'tenants', loadComponent: () => import('./app/pages/tenants.component').then(m => m.TenantsComponent) },
      { path: 'tenants/:id', loadComponent: () => import('./app/pages/tenant-detail.component').then(m => m.TenantDetailComponent) },
      { path: '', pathMatch: 'full', redirectTo: 'tenants' }
    ]
  },
  {
    path: 'app', component: ShellComponent, canActivate: [roleGuard], data: { role: 'tenant' }, children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./app/pages/overview.component').then(m => m.OverviewComponent) },
      { path: 'enviar', loadComponent: () => import('./app/pages/send.component').then(m => m.SendComponent) },
      { path: 'mensagens', loadComponent: () => import('./app/pages/messages.component').then(m => m.MessagesComponent) },
      { path: 'mensagens/:id', loadComponent: () => import('./app/pages/message-detail.component').then(m => m.MessageDetailComponent) },
      { path: 'logs', loadComponent: () => import('./app/pages/logs.component').then(m => m.LogsComponent) }
    ]
  },
  { path: '**', redirectTo: 'entrar' }
];

@Component({ selector: 'sms-root', imports: [RouterOutlet], template: '<router-outlet/>' })
class AppComponent {}

bootstrapApplication(AppComponent, { providers: [
  provideZonelessChangeDetection(), { provide: LOCALE_ID, useValue: 'pt-BR' },
  provideHttpClient(withInterceptors([authInterceptor])),
  provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'enabled' }))
]}).catch(() => {
  const root = document.querySelector('sms-root');
  if (root) root.textContent = 'Não foi possível abrir o painel. Recarregue a página.';
});
