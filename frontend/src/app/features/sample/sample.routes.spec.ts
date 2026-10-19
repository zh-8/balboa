// PLACEHOLDER: demonstrates the pattern only; will be replaced by the first real bounded context.
import { SAMPLE_ROUTES } from './sample.routes';
import { WidgetDetail } from './widget-detail';
import { WidgetList } from './widget-list';

describe('SAMPLE_ROUTES', () => {
  it('lazy-loads the list at the feature root', async () => {
    const route = SAMPLE_ROUTES.find((r) => r.path === '');
    expect(await route?.loadComponent?.()).toBe(WidgetList);
  });

  it('lazy-loads the detail at :id', async () => {
    const route = SAMPLE_ROUTES.find((r) => r.path === ':id');
    expect(await route?.loadComponent?.()).toBe(WidgetDetail);
  });
});
