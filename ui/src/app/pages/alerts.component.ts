import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { AlertNotification, AlertRule } from '../core/models';
import { errorMessage } from '../core/api';
import { IconComponent } from '../shared/icon.component';

interface RuleForm {
  name: string; provider: string; status: number; windowMinutes: number; isActive: boolean;
}

@Component({ selector: 'sms-alerts', imports: [DatePipe, FormsModule, IconComponent], template: `
<div class="page-heading"><div><span class="eyebrow">PROACTIVE MONITORING</span><h1>Alerts<span class="heading-dot">.</span></h1><p>Create rules by status, time window, and optional provider. Matching events always trigger an alert.</p></div><button class="button compact" (click)="load()" [disabled]="loading()"><sms-icon name="refresh"/>Refresh</button></div>
@if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
@if (success()) { <div class="notice success" role="status">{{ success() }}</div> }

<section class="panel padded create-panel">
  <div class="section-heading"><div><h2>{{ editingId() ? 'Edit alert rule' : 'Create alert rule' }}</h2><p>Use an empty provider to monitor all providers.</p></div><sms-icon name="alert"/></div>
  <form (ngSubmit)="save()">
    <div class="form-grid">
      <div><label for="rule-name">Rule name</label><input id="rule-name" name="name" maxlength="120" required [(ngModel)]="form.name" placeholder="High failure volume"/></div>
      <div><label for="rule-provider">Provider <span class="optional">optional</span></label><input id="rule-provider" name="provider" maxlength="50" [(ngModel)]="form.provider" placeholder="Twilio, Bandwidth, or all"/></div>
      <div><label for="rule-status">Status</label><select id="rule-status" name="status" [(ngModel)]="form.status"><option [ngValue]="1">Queued</option><option [ngValue]="2">Sent</option><option [ngValue]="3">Delivered</option><option [ngValue]="4">Failed</option><option [ngValue]="5">Received</option></select></div>
      <div><label for="rule-window">Time window (minutes)</label><input id="rule-window" name="windowMinutes" type="number" min="1" max="1440" required [(ngModel)]="form.windowMinutes"/></div>
    </div>
    <div class="checkbox-row"><label><input name="active" type="checkbox" [(ngModel)]="form.isActive"/>Rule enabled</label></div>
    <div class="form-actions">@if (editingId()) { <button class="button" type="button" (click)="cancelEdit()">Cancel</button> }<button class="button primary" type="submit" [disabled]="saving()">{{ saving() ? 'Saving…' : editingId() ? 'Save changes' : 'Create rule' }}</button></div>
  </form>
</section>

<section class="panel create-panel">
  <div class="panel-heading"><div><h2>Rules</h2><p>Every matching status event triggers the rule. Alert data is retained for 24 hours.</p></div></div>
  <div class="table-scroll"><table><thead><tr><th>Name</th><th>Condition</th><th>State</th><th>Actions</th></tr></thead><tbody>
  @for (rule of rules(); track rule.id) { <tr><td><strong>{{ rule.name }}</strong><small class="cell-secondary">{{ rule.provider || 'All providers' }}</small></td><td>{{ statusLabel(rule.status) }} in {{ rule.windowMinutes }} min</td><td><span class="badge" [class.success]="rule.isActive">{{ rule.isActive ? 'Monitoring' : 'Disabled' }}</span></td><td><div class="button-row"><button class="button compact" (click)="edit(rule)">Edit</button><button class="button compact destructive" (click)="remove(rule)">Delete</button></div></td></tr> }
  @empty { <tr><td colspan="4" class="empty-cell">No alert rules configured.</td></tr> }
  </tbody></table></div>
</section>

<section class="panel">
  <div class="panel-heading"><div><h2>Triggered alerts</h2><p>Alerts are stored in this workspace and never expose another tenant's data.</p></div><button class="text-button" (click)="markAllRead()" [disabled]="!hasUnread()">Mark all as read</button></div>
  <div class="log-list">@for (alert of alerts(); track alert.id) { <article class="log-entry"><div class="log-meta"><span class="badge" [class.danger]="alert.status === 4" [class.success]="alert.isRead">{{ alert.isRead ? 'Read' : 'New' }}</span><time>{{ alert.createdAt | date:'MM/dd/yyyy HH:mm:ss' }}</time></div><p><strong>{{ alert.ruleName }}</strong>: {{ alert.matchCount }} messages reached {{ statusLabel(alert.status).toLowerCase() }} status within {{ alert.windowMinutes }} minutes for {{ alert.provider || 'all providers' }}.</p>@if (!alert.isRead) { <button class="text-button" (click)="markRead(alert)">Mark as read</button> }</article> }
  @empty { <div class="empty-state"><span class="empty-icon"><sms-icon name="alert"/></span><h3>No alerts triggered.</h3><p>Active rules will create alerts when their conditions are met.</p></div> }</div>
</section>
` })
export class AlertsComponent {
  private readonly http = inject(HttpClient); private readonly destroyRef = inject(DestroyRef);
  readonly rules = signal<AlertRule[]>([]); readonly alerts = signal<AlertNotification[]>([]);
  readonly loading = signal(false); readonly saving = signal(false); readonly editingId = signal<string|null>(null);
  readonly error = signal(''); readonly success = signal('');
  form: RuleForm = this.emptyForm();

  constructor() { this.load(); }

  load() {
    this.loading.set(true); this.error.set('');
    forkJoin({ rules: this.http.get<AlertRule[]>('/api/v1/alerts/rules'), alerts: this.http.get<AlertNotification[]>('/api/v1/alerts?take=100') })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false)))
      .subscribe({ next: result => { this.rules.set(result.rules); this.alerts.set(result.alerts); }, error: e => this.error.set(errorMessage(e)) });
  }

  save() {
    this.saving.set(true); this.error.set(''); this.success.set('');
    const payload = { ...this.form, provider: this.form.provider.trim() || null };
    const request = this.editingId() ? this.http.put('/api/v1/alerts/rules/' + this.editingId(), payload) : this.http.post('/api/v1/alerts/rules', payload);
    request.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.success.set(this.editingId() ? 'Alert rule updated.' : 'Alert rule created.'); this.cancelEdit(); this.load(); },
      error: e => this.error.set(errorMessage(e))
    });
  }

  edit(rule: AlertRule) {
    this.editingId.set(rule.id);
    this.form = { name: rule.name, provider: rule.provider || '', status: rule.status, windowMinutes: rule.windowMinutes, isActive: rule.isActive };
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
  cancelEdit() { this.editingId.set(null); this.form = this.emptyForm(); }
  remove(rule: AlertRule) {
    if (!confirm('Delete alert rule "' + rule.name + '"? Existing alerts will be preserved.')) return;
    this.http.delete('/api/v1/alerts/rules/' + rule.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.load(), error: e => this.error.set(errorMessage(e)) });
  }
  markRead(alert: AlertNotification) { this.http.post('/api/v1/alerts/' + alert.id + '/read', {}).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.load(), error: e => this.error.set(errorMessage(e)) }); }
  markAllRead() { this.http.post('/api/v1/alerts/read-all', {}).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.load(), error: e => this.error.set(errorMessage(e)) }); }
  hasUnread() { return this.alerts().some(x => !x.isRead); }
  statusLabel(status: number) { return ['', 'Queued', 'Sent', 'Delivered', 'Failed', 'Received'][status] || 'Unknown'; }
  private emptyForm(): RuleForm { return { name: '', provider: '', status: 4, windowMinutes: 15, isActive: true }; }
}
