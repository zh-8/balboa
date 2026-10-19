// PLACEHOLDER: demonstrates the pattern only; will be replaced by the first real bounded context.
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client';
import { WidgetCatalogItem } from './widget-catalog-item';

@Injectable({ providedIn: 'root' })
export class SampleApi {
  private readonly api = inject(ApiClient);

  list(): Observable<WidgetCatalogItem[]> {
    return this.api.get<WidgetCatalogItem[]>('api/sample/widgets');
  }

  get(id: string): Observable<WidgetCatalogItem> {
    return this.api.get<WidgetCatalogItem>(`api/sample/widgets/${encodeURIComponent(id)}`);
  }
}
