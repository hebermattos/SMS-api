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
  <div class="page-heading"><div><span class="eyebrow">TRANSPARÊNCIA NA OPERAÇÃO</span><h1>Logs de atividade<span class="heading-dot">.</span></h1><p>Consulte os eventos operacionais da sua empresa.</p></div></div>
  <form class="filter-bar panel" (ngSubmit)="load(0)"><div><label for="from-date">A partir de</label><input id="from-date" name="from" type="datetime-local" [(ngModel)]="from"/></div><div><label for="to-date">Até</label><input id="to-date" name="to" type="datetime-local" [(ngModel)]="to"/></div><button class="button primary" [disabled]="loading()" type="submit">Aplicar período</button><button class="text-button" type="button" [disabled]="loading()" (click)="clear()">Limpar</button></form>
  @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
  <section class="panel"><div class="panel-heading"><div><h2>Registro de eventos</h2><p>Horários exibidos no fuso do seu navegador.</p></div><sms-icon name="logs"/></div>@if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Buscando eventos…</div> } @else { <div class="log-list">@for (log of logs(); track log.id) { <article class="log-entry"><div class="log-meta"><span class="badge" [class.danger]="log.severity === 'Error' || log.severity === 'Critical'" [class.warning]="log.severity === 'Warning'">{{ log.severity }}</span><time>{{ log.timestamp | date:'dd/MM/yyyy HH:mm:ss' }}</time></div><p>{{ log.message }}</p><details><summary>Detalhes técnicos</summary><dl class="detail-list"><div><dt>Categoria</dt><dd>{{ log.category }}</dd></div><div><dt>Trace ID</dt><dd>{{ log.traceId || 'Não disponível' }}</dd></div><div><dt>Span ID</dt><dd>{{ log.spanId || 'Não disponível' }}</dd></div></dl></details></article> } @empty { <div class="empty-state"><span class="empty-icon"><sms-icon name="logs"/></span><h3>Nenhum evento neste período.</h3><p>Altere o intervalo ou volte mais tarde para acompanhar novas atividades.</p></div> }</div> }<div class="pagination"><span>Página {{ page() + 1 }}</span><div><button class="button compact" (click)="load(page() - 1)" [disabled]="loading() || page() === 0">Anterior</button><button class="button compact" (click)="load(page() + 1)" [disabled]="loading() || !hasNext()">Próxima</button></div></div></section>
` })
export class LogsComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient); readonly logs = signal<LogEntry[]>([]); readonly loading = signal(false); readonly error = signal(''); readonly page = signal(0); readonly hasNext = signal(false);
  from = ''; to = ''; private appliedFrom = ''; private appliedTo = '';
  constructor() { this.load(); }
  clear() { this.from = ''; this.to = ''; this.load(0); }
  load(page = 0) {
    const from = page === 0 ? this.from : this.appliedFrom; const to = page === 0 ? this.to : this.appliedTo;
    if ((from && !Number.isFinite(Date.parse(from))) || (to && !Number.isFinite(Date.parse(to))) || (from && to && new Date(from) >= new Date(to))) { this.error.set('O início do período deve ser anterior ao fim.'); return; }
    let params = new HttpParams().set('skip', page * 20).set('take', 21);
    if (from) params = params.set('from', new Date(from).toISOString()); if (to) params = params.set('to', new Date(to).toISOString());
    this.loading.set(true); this.error.set(''); this.http.get<LogEntry[]>('/api/v1/logs', { params }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({ next: rows => { this.logs.set(rows.slice(0, 20)); this.hasNext.set(rows.length > 20); this.page.set(page); this.appliedFrom = from; this.appliedTo = to; }, error: error => this.error.set(errorMessage(error)) });
  }
}
