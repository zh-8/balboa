// PLACEHOLDER: demonstrates the pattern only; will be replaced by the first real bounded context.
import { Routes } from '@angular/router';

export const SAMPLE_ROUTES: Routes = [
  { path: '', title: 'Widgets', loadComponent: () => import('./widget-list').then((m) => m.WidgetList) },
  { path: ':id', title: 'Widget', loadComponent: () => import('./widget-detail').then((m) => m.WidgetDetail) },
];
