import { InjectionToken } from '@angular/core';
import { environment } from './environments/environment';

/** Base URL of the backend API, without a trailing slash. Override in tests via providers. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => environment.apiBaseUrl.replace(/\/+$/, ''),
});
