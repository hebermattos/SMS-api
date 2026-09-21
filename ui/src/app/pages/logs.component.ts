import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { LogEntry } from '../core/models';
import { errorMessage } from '../core/api';
import { IconComponent } from '../shared/icon.component';

@Component({ selector: 'sms-logs', imports: [DatePipe, FormsModule, IconComponent], template: `
  <div class="page-heading"><div><span class="eyebrow">OPERATIONAL TRANSPARENCY</span><h1>Activity logs<span class="heading-dot">.</span></h1><p>Review your company's operational events.</p></div></div>
  <form class="filter-bar panel" (ngSubmit)="load(0)"><div><label for="from-date">From</label><input id="from-date" name="from" type="datetime-local" [(ngModel)]="from"/></div><div><label for="to-date">To</label><input id="to-date" name="to" type="datetime-local" [(ngModel)]="to"/></div><button class="button primary" [disabled]="loading()" type="submit">Apply date range</button><button class="text-button" type="button" [disabled]="loading()" (click)="clear()">Clear</button></form>
  @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
  <section class="panel"><div class="panel-heading"><div><h2>Event log</h2><p>Times are displayed in your browser's time zone.</p></div><sms-icon name="logs"/></div>@if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Loading events…</div> } @else { <div class="log-list">@for (log of logs(); track log.id) { <article class="log-entry"><div class="log-meta"><span class="badge" [class.danger]="log.severity === 'Error' || log.severity === 'Critical'" [class.warning]="log.severity === 'Warning'">{{ severityLabel(log.severity) }}</span><time>{{ log.timestamp | date:'MM/dd/yyyy HH:mm:ss' }}</time></div><p>{{ log.message }}</p></article> } @empty { <div class="empty-state"><span class="empty-icon"><sms-icon name="logs"/></span><h3>No events in this period.</h3><p>Change the date range or return later to view new activity.</p></div> }</div> }<div class="pagination"><span>Page {{ page() + 1 }}</span><div><button class="button compact" (click)="load(page() - 1)" [disabled]="loading() || page() === 0">Previous</button><button class="button compact" (click)="load(page() + 1)" [disabled]="loading() || !hasNext()">Next</button></div></div></section>
` })
export class LogsComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient); readonly logs = signal<LogEntry[]>([]); readonly loading = signal(false); readonly error = signal(''); readonly page = signal(0); readonly hasNext = signal(false); private readonly cursors: ({ timestamp: string; id: number } | null)[] = [null];
  from = ''; to = ''; private appliedFrom = ''; private appliedTo = '';
  constructor() { this.load(); }
  clear() { this.from = ''; this.to = ''; this.load(0); }
  severityLabel(severity: string) { return severity === 'Warning' ? 'Needs attention' : severity === 'Error' || severity === 'Critical' ? 'Failed' : 'Completed'; }
  load(page = 0) {
    const from = page === 0 ? this.from : this.appliedFrom; const to = page === 0 ? this.to : this.appliedTo; if (page === 0) this.cursors.splice(1);
    if ((from && !Number.isFinite(Date.parse(from))) || (to && !Number.isFinite(Date.parse(to))) || (from && to && new Date(from) >= new Date(to))) { this.error.set('The start of the date range must be before the end.'); return; }
    let params = new HttpParams().set('take', 21); const cursor = this.cursors[page]; if (cursor) params = params.set('cursorTimestamp', cursor.timestamp).set('cursorId', cursor.id);
    if (from) params = params.set('from', new Date(from).toISOString()); if (to) params = params.set('to', new Date(to).toISOString());
    this.loading.set(true); this.error.set(''); this.http.get<LogEntry[]>('/api/v1/logs', { params }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({ next: rows => { this.logs.set(rows.slice(0, 20)); this.hasNext.set(rows.length > 20); this.page.set(page); if (rows.length > 20) { const last = rows[19]; this.cursors[page + 1] = { timestamp: last.timestamp, id: last.id }; } else this.cursors.splice(page + 1); this.appliedFrom = from; this.appliedTo = to; }, error: error => this.error.set(errorMessage(error)) });
  }
}
