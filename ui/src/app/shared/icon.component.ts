import { Component, input } from '@angular/core';

@Component({
  selector: 'sms-icon',
  template: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path [attr.d]="paths[name()] || paths['message']"/></svg>`,
  styles: [':host{display:inline-flex;width:1.2em;height:1.2em;flex-shrink:0}svg{width:100%;height:100%}']
})
export class IconComponent {
  readonly name = input('message');
  readonly paths: Record<string, string> = {
    message: 'M4 4h16v12H9l-5 4V4z M8 8h8 M8 12h5',
    grid: 'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z',
    send: 'm21 3-7 18-4-7-7-4 18-7z M10 14 21 3',
    users: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M9 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8 M22 21v-2a4 4 0 0 0-3-3.9 M16 3.1a4 4 0 0 1 0 7.8',
    key: 'M8 3a5 5 0 1 0 0 10 5 5 0 0 0 0-10 M12 12l9 9 M17 17l3-3 M8 7h.01',
    plug: 'M8 3v4 M16 3v4 M6 7h12v4a6 6 0 0 1-12 0V7z M12 17v4',
    logs: 'M6 3h12v18H6z M9 7h6 M9 11h6 M9 15h4',
    arrow: 'M5 12h14 M13 6l6 6-6 6',
    check: 'm5 12 4 4L19 6',
    clock: 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18 M12 7v5l3 2',
    down: 'M12 4v16 M6 14l6 6 6-6',
    plus: 'M12 5v14 M5 12h14',
    close: 'm6 6 12 12 M6 18 18 6',
    logout: 'M9 4H4v16h5 M9 12h12 M16 7l5 5-5 5',
    shield: 'm12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6l8-3z m-4 9 3 3 5-6',
    refresh: 'M20 7v5h-5 M4 17v-5h5 M6 6a8 8 0 0 1 14 6 M18 18a8 8 0 0 1-14-6',
    menu: 'M4 6h16 M4 12h16 M4 18h16',
    alert: 'm12 3 10 18H2L12 3z M12 9v5 M12 17h.01'
  };
}
