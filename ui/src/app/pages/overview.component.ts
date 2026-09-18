import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { forkJoin, finalize } from 'rxjs';
import { Message, Overview } from '../core/models';
import { errorMessage } from '../core/api';
import { IconComponent } from '../shared/icon.component';
import { StatusComponent } from '../shared/status.component';

@Component({ selector: 'sms-overview', imports: [RouterLink, DatePipe, DecimalPipe, IconComponent, StatusComponent], template: `
  <div class="page-heading"><div><span class="eyebrow">SEU ESPAÇO DE TRABALHO</span><h1>Visão geral<span class="heading-dot">.</span></h1><p>Acompanhe suas conversas, do envio à entrega.</p></div><a class="button primary" routerLink="/app/enviar"><sms-icon name="plus"/>Novo SMS</a></div>
  @if (error()) { <div class="notice error" role="alert">{{ error() }} <button class="text-button" (click)="load()">Tentar novamente</button></div> }
  @if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Carregando sua visão geral…</div> }
  @if (overview(); as data) {
    <div class="welcome-strip"><div><span class="live-dot"></span><strong>{{ data.name }}</strong><span class="muted">Resumo de todo o período</span></div><button class="text-button" (click)="load()" [disabled]="loading()"><sms-icon name="refresh"/>Atualizar</button></div>
    <section class="stats-grid" aria-label="Indicadores de mensagens"><article class="stat-card"><div class="stat-top">SMS enviados<span class="stat-icon"><sms-icon name="send"/></span></div><strong>{{ data.outbound | number }}</strong><small>Total de solicitações de envio</small></article><article class="stat-card"><div class="stat-top">Entregues<span class="stat-icon green"><sms-icon name="check"/></span></div><strong>{{ data.delivered | number }}</strong><small>Entrega confirmada pelo provedor</small></article><article class="stat-card"><div class="stat-top">Recebidas<span class="stat-icon blue"><sms-icon name="down"/></span></div><strong>{{ data.inbound | number }}</strong><small>Mensagens recebidas pela empresa</small></article><article class="stat-card"><div class="stat-top">Falhas no envio<span class="stat-icon red"><sms-icon name="alert"/></span></div><strong>{{ data.failed | number }}</strong><small>Envios que precisam de atenção</small></article></section>
    <div class="overview-grid"><section class="panel"><div class="panel-heading"><div><h2>Últimas mensagens</h2><p>As conversas mais recentes da sua empresa.</p></div><a class="text-button" routerLink="/app/mensagens">Ver todas <sms-icon name="arrow"/></a></div>
      @if (messages().length) { <div class="table-scroll"><table><thead><tr><th>Destinatário</th><th>Status</th><th>Data</th><th><span class="sr-only">Detalhes</span></th></tr></thead><tbody>@for (message of messages(); track message.id) { <tr><td><strong>{{ message.direction === 2 ? message.from : message.to }}</strong><small class="cell-secondary">{{ message.provider }} · {{ message.direction === 2 ? 'Recebida' : 'Enviada' }}</small></td><td><sms-status [status]="message.status"/></td><td class="muted nowrap">{{ message.createdAt | date:'dd MMM, HH:mm' }}</td><td><a class="icon-button" [routerLink]="['/app/mensagens', message.id]" aria-label="Ver detalhes da mensagem"><sms-icon name="arrow"/></a></td></tr> }</tbody></table></div> } @else { <div class="empty-state"><span class="empty-icon"><sms-icon name="message"/></span><h3>A primeira conversa começa aqui.</h3><p>Envie um SMS para ver o acompanhamento da entrega neste painel.</p><a routerLink="/app/enviar" class="button">Enviar primeiro SMS</a></div> }
    </section><div class="overview-aside"><section class="panel delivery-panel"><span class="eyebrow">ACOMPANHAMENTO</span><h2>Entrega de mensagens</h2><div class="delivery-number">{{ data.outbound ? data.delivered / data.outbound * 100 : 0 | number:'1.0-1' }}<span>%</span></div><p class="muted">dos envios com entrega confirmada</p><div class="progress-track" role="progressbar" aria-label="Percentual entregue" [attr.aria-valuenow]="data.outbound ? data.delivered / data.outbound * 100 : 0" aria-valuemin="0" aria-valuemax="100"><span [style.width.%]="data.outbound ? data.delivered / data.outbound * 100 : 0"></span></div><div class="delivery-legend"><span><i class="status-dot"></i>Entregues <b>{{ data.delivered }}</b></span><span><i class="status-dot amber"></i>Pendentes <b>{{ data.pending }}</b></span><span><i class="status-dot red"></i>Falhas <b>{{ data.failed }}</b></span></div></section>
      <section class="panel providers-summary"><h2>Provedores ativos</h2>@for (provider of data.providers; track provider.name) { <div class="provider-row"><span class="provider-avatar">{{ provider.name.slice(0,1) }}</span><div><strong>{{ provider.name }}</strong><small>{{ provider.fromNumber }}</small></div>@if (provider.isDefault) { <span class="badge success">Padrão</span> }</div> } @empty { <p class="muted">Nenhum provedor ativo. Solicite a configuração ao administrador.</p> }</section></div></div>
  }` })
export class OverviewComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient);
  readonly overview = signal<Overview | null>(null); readonly messages = signal<Message[]>([]);
  readonly loading = signal(false); readonly error = signal('');
  constructor() { this.load(); }
  load() { this.loading.set(true); this.error.set(''); forkJoin({ overview: this.http.get<Overview>('/api/v1/overview'), messages: this.http.get<Message[]>('/api/v1/messages?take=5') }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({ next: data => { this.overview.set(data.overview); this.messages.set(data.messages); }, error: error => this.error.set(errorMessage(error)) }); }
}
