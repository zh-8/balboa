import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'sample' },
  { path: 'sample', loadChildren: () => import('./features/sample/sample.routes').then((m) => m.SAMPLE_ROUTES) },
  { path: '**', redirectTo: 'sample' },
];
