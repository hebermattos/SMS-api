import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { errorMessage } from '../core/api';
import { IconComponent } from '../shared/icon.component';

@Component({ selector: 'sms-login', imports: [FormsModule, IconComponent], template: `
  <main class="login-page">
    <section class="login-story" aria-label="SMS Console">
      <a class="brand" href="/"><span class="brand-mark"><sms-icon name="message"/></span>SMS<span class="brand-light">console</span></a>
      <div class="login-story-content"><span class="eyebrow light">CONVERSAS QUE CONECTAM</span>
        <h1>Every message.<br>One connection.</h1><p>One place to manage sends, track deliveries, and keep everything under control.</p>
        <div class="message-illustration" aria-hidden="true"><span class="orbit orbit-one"></span><span class="orbit orbit-two"></span><div class="illustration-card"><span class="illustration-icon"><sms-icon name="message"/></span><div><span class="illustration-line"></span><span class="illustration-line short"></span></div><span class="illustration-check"><sms-icon name="check"/></span></div></div>
      </div><div class="login-story-footer"><sms-icon name="shield"/> A separate workspace for every company.</div>
    </section>
    <section class="login-form-panel"><div class="login-form-wrap">
      <span class="eyebrow">WELCOME TO SMS CONSOLE</span><h2>Let's get started.</h2><p class="muted">Sign in to your workspace.</p>
      <div class="segmented" aria-label="Access type"><button type="button" [class.selected]="!admin()" (click)="changeMode(false)" [disabled]="busy()" [attr.aria-pressed]="!admin()">My company</button><button type="button" [class.selected]="admin()" (click)="changeMode(true)" [disabled]="busy()" [attr.aria-pressed]="admin()">Platform</button></div>
      @if (auth.expired()) { <div class="notice" role="status">Your session has ended. Sign in again to continue.</div> }
      @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
      <form #form="ngForm" (ngSubmit)="submit()">
        @if (!admin()) { <label for="client-id">Client identifier</label><input id="client-id" name="clientId" [(ngModel)]="clientId" autocomplete="username" required maxlength="100" placeholder="Your client ID"/> }
        <label for="credential">{{ admin() ? 'Administration key' : 'Client secret' }}</label><input id="credential" name="credential" type="password" [(ngModel)]="credential" autocomplete="current-password" required maxlength="1024" [placeholder]="admin() ? 'Enter your access key' : 'Enter your client secret'"/>
        <button class="button primary full" type="submit" [disabled]="form.invalid || busy()">{{ busy() ? 'Signing in…' : 'Sign in to console' }}<sms-icon name="arrow"/></button>
      </form>
      <p class="login-help"><sms-icon name="shield"/>{{ admin() ? 'Exclusive access to platform administration.' : 'Need access? Request credentials from your administrator.' }}</p>
      <p class="fine-print">For security, you must sign in again after reloading or closing this page.</p>
    </div></section>
  </main>` })
export class LoginComponent {
  private readonly destroyRef = inject(DestroyRef);
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly admin = signal(false); readonly busy = signal(false); readonly error = signal('');
  clientId = ''; credential = '';
  changeMode(admin: boolean) { this.admin.set(admin); this.credential = ''; this.error.set(''); }
  submit() {
    if (this.busy() || !this.credential || (!this.admin() && !this.clientId.trim())) return;
    this.busy.set(true); this.error.set('');
    const request = this.admin() ? this.auth.loginAdmin(this.credential) : this.auth.loginTenant(this.clientId.trim(), this.credential);
    request.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => { this.busy.set(false); this.credential = ''; })).subscribe({
      next: () => void this.router.navigateByUrl(this.admin() ? '/admin/tenants' : '/app'),
      error: error => this.error.set(errorMessage(error))
    });
  }
}
