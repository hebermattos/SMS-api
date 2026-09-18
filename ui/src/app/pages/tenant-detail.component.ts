import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, finalize, switchMap, tap } from 'rxjs';
import { Tenant, Client, IssuedSecret, ProviderConfig, ProviderDefinition } from '../core/models';
import { errorMessage } from '../core/api';
import { IconComponent } from '../shared/icon.component';
import { SecretComponent } from '../shared/secret.component';

@Component({ selector: 'sms-tenant-detail', imports: [DatePipe, FormsModule, RouterLink, IconComponent, SecretComponent], templateUrl: './tenant-detail.component.html' })
export class TenantDetailComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient); private readonly route = inject(ActivatedRoute);
  private readonly base = `/api/v1/admin/tenants/${encodeURIComponent(this.route.snapshot.paramMap.get('id') || '')}`;
  readonly tenant = signal<Tenant | null>(null); readonly clients = signal<Client[]>([]); readonly providers = signal<ProviderConfig[]>([]); readonly catalog = signal<ProviderDefinition[]>([]);
  readonly tab = signal<'settings' | 'clients' | 'providers'>('clients'); readonly loading = signal(false); readonly busy = signal(false); readonly error = signal(''); readonly success = signal('');
  readonly secret = signal<IssuedSecret | null>(null); readonly page = signal(0); readonly hasNext = signal(false); readonly editingProvider = signal<ProviderDefinition | null>(null); readonly existingProvider = signal<ProviderConfig | null>(null);
  tenantName = ''; clientId = ''; accountId = ''; fromNumber = ''; apiSecret = ''; providerActive = true; providerDefault = false; settings: Record<string, string> = {};

  constructor() { this.load(); }
  load() {
    this.loading.set(true); this.error.set('');
    forkJoin({ tenant: this.http.get<Tenant>(this.base), clients: this.http.get<Client[]>(`${this.base}/clients?take=21`), providers: this.http.get<ProviderConfig[]>(`${this.base}/providers`), catalog: this.http.get<ProviderDefinition[]>('/api/v1/admin/providers/catalog') })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading.set(false))).subscribe({ next: data => { this.tenant.set(data.tenant); this.tenantName = data.tenant.name; this.clients.set(data.clients.slice(0,20)); this.hasNext.set(data.clients.length > 20); this.page.set(0); this.providers.set(data.providers); this.catalog.set(data.catalog); }, error: error => this.error.set(errorMessage(error)) });
  }
  changeTab(tab: 'settings' | 'clients' | 'providers') { if (this.busy()) return; this.tab.set(tab); this.error.set(''); this.success.set(''); this.closeProvider(); }
  updateTenant(isActive = this.tenant()?.isActive ?? true) {
    const tenant = this.tenant(); if (!tenant || this.busy() || !this.tenantName.trim()) return;
    if (tenant.isActive && !isActive && !confirm("Suspend this company's access? API clients and active sessions will lose access. Data will be preserved.")) return;
    this.start(); this.http.put<void>(this.base, { name: this.tenantName.trim(), isActive }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({ next: () => { this.tenant.set({ ...tenant, name: this.tenantName.trim(), isActive }); this.success.set('Company updated.'); }, error: error => this.error.set(errorMessage(error)) });
  }
  loadClients(page = this.page()) {
    this.start(); this.http.get<Client[]>(`${this.base}/clients`, { params: { skip: page * 20, take: 21 } }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({ next: rows => { this.clients.set(rows.slice(0,20)); this.hasNext.set(rows.length > 20); this.page.set(page); }, error: error => this.error.set(errorMessage(error)) });
  }
  createClient() {
    if (this.busy() || this.secret()) return; this.start();
    this.http.post<IssuedSecret>(`${this.base}/clients`, { clientId: this.clientId.trim() || null }).pipe(
      tap(secret => { this.secret.set(secret); this.clientId = ''; }),
      switchMap(() => this.http.get<Client[]>(`${this.base}/clients?take=21`)),
      takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))
    ).subscribe({ next: rows => { this.clients.set(rows.slice(0, 20)); this.hasNext.set(rows.length > 20); this.page.set(0); }, error: error => this.error.set(errorMessage(error)) });
  }
  setClientState(client: Client) {
    if (this.busy() || (client.isActive && !confirm("Deactivate this client? Its sessions will lose API access."))) return;
    this.start(); this.http.put<void>(`${this.base}/clients/${client.id}/state`, { isActive: !client.isActive }).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({ next: () => { this.clients.update(rows => rows.map(row => row.id === client.id ? { ...row, isActive: !row.isActive } : row)); this.success.set('Client access updated.'); }, error: error => this.error.set(errorMessage(error)) });
  }
  rotate(client: Client) {
    if (this.busy() || this.secret() || !confirm('Generate another secret? The previous secret will stop working for new authentications. Update integrations that use this client.')) return;
    this.start(); this.http.post<IssuedSecret>(`${this.base}/clients/${client.id}/rotate-secret`, {}).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({ next: secret => this.secret.set(secret), error: error => this.error.set(errorMessage(error)) });
  }
  providerFor(name: string) { return this.providers().find(x => x.provider === name); }
  editProvider(definition: ProviderDefinition) {
    const existing = this.providerFor(definition.name); this.editingProvider.set(definition); this.existingProvider.set(existing || null);
    this.accountId = existing?.accountId || ''; this.fromNumber = existing?.fromNumber || ''; this.providerActive = existing?.isActive ?? true; this.providerDefault = existing?.isDefault ?? !this.providers().some(x => x.isActive && x.isDefault);
    this.apiSecret = ''; this.settings = {}; for (const field of definition.fields) this.settings[field.key] = field.secret ? '' : existing?.settings[field.key] || '';
    this.error.set(''); this.success.set('');
  }
  closeProvider() { this.editingProvider.set(null); this.existingProvider.set(null); this.apiSecret = ''; this.settings = {}; }
  saveProvider() {
    const definition = this.editingProvider(); if (!definition || this.busy()) return;
    this.start(); this.http.put<void>(`${this.base}/providers/${encodeURIComponent(definition.name)}`, { accountId: this.accountId, fromNumber: this.fromNumber, isActive: this.providerActive, isDefault: this.providerActive && this.providerDefault, apiSecret: this.apiSecret || null, settings: this.settings })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => { this.busy.set(false); this.apiSecret = ''; for (const field of definition.fields.filter(x => x.secret)) this.settings[field.key] = ''; })).subscribe({ next: () => { this.closeProvider(); this.success.set('Configuration saved securely.'); this.http.get<ProviderConfig[]>(`${this.base}/providers`).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rows => this.providers.set(rows), error: error => this.error.set(errorMessage(error)) }); }, error: error => this.error.set(errorMessage(error)) });
  }
  private start() { this.busy.set(true); this.error.set(''); this.success.set(''); }
}
