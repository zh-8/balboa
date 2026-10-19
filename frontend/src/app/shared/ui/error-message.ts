import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-error-message',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p role="alert" class="error">{{ message() }}</p>`,
  styles: `
    .error {
      color: #b00020;
    }
  `,
})
export class ErrorMessage {
  readonly message = input.required<string>();
}
