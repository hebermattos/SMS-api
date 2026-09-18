import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TenantDetailComponent } from './tenant-detail.component';
import { ProviderConfig, ProviderDefinition } from '../core/models';

describe('Provider administration', () => {
  let http: HttpTestingController;
  const definition: ProviderDefinition = { name: 'Bandwidth', accountLabel: 'OAuth Client ID', secretLabel: 'Client Secret', fields: [{ key: 'webhookPassword', label: 'Callback password', secret: true, required: true }] };
  const provider: ProviderConfig = { provider: 'Bandwidth', accountId: 'account', fromNumber: '+15550000001', isActive: true, isDefault: true, hasApiSecret: true, settings: {}, configuredSecrets: ['webhookPassword'] };
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'tenant-1' }) } } }] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function create() {
    const fixture = TestBed.createComponent(TenantDetailComponent);
    http.expectOne('/api/v1/admin/tenants/tenant-1').flush({ id: 'tenant-1', name: 'Company', isActive: true, createdAt: '2026-01-01' });
    http.expectOne('/api/v1/admin/tenants/tenant-1/clients?take=21').flush([]);
    http.expectOne('/api/v1/admin/tenants/tenant-1/providers').flush([provider]);
    http.expectOne('/api/v1/admin/providers/catalog').flush([definition]);
    return fixture;
  }
  it('leaves stored secrets blank and submits blank values to preserve them', () => {
    const fixture = create(); const component = fixture.componentInstance;
    component.editProvider(definition);
    expect(component.apiSecret).toBe(''); expect(component.settings['webhookPassword']).toBe('');
    component.saveProvider();
    const request = http.expectOne('/api/v1/admin/tenants/tenant-1/providers/Bandwidth');
    expect(request.request.body.apiSecret).toBeNull(); expect(request.request.body.settings.webhookPassword).toBe('');
    request.flush(null); http.expectOne('/api/v1/admin/tenants/tenant-1/providers').flush([provider]);
    expect(component.editingProvider()).toBeNull(); expect(component.apiSecret).toBe(''); fixture.destroy();
  });
  it('disables duplicate client creation until the client list finishes refreshing', () => {
    const fixture = create(); const component = fixture.componentInstance;
    component.createClient(); http.expectOne('/api/v1/admin/tenants/tenant-1/clients').flush({ clientId: 'new-client', clientSecret: 'one-time-secret' });
    expect(component.busy()).toBe(true); expect(component.secret()?.clientSecret).toBe('one-time-secret');
    http.expectOne('/api/v1/admin/tenants/tenant-1/clients?take=21').flush([]);
    expect(component.busy()).toBe(false); component.createClient();
    http.expectNone('/api/v1/admin/tenants/tenant-1/clients'); fixture.destroy();
  });
});
