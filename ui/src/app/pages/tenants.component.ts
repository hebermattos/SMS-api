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
  <div class="page-heading"><div><span class="eyebrow">PLATFORM ADMINISTRATION</span><h1>Companies<span class="heading-dot">.</span></h1><p>Each company has its own access, providers, and messages.</p></div><button class="button primary" (click)="creating.set(!creating())" [disabled]="!!secret()"><sms-icon name="plus"/>New company</button></div>
  @if (secret(); as issued) { <sms-secret [secret]="issued" (dismiss)="secret.set(null)"/> }
  @if (creating()) { <section class="panel padded create-panel"><div class="section-heading"><div><h2>A new company</h2><p>Its first API client will be created at the same time.</p></div><button class="icon-button" aria-label="Cancel registration" (click)="creating.set(false)" [disabled]="saving()"><sms-icon name="close"/></button></div><form #form="ngForm" (ngSubmit)="create()"><div class="form-grid"><div><label for="tenant-name">Company name</label><input id="tenant-name" name="name" [(ngModel)]="name" required maxlength="200" placeholder="Example: Acme Inc."/></div><div><label for="initial-client">Client ID <span class="optional">optional</span></label><input id="initial-client" name="clientId" [(ngModel)]="clientId" maxlength="100" pattern="[a-zA-Z0-9_-]+" placeholder="Generated automatically if blank"/></div></div><div class="form-actions"><button class="button" type="button" (click)="creating.set(false)" [disabled]="saving()">Cancel</button><button class="button primary" [disabled]="form.invalid || saving() || !name.trim()">{{ saving() ? 'Creating…' : 'Create company' }}</button></div></form></section> }
  @if (error()) { <div class="notice error" role="alert">{{ error() }}</div> }
  <section class="panel"><div class="panel-heading"><div><h2>Registered companies</h2><p>Manage each tenant's access and configuration.</p></div><button class="button compact" (click)="load()" [disabled]="loading()"><sms-icon name="refresh"/>Refresh</button></div>
    @if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Loading companies…</div> } @else if (tenants().length) { <div class="table-scroll"><table><thead><tr><th>Company</th><th>Access</th><th>Created at</th><th></th></tr></thead><tbody>@for (tenant of tenants(); track tenant.id) { <tr><td><div class="company-cell"><span class="company-avatar">{{ tenant.name.slice(0,2).toUpperCase() }}</span><div><strong>{{ tenant.name }}</strong><small class="cell-secondary mono">{{ tenant.id }}</small></div></div></td><td><span class="badge" [class.success]="tenant.isActive"><i class="status-dot"></i>{{ tenant.isActive ? 'Active' : 'Suspended' }}</span></td><td class="muted">{{ tenant.createdAt | date:'MM/dd/yyyy' }}</td><td><a class="text-button" [routerLink]="['/admin/tenants', tenant.id]">Manage <sms-icon name="arrow"/></a></td></tr> }</tbody></table></div> } @else { <div class="empty-state"><span class="empty-icon"><sms-icon name="users"/></span><h3>Your next client starts here.</h3><p>Register a company to configure its access and providers.</p><button class="button" (click)="creating.set(true)" [disabled]="!!secret()">Register company</button></div> }
    <div class="pagination"><span>Page {{ page() + 1 }}</span><div><button class="button compact" (click)="load(page() - 1)" [disabled]="loading() || page() === 0">Previous</button><button class="button compact" (click)="load(page() + 1)" [disabled]="loading() || !hasNext()">Next</button></div></div>
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
