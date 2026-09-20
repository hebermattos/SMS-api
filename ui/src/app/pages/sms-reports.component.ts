import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { errorMessage } from '../core/api';
import { PlatformSmsReportSummary, SmsReportSummary, UserSmsReportSummary } from '../core/models';
import { IconComponent } from '../shared/icon.component';

@Component({ selector: 'sms-reports', imports: [FormsModule, DecimalPipe, IconComponent], template: `
<div class="page-heading"><div><span class="eyebrow">{{ auth.role() === 'admin' ? 'PLATFORM REPORTING' : 'YOUR WORKSPACE' }}</span><h1>SMS reports<span class="heading-dot">.</span></h1><p>{{ auth.role() === 'admin' ? 'Monitor sending activity across all companies.' : 'Understand your sending volume and delivery performance.' }}</p></div><div class="form-actions"><button class="button compact" (click)="downloadCsv()" [disabled]="loading() || !hasReport()">Download CSV</button><button class="button compact" (click)="load()" [disabled]="loading()"><sms-icon name="refresh"/>Refresh</button></div></div>
<section class="panel padded report-filters"><div class="section-heading"><div><h2>Filters</h2><p>Choose the period and dimensions for this report.</p></div><button class="text-button" (click)="clearFilters()">Clear filters</button></div><div class="form-grid"><div><label for="report-from">From</label><input id="report-from" name="from" type="date" [(ngModel)]="from"/></div><div><label for="report-to">To</label><input id="report-to" name="to" type="date" [(ngModel)]="to"/></div><div><label for="report-status">Status</label><select id="report-status" name="status" [(ngModel)]="status"><option value="">All statuses</option><option value="1">Queued</option><option value="2">Sent</option><option value="3">Delivered</option><option value="4">Failed</option><option value="5">Received</option></select></div><div><label for="report-direction">Direction</label><select id="report-direction" name="direction" [(ngModel)]="direction"><option value="">All directions</option><option value="1">Outbound</option><option value="2">Inbound</option></select></div><div><label for="report-provider">Provider</label><input id="report-provider" name="provider" [(ngModel)]="provider" placeholder="Twilio or Bandwidth"/></div></div><div class="form-actions"><button class="button primary" (click)="load()" [disabled]="loading()">{{ loading() ? 'Loading…' : 'Apply filters' }}</button></div></section>
@if (error()) { <div class="notice error" role="alert">{{ error() }}</div> } @if (loading()) { <div class="loading-state" role="status"><span class="spinner"></span>Loading report…</div> }
@if (tenantReport(); as report) { <section class="stats-grid"><article class="stat-card"><div class="stat-top">Total messages</div><strong>{{ report.totalMessages | number }}</strong></article><article class="stat-card"><div class="stat-top">Delivered</div><strong>{{ report.delivered | number }}</strong><small>{{ percent(report.delivered, report.totalMessages) }}% of total</small></article><article class="stat-card"><div class="stat-top">Failed</div><strong>{{ report.failed | number }}</strong><small>{{ percent(report.failed, report.totalMessages) }}% of total</small></article><article class="stat-card"><div class="stat-top">Outbound</div><strong>{{ report.outbound | number }}</strong><small>{{ report.inbound | number }} inbound</small></article></section><section class="panel"><div class="panel-heading"><div><h2>By provider</h2><p>Delivery results for the selected period.</p></div></div><div class="table-scroll"><table><thead><tr><th>Provider</th><th>Total</th><th>Delivered</th><th>Failed</th><th>Delivery rate</th></tr></thead><tbody>@for (provider of report.byProvider; track provider.provider) {<tr><td><strong>{{ provider.provider }}</strong></td><td>{{ provider.totalMessages | number }}</td><td>{{ provider.delivered | number }}</td><td>{{ provider.failed | number }}</td><td>{{ percent(provider.delivered, provider.totalMessages) }}%</td></tr>} @empty {<tr><td colspan="5" class="muted">No messages match the selected filters.</td></tr>}</tbody></table></div></section> }
@if (platformReport(); as report) { <section class="stats-grid"><article class="stat-card"><div class="stat-top">Total messages</div><strong>{{ report.totalMessages | number }}</strong></article><article class="stat-card"><div class="stat-top">Delivered</div><strong>{{ report.delivered | number }}</strong><small>{{ percent(report.delivered, report.totalMessages) }}% of total</small></article><article class="stat-card"><div class="stat-top">Failed</div><strong>{{ report.failed | number }}</strong><small>{{ percent(report.failed, report.totalMessages) }}% of total</small></article><article class="stat-card"><div class="stat-top">Companies</div><strong>{{ report.byTenant.length | number }}</strong></article></section><section class="panel"><div class="panel-heading"><div><h2>By company</h2><p>Sending activity grouped by tenant.</p></div></div><div class="table-scroll"><table><thead><tr><th>Company</th><th>Total</th><th>Queued</th><th>Sent</th><th>Delivered</th><th>Failed</th><th>Received</th></tr></thead><tbody>@for (tenant of report.byTenant; track tenant.tenantId) {<tr><td><strong>{{ tenant.tenantName }}</strong><small class="cell-secondary mono">{{ tenant.tenantId }}</small></td><td>{{ tenant.totalMessages | number }}</td><td>{{ tenant.queued | number }}</td><td>{{ tenant.sent | number }}</td><td>{{ tenant.delivered | number }}</td><td>{{ tenant.failed | number }}</td><td>{{ tenant.received | number }}</td></tr>} @empty {<tr><td colspan="7" class="muted">No messages match the selected filters.</td></tr>}</tbody></table></div></section> }
` }) export class SmsReportsComponent {
readonly auth=inject(AuthService); private readonly http=inject(HttpClient); private readonly destroyRef=inject(DestroyRef);
readonly tenantReport=signal<SmsReportSummary|null>(null); readonly userReport=signal<UserSmsReportSummary[]>([]); readonly platformReport=signal<PlatformSmsReportSummary|null>(null); readonly loading=signal(false); readonly error=signal('');
from=''; to=''; status=''; direction=''; provider='';
constructor(){this.load();this.loadUsers();}
load(){this.loading.set(true);this.error.set('');const url=this.auth.role()==='admin'?'/api/v1/admin/reports/sms':'/api/v1/reports/sms';this.http.get<SmsReportSummary|PlatformSmsReportSummary>(url,{params:this.params()}).pipe(takeUntilDestroyed(this.destroyRef),finalize(()=>this.loading.set(false))).subscribe({next:data=>{if(this.auth.role()==='admin'){this.platformReport.set(data as PlatformSmsReportSummary);this.tenantReport.set(null);}else{this.tenantReport.set(data as SmsReportSummary);this.platformReport.set(null);}},error:e=>this.error.set(errorMessage(e))});}
params(){let p=new HttpParams();if(this.from)p=p.set('from',this.from+'T00:00:00Z');if(this.to){const end=new Date(this.to+'T00:00:00Z');end.setUTCDate(end.getUTCDate()+1);p=p.set('to',end.toISOString());}if(this.status)p=p.set('status',this.status);if(this.direction)p=p.set('direction',this.direction);if(this.provider.trim())p=p.set('provider',this.provider.trim());return p;}
loadUsers(){if(this.auth.role()==='admin'){this.userReport.set([]);return;}this.http.get<UserSmsReportSummary[]>('/api/v1/reports/sms/users').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({next:data=>this.userReport.set(data),error:()=>this.userReport.set([])});}
clearFilters(){this.from='';this.to='';this.status='';this.direction='';this.provider='';this.load();}
hasReport(){return this.auth.role()==='admin'?this.platformReport()!==null:this.tenantReport()!==null;}
downloadCsv(){
  const rows:string[][]=[];
  if(this.auth.role()==='admin'){
    const report=this.platformReport(); if(!report)return;
    rows.push(['Company','Total','Queued','Sent','Delivered','Failed','Received']);
    for(const tenant of report.byTenant)rows.push([tenant.tenantName,String(tenant.totalMessages),String(tenant.queued),String(tenant.sent),String(tenant.delivered),String(tenant.failed),String(tenant.received)]);
  }else{
    const report=this.tenantReport(); if(!report)return;
    rows.push(['Provider','Total','Delivered','Failed','Delivery rate']);
    for(const provider of report.byProvider)rows.push([provider.provider,String(provider.totalMessages),String(provider.delivered),String(provider.failed),this.percent(provider.delivered,provider.totalMessages)+'%']);
  }
  const csv='\\uFEFF'+rows.map(row=>row.map(value=>this.csvValue(value)).join(',')).join('\\r\\n');
  const blob=new Blob([csv],{type:'text/csv;charset=utf-8'});
  const url=URL.createObjectURL(blob); const link=document.createElement('a');
  link.href=url; link.download='sms-report-'+new Date().toISOString().slice(0,10)+'.csv'; link.click(); URL.revokeObjectURL(url);
}
private csvValue(value:string){return '"'+value.replace(/"/g,'""')+'"';}
percent(v:number,t:number){return t?((v/t)*100).toFixed(1):'0.0';}
}
