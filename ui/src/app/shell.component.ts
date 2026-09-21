import { Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { AuthService } from './core/auth.service';
import { IconComponent } from './shared/icon.component';

@Component({ selector: 'sms-shell', imports: [RouterLink, RouterLinkActive, RouterOutlet, IconComponent], template: `
  <a class="skip-link" href="#main-content">Skip to content</a>
  <div class="workspace">
    @if (menuOpen()) { <button class="sidebar-backdrop" aria-label="Close menu" (click)="menuOpen.set(false)"></button> }
    <aside class="sidebar" [class.open]="menuOpen()"><a class="brand" [routerLink]="auth.role() === 'admin' ? '/admin/tenants' : '/app'"><span class="brand-mark"><sms-icon name="message"/></span>SMS<span class="brand-light">ui</span></a>
      <div class="workspace-label"><span class="workspace-avatar">{{ auth.role() === 'admin' ? 'P' : 'C' }}</span><div><strong>{{ auth.role() === 'admin' ? 'Platform' : 'My company' }}</strong><small>{{ auth.role() === 'admin' ? 'Administration' : 'Workspace' }}</small></div></div>
      <div class="nav-label">MAIN</div>
      <nav aria-label="Main menu" (click)="menuOpen.set(false)">
        @if (auth.role() === 'admin') {
          <a routerLink="/admin/tenants" routerLinkActive="active"><sms-icon name="users"/>Companies <span class="nav-arrow">↗</span></a>
          <a routerLink="/admin/administrators" routerLinkActive="active"><sms-icon name="shield"/>Administrators</a>
          <a routerLink="/admin/reports" routerLinkActive="active"><sms-icon name="grid"/>Reports</a>
          <a routerLink="/admin/system-logs" routerLinkActive="active"><sms-icon name="logs"/>System logs</a>
        } @else {
          <a routerLink="/app" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}"><sms-icon name="grid"/>Overview</a>
          <a routerLink="/app/messages" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}"><sms-icon name="message"/>Messages</a>
          <a routerLink="/app/send" routerLinkActive="active"><sms-icon name="send"/>Send SMS</a>
          <a routerLink="/app/templates" routerLinkActive="active"><sms-icon name="message"/>Templates</a>
          <a routerLink="/app/users" routerLinkActive="active"><sms-icon name="users"/>Users</a>
          <a routerLink="/app/reports" routerLinkActive="active"><sms-icon name="grid"/>Reports</a>
          <a routerLink="/app/alerts" routerLinkActive="active"><sms-icon name="alert"/>Alerts</a>
          <a routerLink="/app/opt-outs" routerLinkActive="active"><sms-icon name="shield"/>Opt-outs</a>
          <a routerLink="/app/logs" routerLinkActive="active"><sms-icon name="logs"/>Activity logs</a>
        }
      </nav>
      <div class="sidebar-bottom"><div class="sidebar-note"><sms-icon name="shield"/><strong>{{ auth.role() === 'admin' ? 'Management with control' : 'Your workspace, your data' }}</strong><p>{{ auth.role() === 'admin' ? 'Manage companies and credentials in a separate environment.' : 'The information in this UI belongs only to your company.' }}</p></div><button class="logout" (click)="auth.logout()"><sms-icon name="logout"/>Sign out</button></div>
    </aside>
    <div class="workspace-main"><header class="topbar"><div class="topbar-left"><button class="icon-button mobile-menu" aria-label="Open menu" [attr.aria-expanded]="menuOpen()" (click)="menuOpen.set(!menuOpen())"><sms-icon name="menu"/></button><span class="breadcrumb">UI <span>/</span> {{ auth.role() === 'admin' ? 'Administration' : 'Workspace' }}</span></div><div class="account"><span class="badge">{{ auth.role() === 'admin' ? 'Administrator' : 'Tenant' }}</span><span class="account-name">{{ auth.identity() }}</span><span class="avatar">{{ auth.identity().slice(0, 1).toUpperCase() }}</span></div></header>
      <main class="page-content" id="main-content" tabindex="-1"><router-outlet/></main>
      <footer class="workspace-footer"><span>SMS UI</span><span>Communication, made clear.</span></footer>
    </div>
  </div>` })
export class ShellComponent {
  readonly auth = inject(AuthService);
  readonly menuOpen = signal(false);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(event => this.recordPage(event.urlAfterRedirects));
  }

  private recordPage(url: string): void {
    if (this.auth.role() === 'admin') return;

    const path = url.split('?')[0];
    const page =
      path === '/app' ? 'overview' :
      path === '/app/messages' || path.startsWith('/app/messages/') ? 'messages' :
      path === '/app/send' ? 'send' :
      path === '/app/templates' ? 'templates' :
      path === '/app/users' ? 'users' :
      path === '/app/reports' ? 'reports' :
      path === '/app/alerts' ? 'alerts' :
      path === '/app/opt-outs' ? 'opt-outs' :
      path === '/app/logs' ? 'logs' : null;

    if (page) this.http.post('/api/v1/activity/page', { page }).subscribe({ error: () => {} });
  }
}
