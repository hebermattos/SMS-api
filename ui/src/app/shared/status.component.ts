import { Component, input } from '@angular/core';

export const statusLabels: Record<number, string> = { 1: 'Queued', 2: 'Sent', 3: 'Delivered', 4: 'Failed', 5: 'Received' };
@Component({ selector: 'sms-status', template: `<span class="badge" [class.success]="status() === 3 || status() === 5" [class.danger]="status() === 4" [class.warning]="status() === 1"><span class="status-dot"></span>{{ labels[status()] || 'Unknown' }}</span>` })
export class StatusComponent { readonly status = input.required<number>(); readonly labels = statusLabels; }
