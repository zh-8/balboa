// PLACEHOLDER: demonstrates the pattern only; will be replaced by the first real bounded context.
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client';
import { Widget } from './widget';

@Injectable({ providedIn: 'root' })
export class SampleApi {
  private readonly api = inject(ApiClient);

  list(): Observable<Widget[]> {
    return this.api.get<Widget[]>('api/sample/widgets');
  }

  get(id: string): Observable<Widget> {
    return this.api.get<Widget>(`api/sample/widgets/${encodeURIComponent(id)}`);
  }
}
