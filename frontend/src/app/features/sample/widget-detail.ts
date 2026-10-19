// PLACEHOLDER: demonstrates the pattern only; will be replaced by the first real bounded context.
import { ChangeDetectionStrategy, Component, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError } from '../../core/api/api-error';
import { ErrorMessage } from '../../shared/ui/error-message';
import { LoadingIndicator } from '../../shared/ui/loading-indicator';
import { SampleApi } from './sample-api';
import { WidgetCatalogItem } from './widget-catalog-item';

@Component({
  selector: 'app-widget-detail',
  imports: [RouterLink, LoadingIndicator, ErrorMessage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="..">Back to catalogue</a>
    @if (loading()) {
      <app-loading-indicator />
    } @else if (error(); as message) {
      <app-error-message [message]="message" />
    } @else if (item(); as widget) {
      <h2>{{ widget.name }}</h2>
      <p>{{ widget.description ?? 'No description.' }}</p>
    }
  `,
})
export class WidgetDetail {
  private readonly api = inject(SampleApi);

  /** Bound from the `:id` route parameter (withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly item = signal<WidgetCatalogItem | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.load(id));
    });
  }

  private load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.get(id).subscribe({
      next: (item) => {
        this.item.set(item);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(err instanceof ApiError && err.isNotFound ? 'Widget not found.' : 'Could not load the widget.');
        this.loading.set(false);
      },
    });
  }
}
