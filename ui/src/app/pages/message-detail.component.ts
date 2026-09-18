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
  <a class="back-link" routerLink="/app/mensagens">← Voltar para mensagens</a><div class="page-heading"><div><span class="eyebrow">ACOMPANHAMENTO</span><h1>Detalhes da mensagem<span class="heading-dot">.</span></h1><p>O conteúdo e cada mudança de status em um só lugar.</p></div><button class="button" (click)="load()" [disabled]="loading()"><sms-icon name="refresh"/>Atualizar</button></div>
  @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
  @if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Carregando mensagem…</div> }
  @if (message(); as item) { <div class="detail-grid"><section class="panel padded"><div class="section-heading"><h2>{{ item.direction === 2 ? 'SMS recebido' : 'SMS enviado' }}</h2><sms-status [status]="item.status"/></div><dl class="detail-list"><div><dt>De</dt><dd>{{ item.from || 'Remetente do provedor' }}</dd></div><div><dt>Para</dt><dd>{{ item.to }}</dd></div><div><dt>Provedor</dt><dd>{{ item.provider }}</dd></div><div><dt>Criada em</dt><dd>{{ item.createdAt | date:'dd/MM/yyyy HH:mm:ss' }}</dd></div></dl><h3 class="subheading">Conteúdo</h3><div class="message-body">{{ item.body || '(Sem texto)' }}</div><dl class="detail-list identifiers"><div><dt>ID da mensagem</dt><dd>{{ item.id }}</dd></div><div><dt>ID no provedor</dt><dd>{{ item.providerMessageId || 'Ainda não disponível' }}</dd></div></dl></section><section class="panel padded"><h2>Histórico de status</h2><p class="muted">Atualizações registradas para esta mensagem.</p><ol class="timeline">@for (event of history(); track event.id) { <li><span class="timeline-mark" [class.failed]="event.status === 4"><sms-icon [name]="event.status === 4 ? 'close' : 'check'"/></span><div><strong>{{ labels[event.status] || 'Desconhecido' }}</strong><time>{{ event.createdAt | date:'dd/MM/yyyy · HH:mm:ss' }}</time></div></li> } @empty { <li class="muted">Nenhuma atualização registrada.</li> }</ol></section></div> }
` })
export class MessageDetailComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient); private readonly route = inject(ActivatedRoute);
  readonly message = signal<Message | null>(null); readonly history = signal<StatusHistory[]>([]); readonly loading = signal(false); readonly error = signal(''); readonly labels = statusLabels;
  constructor() { this.load(); }
  load() { this.loading.set(true); this.error.set(''); const id = encodeURIComponent(this.route.snapshot.paramMap.get('id') || ''); forkJoin({ message: this.http.get<Message>(`/api/v1/messages/${id}`), history: this.http.get<StatusHistory[]>(`/api/v1/messages/${id}/status-history`) }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({ next: data => { this.message.set(data.message); this.history.set(data.history); }, error: error => this.error.set(errorMessage(error)) }); }
}
