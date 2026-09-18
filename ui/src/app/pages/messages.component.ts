import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Message } from '../core/models';
import { errorMessage } from '../core/api';
import { IconComponent } from '../shared/icon.component';
import { StatusComponent } from '../shared/status.component';

@Component({ selector: 'sms-messages', imports: [RouterLink, DatePipe, IconComponent, StatusComponent], template: `
  <div class="page-heading"><div><span class="eyebrow">SUAS CONVERSAS</span><h1>Mensagens<span class="heading-dot">.</span></h1><p>Envios e recebimentos, com o histórico de cada entrega.</p></div><a class="button primary" routerLink="/app/enviar"><sms-icon name="plus"/>Novo SMS</a></div>
  @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
  <section class="panel"><div class="panel-heading"><div><h2>Todas as mensagens</h2><p>Mais recentes primeiro · {{ pageSize }} por página</p></div><button class="button compact" (click)="load()" [disabled]="loading()"><sms-icon name="refresh"/>Atualizar</button></div>
    @if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Buscando mensagens…</div> }
    @else if (messages().length) { <div class="table-scroll"><table><thead><tr><th>Mensagem</th><th>Direção</th><th>Provedor</th><th>Status</th><th>Data</th><th></th></tr></thead><tbody>@for (message of messages(); track message.id) { <tr><td><strong>{{ message.direction === 2 ? message.from : message.to }}</strong><span class="message-preview">{{ message.body }}</span></td><td><span class="direction"><sms-icon [name]="message.direction === 2 ? 'down' : 'send'"/>{{ message.direction === 2 ? 'Recebida' : 'Enviada' }}</span></td><td>{{ message.provider }}</td><td><sms-status [status]="message.status"/></td><td class="nowrap muted">{{ message.createdAt | date:'dd/MM/yy HH:mm' }}</td><td><a class="icon-button" [routerLink]="['/app/mensagens', message.id]" aria-label="Ver detalhes da mensagem"><sms-icon name="arrow"/></a></td></tr> }</tbody></table></div> }
    @else { <div class="empty-state"><span class="empty-icon"><sms-icon name="message"/></span><h3>Nenhuma mensagem nesta página.</h3><p>Quando houver envios ou recebimentos, eles aparecerão aqui.</p></div> }
    <div class="pagination"><span>Página {{ page() + 1 }}</span><div><button class="button compact" [disabled]="loading() || page() === 0" (click)="load(page() - 1)">Anterior</button><button class="button compact" [disabled]="loading() || !hasNext()" (click)="load(page() + 1)">Próxima</button></div></div>
  </section>` })
export class MessagesComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient); readonly pageSize = 20;
  readonly messages = signal<Message[]>([]); readonly loading = signal(false); readonly error = signal(''); readonly page = signal(0); readonly hasNext = signal(false);
  constructor() { this.load(); }
  load(page = this.page()) {
    this.loading.set(true); this.error.set('');
    this.http.get<Message[]>('/api/v1/messages', { params: { skip: page * this.pageSize, take: this.pageSize + 1 } }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({
      next: rows => { this.messages.set(rows.slice(0, this.pageSize)); this.hasNext.set(rows.length > this.pageSize); this.page.set(page); }, error: error => this.error.set(errorMessage(error))
    });
  }
}
