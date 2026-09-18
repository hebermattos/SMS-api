import { Component, input, output, signal } from '@angular/core';
import { IssuedSecret } from '../core/models';
import { IconComponent } from './icon.component';

@Component({ selector: 'sms-secret', imports: [IconComponent], template: `
  <section class="secret-box" aria-label="Generated credential" role="status"><div class="section-heading"><div><span class="eyebrow">GENERATED CREDENTIAL</span><h3>Save this secret now.</h3></div><sms-icon name="key"/></div>
    <p>This is the only time the secret will be displayed. Store it securely before leaving this screen.</p>
    <label>Client ID</label><code class="secret-value">{{ secret().clientId }}</code><label>Client secret</label><code class="secret-value">{{ secret().clientSecret }}</code>
    <div class="button-row"><button class="button" type="button" (click)="copy()">{{ copied() ? 'Copied' : 'Copy secret' }}</button><button class="button primary" type="button" (click)="dismiss.emit()">I saved it, hide</button></div>
    @if (copyError()) { <p class="muted">Select and copy the secret manually. Automatic copying is unavailable.</p> }
  </section>` })
export class SecretComponent {
  readonly secret = input.required<IssuedSecret>(); readonly dismiss = output<void>();
  readonly copied = signal(false); readonly copyError = signal(false);
  async copy() { try { await navigator.clipboard.writeText(this.secret().clientSecret); this.copied.set(true); } catch { this.copyError.set(true); } }
}
