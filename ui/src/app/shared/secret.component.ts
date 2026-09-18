import { Component, input, output, signal } from '@angular/core';
import { IssuedSecret } from '../core/models';
import { IconComponent } from './icon.component';

@Component({ selector: 'sms-secret', imports: [IconComponent], template: `
  <section class="secret-box" aria-label="Credencial gerada" role="status"><div class="section-heading"><div><span class="eyebrow">CREDENCIAL GERADA</span><h3>Guarde este segredo agora.</h3></div><sms-icon name="key"/></div>
    <p>Esta é a única exibição do segredo. Salve-o em um local seguro antes de sair desta tela.</p>
    <label>Client ID</label><code class="secret-value">{{ secret().clientId }}</code><label>Client secret</label><code class="secret-value">{{ secret().clientSecret }}</code>
    <div class="button-row"><button class="button" type="button" (click)="copy()">{{ copied() ? 'Copiado' : 'Copiar segredo' }}</button><button class="button primary" type="button" (click)="dismiss.emit()">Já guardei, ocultar</button></div>
    @if (copyError()) { <p class="muted">Selecione e copie o segredo manualmente. A cópia automática não está disponível.</p> }
  </section>` })
export class SecretComponent {
  readonly secret = input.required<IssuedSecret>(); readonly dismiss = output<void>();
  readonly copied = signal(false); readonly copyError = signal(false);
  async copy() { try { await navigator.clipboard.writeText(this.secret().clientSecret); this.copied.set(true); } catch { this.copyError.set(true); } }
}
