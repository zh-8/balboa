// PLACEHOLDER: demonstrates the pattern only; will be replaced by the first real bounded context.
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ErrorMessage } from '../../shared/ui/error-message';
import { LoadingIndicator } from '../../shared/ui/loading-indicator';
import { SampleApi } from './sample-api';
import { WidgetCatalogItem } from './widget-catalog-item';

@Component({
  selector: 'app-widget-list',
  imports: [RouterLink, LoadingIndicator, ErrorMessage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2>Widget catalogue</h2>
    @if (loading()) {
      <app-loading-indicator />
    } @else if (error(); as message) {
      <app-error-message [message]="message" />
    } @else if (items().length === 0) {
      <p>No widgets yet.</p>
    } @else {
      <ul>
        @for (item of items(); track item.id) {
          <li>
            <a [routerLink]="[item.id]">{{ item.name }}</a>
          </li>
        }
      </ul>
    }
  `,
})
export class WidgetList implements OnInit {
  private readonly api = inject(SampleApi);

  readonly items = signal<WidgetCatalogItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.list().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load the widget catalogue.');
        this.loading.set(false);
      },
    });
  }
}
