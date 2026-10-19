import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, of, tap } from 'rxjs';
import { ApiClient } from '../api/api-client';

export type FeatureFlags = Readonly<Record<string, boolean>>;

/** Reads feature flags from `GET /api/features`. Fails closed: unknown or unreachable means off. */
@Injectable({ providedIn: 'root' })
export class FeatureFlagService {
  private readonly api = inject(ApiClient);
  private readonly state = signal<FeatureFlags>({});
  private readonly loadedState = signal(false);

  readonly flags = this.state.asReadonly();
  readonly loaded = this.loadedState.asReadonly();
  readonly enabledCount = computed(() => Object.values(this.state()).filter(Boolean).length);

  load() {
    return this.api.get<FeatureFlags>('api/features').pipe(
      tap((flags) => this.state.set(flags)),
      catchError(() => {
        this.state.set({});
        return of({} as FeatureFlags);
      }),
      tap(() => this.loadedState.set(true)),
    );
  }

  isEnabled(name: string): boolean {
    return this.state()[name] === true;
  }
}
