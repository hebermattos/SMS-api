import { Component, LOCALE_ID, provideZonelessChangeDetection } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, RouterOutlet, Routes, withInMemoryScrolling } from '@angular/router';
import { authInterceptor } from './app/core/api';
import { roleGuard } from './app/core/auth.service';
import { ShellComponent } from './app/shell.component';

const routes: Routes = [
  { path: 'login', loadComponent: () => import('./app/pages/login.component').then(m => m.LoginComponent) },
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
      { path: 'send', loadComponent: () => import('./app/pages/send.component').then(m => m.SendComponent) },
      { path: 'messages', loadComponent: () => import('./app/pages/messages.component').then(m => m.MessagesComponent) },
      { path: 'messages/:id', loadComponent: () => import('./app/pages/message-detail.component').then(m => m.MessageDetailComponent) },
      { path: 'logs', loadComponent: () => import('./app/pages/logs.component').then(m => m.LogsComponent) }
    ]
  },
  { path: '**', redirectTo: 'login' }
];

@Component({ selector: 'sms-root', imports: [RouterOutlet], template: '<router-outlet/>' })
class AppComponent {}

bootstrapApplication(AppComponent, { providers: [
  provideZonelessChangeDetection(), { provide: LOCALE_ID, useValue: 'en-US' },
  provideHttpClient(withInterceptors([authInterceptor])),
  provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'enabled' }))
]}).catch(() => {
  const root = document.querySelector('sms-root');
  if (root) root.textContent = 'The console could not be opened. Reload the page.';
});
