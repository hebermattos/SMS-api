import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { MessageTemplate } from '../core/models';

@Component({
  selector: 'sms-templates',
  imports: [FormsModule],
  template: `
    <div class="page-header"><div><span class="eyebrow">MESSAGING</span><h1>Message templates</h1><p>Create reusable SMS content with variables such as <code>{{ '{{firstName}}' }}</code>.</p></div>
      <button class="primary" (click)="newTemplate()">New template</button></div>

    @if (editing) {
      <section class="card">
        <h2>{{ editing.id ? 'Edit template' : 'New template' }}</h2>
        <label>Name<input [(ngModel)]="editing.name" maxlength="120" placeholder="Appointment reminder"></label>
        <label>Message<textarea [(ngModel)]="editing.body" maxlength="4000" rows="6" placeholder="Hello {{ '{{firstName}}' }}, your appointment is on {{ '{{date}}' }}."></textarea></label>
        <p class="muted">Variables use <code>{{ '{{variableName}}' }}</code>. Values are supplied when the template is rendered.</p>
        <div class="actions"><button class="primary" (click)="save()">Save template</button><button (click)="editing = null">Cancel</button></div>
        @if (error) { <p class="error">{{ error }}</p> }
      </section>
    }

    <section class="card">
      @if (!templates.length) { <p>No templates yet.</p> }
      @for (template of templates; track template.id) {
        <div class="list-row">
          <div><strong>{{ template.name }}</strong><p>{{ template.body }}</p>
            @if (template.variables.length) { <small>Variables: {{ template.variables.join(', ') }}</small> }
          </div>
          <div class="actions"><button (click)="edit(template)">Edit</button><button (click)="remove(template)">Delete</button></div>
        </div>
      }
    </section>
  `
})
export class TemplatesComponent implements OnInit {
  private readonly http = inject(HttpClient);
  templates: MessageTemplate[] = [];
  editing: { id?: string; name: string; body: string } | null = null;
  error = '';

  ngOnInit() { this.load(); }
  load() { this.http.get<MessageTemplate[]>('/api/v1/templates?take=200').subscribe(items => this.templates = items); }
  newTemplate() { this.error = ''; this.editing = { name: '', body: '' }; }
  edit(item: MessageTemplate) { this.error = ''; this.editing = { id: item.id, name: item.name, body: item.body }; }
  save() {
    if (!this.editing) return;
    this.error = '';
    const request = { name: this.editing.name, body: this.editing.body };
    const call = this.editing.id
      ? this.http.put<MessageTemplate>(`/api/v1/templates/${this.editing.id}`, request)
      : this.http.post<MessageTemplate>('/api/v1/templates', request);
    call.subscribe({ next: () => { this.editing = null; this.load(); }, error: e => this.error = e.error || 'The template could not be saved.' });
  }
  remove(item: MessageTemplate) {
    if (!confirm(`Delete "${item.name}"?`)) return;
    this.http.delete(`/api/v1/templates/${item.id}`).subscribe(() => this.load());
  }
}
