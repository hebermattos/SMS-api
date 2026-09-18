import { DatePipe } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { errorMessage } from '../core/api';
import { LogEntry } from '../core/models';
import { IconComponent } from '../shared/icon.component';

@Component({ selector: 'sms-system-logs', imports: [DatePipe, FormsModule, IconComponent], template: `
  <div class="page-heading"><div><span class="eyebrow">PLATFORM OBSERVABILITY</span><h1>System logs<span class="heading-dot">.</span></h1><p>Review errors that require attention from platform administrators.</p></div></div>
  <form class="filter-bar panel" (ngSubmit)="load(0)"><div><label for="from-date">From</label><input id="from-date" name="from" type="datetime-local" [(ngModel)]="from"/></div><div><label for="to-date">To</label><input id="to-date" name="to" type="datetime-local" [(ngModel)]="to"/></div><button class="button primary" [disabled]="loading()" type="submit">Apply date range</button><button class="text-button" type="button" [disabled]="loading()" (click)="clear()">Clear</button></form>
  @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
  <section class="panel"><div class="panel-heading"><div><h2>Error log</h2><p>Only Error and Critical events are stored. Times use your browser's time zone.</p></div><sms-icon name="logs"/></div>@if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Loading errors…</div> } @else { <div class="log-list">@for (log of logs(); track log.id) { <article class="log-entry"><div class="log-meta"><span class="badge danger">{{ log.severity }}</span><time>{{ log.timestamp | date:'MM/dd/yyyy HH:mm:ss' }}</time></div><p>{{ log.message }}</p><details><summary>Technical details</summary><dl class="detail-list"><div><dt>Category</dt><dd>{{ log.category }}</dd></div><div><dt>Trace ID</dt><dd>{{ log.traceId || 'Not available' }}</dd></div><div><dt>Span ID</dt><dd>{{ log.spanId || 'Not available' }}</dd></div></dl></details></article> } @empty { <div class="empty-state"><span class="empty-icon"><sms-icon name="shield"/></span><h3>No system errors in this period.</h3><p>The platform has not recorded any Error or Critical events for this date range.</p></div> }</div> }<div class="pagination"><span>Page {{ page() + 1 }}</span><div><button class="button compact" (click)="load(page() - 1)" [disabled]="loading() || page() === 0">Previous</button><button class="button compact" (click)="load(page() + 1)" [disabled]="loading() || !hasNext()">Next</button></div></div></section>
` })
export class SystemLogsComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient);
  readonly logs = signal<LogEntry[]>([]); readonly loading = signal(false); readonly error = signal(''); readonly page = signal(0); readonly hasNext = signal(false);
  from = ''; to = ''; private appliedFrom = ''; private appliedTo = '';
  constructor() { this.load(); }
  clear() { this.from = ''; this.to = ''; this.load(0); }
  load(page = 0) {
    const from = page === 0 ? this.from : this.appliedFrom; const to = page === 0 ? this.to : this.appliedTo;
    if ((from && !Number.isFinite(Date.parse(from))) || (to && !Number.isFinite(Date.parse(to))) || (from && to && new Date(from) >= new Date(to))) { this.error.set('The start of the date range must be before the end.'); return; }
    let params = new HttpParams().set('skip', page * 20).set('take', 21);
    if (from) params = params.set('from', new Date(from).toISOString()); if (to) params = params.set('to', new Date(to).toISOString());
    this.loading.set(true); this.error.set('');
    this.http.get<LogEntry[]>('/api/v1/admin/system-logs', { params }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({ next: rows => { this.logs.set(rows.slice(0, 20)); this.hasNext.set(rows.length > 20); this.page.set(page); this.appliedFrom = from; this.appliedTo = to; }, error: error => this.error.set(errorMessage(error)) });
  }
}
