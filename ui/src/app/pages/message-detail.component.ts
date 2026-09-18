import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, finalize } from 'rxjs';
import { Message, StatusHistory } from '../core/models';
import { errorMessage } from '../core/api';
import { IconComponent } from '../shared/icon.component';
import { StatusComponent, statusLabels } from '../shared/status.component';

@Component({ selector: 'sms-message-detail', imports: [RouterLink, DatePipe, IconComponent, StatusComponent], template: `
  <a class="back-link" routerLink="/app/messages">← Back to messages</a><div class="page-heading"><div><span class="eyebrow">TRACKING</span><h1>Message details<span class="heading-dot">.</span></h1><p>View the content and every status change in one place.</p></div><button class="button" (click)="load()" [disabled]="loading()"><sms-icon name="refresh"/>Refresh</button></div>
  @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
  @if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Loading message…</div> }
  @if (message(); as item) { <div class="detail-grid"><section class="panel padded"><div class="section-heading"><h2>{{ item.direction === 2 ? 'Received SMS' : 'Sent SMS' }}</h2><sms-status [status]="item.status"/></div><dl class="detail-list"><div><dt>From</dt><dd>{{ item.from || 'Provider sender' }}</dd></div><div><dt>To</dt><dd>{{ item.to }}</dd></div><div><dt>Provider</dt><dd>{{ item.provider }}</dd></div><div><dt>Created at</dt><dd>{{ item.createdAt | date:'MM/dd/yyyy HH:mm:ss' }}</dd></div></dl><h3 class="subheading">Content</h3><div class="message-body">{{ item.body || '(No text)' }}</div><dl class="detail-list identifiers"><div><dt>Message ID</dt><dd>{{ item.id }}</dd></div><div><dt>Provider ID</dt><dd>{{ item.providerMessageId || 'Not available yet' }}</dd></div></dl></section><section class="panel padded"><h2>Status history</h2><p class="muted">Updates recorded for this message.</p><ol class="timeline">@for (event of history(); track event.id) { <li><span class="timeline-mark" [class.failed]="event.status === 4"><sms-icon [name]="event.status === 4 ? 'close' : 'check'"/></span><div><strong>{{ labels[event.status] || 'Unknown' }}</strong><time>{{ event.createdAt | date:'MM/dd/yyyy · HH:mm:ss' }}</time></div></li> } @empty { <li class="muted">No updates recorded.</li> }</ol></section></div> }
` })
export class MessageDetailComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient); private readonly route = inject(ActivatedRoute);
  readonly message = signal<Message | null>(null); readonly history = signal<StatusHistory[]>([]); readonly loading = signal(false); readonly error = signal(''); readonly labels = statusLabels;
  constructor() { this.load(); }
  load() { this.loading.set(true); this.error.set(''); const id = encodeURIComponent(this.route.snapshot.paramMap.get('id') || ''); forkJoin({ message: this.http.get<Message>(`/api/v1/messages/${id}`), history: this.http.get<StatusHistory[]>(`/api/v1/messages/${id}/status-history`) }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({ next: data => { this.message.set(data.message); this.history.set(data.history); }, error: error => this.error.set(errorMessage(error)) }); }
}
