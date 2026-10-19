import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-loading-indicator',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p role="status" aria-live="polite">Loading…</p>`,
})
export class LoadingIndicator {}
