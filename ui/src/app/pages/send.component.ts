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
  <div class="page-heading"><div><span class="eyebrow">NOVA CONVERSA</span><h1>Enviar SMS<span class="heading-dot">.</span></h1><p>Escreva sua mensagem. Nós cuidamos do caminho até o destinatário.</p></div></div>
  @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
  @if (result(); as sent) { <div class="notice success" role="status"><sms-icon name="check"/><div><strong>Solicitação enviada.</strong><p>O status de entrega será atualizado pelo provedor.</p><a [routerLink]="['/app/mensagens', sent.id]">Acompanhar mensagem →</a></div></div> }
  @if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Carregando provedores…</div> }
  @else if (overview(); as data) { @if (!data.providers.length) { <div class="empty-state panel"><sms-icon name="plug"/><h3>Falta configurar um provedor.</h3><p>Solicite ao administrador um número de envio e um provedor ativo para sua empresa.</p></div> } @else {
    <div class="compose-grid"><section class="panel padded"><form #form="ngForm" (ngSubmit)="send()"><div class="section-heading"><h2>Detalhes do envio</h2><span class="badge">SMS</span></div><label for="destination">Número de destino</label><input id="destination" name="to" [(ngModel)]="to" type="tel" required pattern="\\+[1-9][0-9]{6,14}" maxlength="16" placeholder="+5511999999999" autocomplete="tel"/><p class="field-help">Inclua +, código do país e DDD.</p><label for="send-provider">Provedor</label><select id="send-provider" name="provider" [(ngModel)]="provider" required>@for (option of data.providers; track option.name) { <option [value]="option.name">{{ option.name }}{{ option.isDefault ? ' · Padrão' : '' }} — {{ option.fromNumber }}</option> }</select><label for="body">Sua mensagem</label><textarea id="body" name="body" [(ngModel)]="body" required maxlength="1600" rows="7" placeholder="O que você gostaria de dizer?"></textarea><div class="field-counter"><span>Mensagens longas podem usar mais de um segmento.</span><strong>{{ body.length }}/1600</strong></div><div class="form-actions"><a class="button" routerLink="/app/mensagens">Cancelar</a><button class="button primary" type="submit" [disabled]="form.invalid || busy() || !body.trim()"><sms-icon name="send"/>{{ busy() ? 'Enviando…' : 'Enviar mensagem' }}</button></div></form></section><aside><section class="phone-preview"><div class="phone-speaker"></div><span class="phone-title">Prévia da mensagem</span><div class="sms-bubble">{{ body || 'Sua mensagem aparecerá aqui enquanto você escreve.' }}</div><small>Prévia ilustrativa</small></section><p class="compose-note"><sms-icon name="shield"/>O envio usa o número configurado para sua empresa.</p></aside></div>
  } } @else if (!loading()) { <button class="button" (click)="load()">Tentar novamente</button> }
` })
export class SendComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient); readonly overview = signal<Overview | null>(null);
  readonly result = signal<SendResult | null>(null); readonly loading = signal(false); readonly busy = signal(false); readonly error = signal('');
  to = ''; body = ''; provider = '';
  constructor() { this.load(); }
  load() { this.loading.set(true); this.error.set(''); this.http.get<Overview>('/api/v1/overview').pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({ next: data => { this.overview.set(data); this.provider = data.providers.find(x => x.isDefault)?.name || data.providers[0]?.name || ''; }, error: error => this.error.set(errorMessage(error)) }); }
  send() { if (this.busy() || !this.body.trim() || !/^\+[1-9][0-9]{6,14}$/.test(this.to)) return; this.busy.set(true); this.error.set(''); this.result.set(null); this.http.post<SendResult>('/api/v1/messages', { to: this.to, body: this.body, provider: this.provider }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({ next: result => { this.result.set(result); this.body = ''; this.to = ''; }, error: error => this.error.set(errorMessage(error) + ' Consulte as mensagens antes de tentar enviar novamente, para evitar duplicidade.') }); }
}
