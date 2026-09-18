import { Component, input } from '@angular/core';

export const statusLabels: Record<number, string> = { 1: 'Na fila', 2: 'Enviada', 3: 'Entregue', 4: 'Falhou', 5: 'Recebida' };
@Component({ selector: 'sms-status', template: `<span class="badge" [class.success]="status() === 3 || status() === 5" [class.danger]="status() === 4" [class.warning]="status() === 1"><span class="status-dot"></span>{{ labels[status()] || 'Desconhecido' }}</span>` })
export class StatusComponent { readonly status = input.required<number>(); readonly labels = statusLabels; }
