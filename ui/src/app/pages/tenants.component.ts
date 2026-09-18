import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Tenant, ProvisionedTenant, IssuedSecret } from '../core/models';
import { errorMessage } from '../core/api';
import { IconComponent } from '../shared/icon.component';
import { SecretComponent } from '../shared/secret.component';

@Component({ selector: 'sms-tenants', imports: [DatePipe, FormsModule, RouterLink, IconComponent, SecretComponent], template: `
  <div class="page-heading"><div><span class="eyebrow">ADMINISTRAÇÃO DA PLATAFORMA</span><h1>Empresas<span class="heading-dot">.</span></h1><p>Cada empresa tem seus próprios acessos, provedores e mensagens.</p></div><button class="button primary" (click)="creating.set(!creating())" [disabled]="!!secret()"><sms-icon name="plus"/>Nova empresa</button></div>
  @if (secret(); as issued) { <sms-secret [secret]="issued" (dismiss)="secret.set(null)"/> }
  @if (creating()) { <section class="panel padded create-panel"><div class="section-heading"><div><h2>Uma nova empresa</h2><p>O primeiro cliente de API será criado junto com ela.</p></div><button class="icon-button" aria-label="Cancelar cadastro" (click)="creating.set(false)" [disabled]="saving()"><sms-icon name="close"/></button></div><form #form="ngForm" (ngSubmit)="create()"><div class="form-grid"><div><label for="tenant-name">Nome da empresa</label><input id="tenant-name" name="name" [(ngModel)]="name" required maxlength="200" placeholder="Ex.: Acme Brasil"/></div><div><label for="initial-client">Client ID <span class="optional">opcional</span></label><input id="initial-client" name="clientId" [(ngModel)]="clientId" maxlength="100" pattern="[a-zA-Z0-9_-]+" placeholder="Gerado automaticamente se vazio"/></div></div><div class="form-actions"><button class="button" type="button" (click)="creating.set(false)" [disabled]="saving()">Cancelar</button><button class="button primary" [disabled]="form.invalid || saving() || !name.trim()">{{ saving() ? 'Criando…' : 'Criar empresa' }}</button></div></form></section> }
  @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
  <section class="panel"><div class="panel-heading"><div><h2>Empresas cadastradas</h2><p>Gerencie os acessos e a configuração de cada tenant.</p></div><button class="button compact" (click)="load()" [disabled]="loading()"><sms-icon name="refresh"/>Atualizar</button></div>
    @if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Carregando empresas…</div> } @else if (tenants().length) { <div class="table-scroll"><table><thead><tr><th>Empresa</th><th>Acesso</th><th>Criada em</th><th></th></tr></thead><tbody>@for (tenant of tenants(); track tenant.id) { <tr><td><div class="company-cell"><span class="company-avatar">{{ tenant.name.slice(0,2).toUpperCase() }}</span><div><strong>{{ tenant.name }}</strong><small class="cell-secondary mono">{{ tenant.id }}</small></div></div></td><td><span class="badge" [class.success]="tenant.isActive"><i class="status-dot"></i>{{ tenant.isActive ? 'Ativo' : 'Suspenso' }}</span></td><td class="muted">{{ tenant.createdAt | date:'dd/MM/yyyy' }}</td><td><a class="text-button" [routerLink]="['/admin/tenants', tenant.id]">Gerenciar <sms-icon name="arrow"/></a></td></tr> }</tbody></table></div> } @else { <div class="empty-state"><span class="empty-icon"><sms-icon name="users"/></span><h3>Seu próximo cliente começa aqui.</h3><p>Cadastre uma empresa para configurar seus acessos e provedores.</p><button class="button" (click)="creating.set(true)" [disabled]="!!secret()">Cadastrar empresa</button></div> }
    <div class="pagination"><span>Página {{ page() + 1 }}</span><div><button class="button compact" (click)="load(page() - 1)" [disabled]="loading() || page() === 0">Anterior</button><button class="button compact" (click)="load(page() + 1)" [disabled]="loading() || !hasNext()">Próxima</button></div></div>
  </section>` })
export class TenantsComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient); readonly tenants = signal<Tenant[]>([]); readonly secret = signal<IssuedSecret | null>(null);
  readonly loading = signal(false); readonly saving = signal(false); readonly creating = signal(false); readonly error = signal(''); readonly page = signal(0); readonly hasNext = signal(false);
  name = ''; clientId = '';
  constructor() { this.load(); }
  load(page = this.page()) { this.loading.set(true); this.error.set(''); this.http.get<Tenant[]>('/api/v1/admin/tenants', { params: { skip: page * 20, take: 21 } }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({ next: rows => { this.tenants.set(rows.slice(0,20)); this.hasNext.set(rows.length > 20); this.page.set(page); }, error: error => this.error.set(errorMessage(error)) }); }
  create() { if (this.saving() || !this.name.trim()) return; this.saving.set(true); this.error.set(''); this.http.post<ProvisionedTenant>('/api/v1/admin/tenants', { name: this.name.trim(), clientId: this.clientId.trim() || null }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({ next: data => { this.secret.set({ clientId: data.client_id, clientSecret: data.client_secret }); this.creating.set(false); this.name = ''; this.clientId = ''; this.load(0); }, error: error => this.error.set(errorMessage(error)) }); }
}
