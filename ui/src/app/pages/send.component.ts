import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Overview, SendResult } from '../core/models';
import { errorMessage } from '../core/api';
import { IconComponent } from '../shared/icon.component';

@Component({ selector: 'sms-send', imports: [FormsModule, RouterLink, IconComponent], template: `
  <div class="page-heading"><div><span class="eyebrow">NEW CONVERSATION</span><h1>Send SMS<span class="heading-dot">.</span></h1><p>Write your message. We'll take care of delivering it to the recipient.</p></div></div>
  @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
  @if (result(); as sent) { <div class="notice success" role="status"><sms-icon name="check"/><div><strong>{{ sent.status === 'Scheduled' ? 'Message scheduled.' : 'Request sent.' }}</strong><p>{{ sent.status === 'Scheduled' ? 'It will be queued at the selected tenant-local time.' : 'The provider will update the delivery status.' }}</p><a [routerLink]="['/app/messages', sent.id]">Track message →</a></div></div> }
  @if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Loading providers…</div> }
  @else if (overview(); as data) { @if (!data.providers.length) { <div class="empty-state panel"><sms-icon name="plug"/><h3>A provider must be configured.</h3><p>Ask the administrator to configure a sender number and an active provider for your company.</p></div> } @else {
    <div class="compose-grid"><section class="panel padded"><form #form="ngForm" (ngSubmit)="send()"><div class="section-heading"><h2>Sending details</h2><span class="badge">SMS</span></div><label for="destination">Destination number</label><input id="destination" name="to" [(ngModel)]="to" type="tel" required pattern="\\+[1-9][0-9]{6,14}" maxlength="16" placeholder="+15551234567" autocomplete="tel"/><p class="field-help">Include +, country code, and area code.</p><label for="send-provider">Provider</label><select id="send-provider" name="provider" [(ngModel)]="provider" required>@for (option of data.providers; track option.name) { <option [value]="option.name">{{ option.name }}{{ option.isDefault ? ' · Default' : '' }} — {{ option.fromNumber }}</option> }</select><label for="body">Your message</label><textarea id="body" name="body" [(ngModel)]="body" required maxlength="1600" rows="7" placeholder="What would you like to say?"></textarea><div class="button-row"><button class="button compact" type="button" (click)="improve()" [disabled]="aiBusy() || !body.trim()">Improve with AI</button><button class="button compact" type="button" (click)="validateMessage()" [disabled]="aiBusy() || !body.trim()">Validate</button></div>@if (aiResult()) { <div class="notice" role="status">{{ aiResult() }}</div> }<div class="field-counter"><span>Long messages may use more than one segment.</span><strong>{{ body.length }}/1600</strong></div><label for="scheduled-at">Schedule for later <span class="muted">(optional)</span></label><input id="scheduled-at" name="scheduledAt" [(ngModel)]="scheduledAt" type="datetime-local"/><p class="field-help">Time zone: {{ data.timeZoneId }}. Leave empty to send now. You can schedule up to one year ahead.</p><div class="form-actions"><a class="button" routerLink="/app/messages">Cancel</a><button class="button primary" type="submit" [disabled]="form.invalid || busy() || !body.trim()"><sms-icon name="send"/>{{ busy() ? 'Submitting…' : (scheduledAt ? 'Schedule message' : 'Send message') }}</button></div></form></section><aside><section class="phone-preview"><div class="phone-speaker"></div><span class="phone-title">Message preview</span><div class="sms-bubble">{{ body || 'Your message will appear here as you type.' }}</div><small>Illustrative preview</small></section><p class="compose-note"><sms-icon name="shield"/>Messages use the number configured for your company.</p></aside></div>
  } } @else if (!loading()) { <button class="button" (click)="load()">Try again</button> }
` })
export class SendComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient); readonly overview = signal<Overview | null>(null);
  readonly result = signal<SendResult | null>(null); readonly aiBusy = signal(false); readonly aiResult = signal(''); readonly loading = signal(false); readonly busy = signal(false); readonly error = signal('');
  to = ''; body = ''; provider = ''; scheduledAt = '';
  constructor() { this.load(); }
  load() { this.loading.set(true); this.error.set(''); this.http.get<Overview>('/api/v1/overview').pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({ next: data => { this.overview.set(data); this.provider = data.providers.find(x => x.isDefault)?.name || data.providers[0]?.name || ''; }, error: error => this.error.set(errorMessage(error)) }); }
  improve() { this.runAi('improve'); }
  validateMessage() { this.runAi('validate'); }
  private runAi(action: 'improve' | 'validate') {
    if (this.aiBusy() || !this.body.trim()) return;
    this.aiBusy.set(true); this.aiResult.set('');
    this.http.post<{ message: string; issues: string[]; isValid: boolean }>(`/api/v1/message-assistant/${action}`, { message: this.body })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.aiBusy.set(false)))
      .subscribe({ next: result => { if (action === 'improve') { if (confirm('Apply the AI suggestion?\n\n' + result.message)) this.body = result.message; } else this.aiResult.set(result.isValid ? 'No issues found.' : result.issues.join(' · ')); }, error: error => this.error.set(errorMessage(error)) });
  }
  send() { if (this.busy() || !this.body.trim() || !/^\+[1-9][0-9]{6,14}$/.test(this.to)) return; this.busy.set(true); this.error.set(''); this.result.set(null); const request = { to: this.to, body: this.body, provider: this.provider, scheduledAt: this.scheduledAt || null }; this.http.post<SendResult>('/api/v1/messages', request).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({ next: result => { this.result.set(result); this.body = ''; this.to = ''; this.scheduledAt = ''; }, error: error => this.error.set(errorMessage(error) + ' Check your messages before trying again to avoid duplicates.') }); }
}
