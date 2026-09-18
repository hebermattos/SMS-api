import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { IconComponent } from './shared/icon.component';

@Component({ selector: 'sms-shell', imports: [RouterLink, RouterLinkActive, RouterOutlet, IconComponent], template: `
  <a class="skip-link" href="#main-content">Pular para o conteúdo</a>
  <div class="workspace">
    @if (menuOpen()) { <button class="sidebar-backdrop" aria-label="Fechar menu" (click)="menuOpen.set(false)"></button> }
    <aside class="sidebar" [class.open]="menuOpen()"><a class="brand" [routerLink]="auth.role() === 'admin' ? '/admin/tenants' : '/app'"><span class="brand-mark"><sms-icon name="message"/></span>SMS<span class="brand-light">console</span></a>
      <div class="workspace-label"><span class="workspace-avatar">{{ auth.role() === 'admin' ? 'P' : 'E' }}</span><div><strong>{{ auth.role() === 'admin' ? 'Plataforma' : 'Minha empresa' }}</strong><small>{{ auth.role() === 'admin' ? 'Administração' : 'Espaço de trabalho' }}</small></div></div>
      <div class="nav-label">PRINCIPAL</div>
      <nav aria-label="Menu principal" (click)="menuOpen.set(false)">
        @if (auth.role() === 'admin') {
          <a routerLink="/admin/tenants" routerLinkActive="active"><sms-icon name="users"/>Empresas <span class="nav-arrow">↗</span></a>
        } @else {
          <a routerLink="/app" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}"><sms-icon name="grid"/>Visão geral</a>
          <a routerLink="/app/mensagens" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}"><sms-icon name="message"/>Mensagens</a>
          <a routerLink="/app/enviar" routerLinkActive="active"><sms-icon name="send"/>Enviar SMS</a>
          <a routerLink="/app/logs" routerLinkActive="active"><sms-icon name="logs"/>Logs de atividade</a>
        }
      </nav>
      <div class="sidebar-bottom"><div class="sidebar-note"><sms-icon name="shield"/><strong>{{ auth.role() === 'admin' ? 'Gestão com controle' : 'Seu espaço, seus dados' }}</strong><p>{{ auth.role() === 'admin' ? 'Gerencie empresas e credenciais em um ambiente separado.' : 'As informações deste painel pertencem apenas à sua empresa.' }}</p></div><button class="logout" (click)="auth.logout()"><sms-icon name="logout"/>Sair da conta</button></div>
    </aside>
    <div class="workspace-main"><header class="topbar"><div class="topbar-left"><button class="icon-button mobile-menu" aria-label="Abrir menu" [attr.aria-expanded]="menuOpen()" (click)="menuOpen.set(!menuOpen())"><sms-icon name="menu"/></button><span class="breadcrumb">Console <span>/</span> {{ auth.role() === 'admin' ? 'Administração' : 'Workspace' }}</span></div><div class="account"><span class="badge">{{ auth.role() === 'admin' ? 'Administrador' : 'Tenant' }}</span><span class="account-name">{{ auth.identity() }}</span><span class="avatar">{{ auth.identity().slice(0, 1).toUpperCase() }}</span></div></header>
      <main class="page-content" id="main-content" tabindex="-1"><router-outlet/></main>
      <footer class="workspace-footer"><span>SMS Console</span><span>Comunicação, com clareza.</span></footer>
    </div>
  </div>` })
export class ShellComponent { readonly auth = inject(AuthService); readonly menuOpen = signal(false); }
