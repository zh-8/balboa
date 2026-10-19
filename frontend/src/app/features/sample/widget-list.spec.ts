// PLACEHOLDER: demonstrates the pattern only; will be replaced by the first real bounded context.
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { render, screen } from '@testing-library/angular';
import { API_BASE_URL } from '../../core/config/api-config';
import { WidgetList } from './widget-list';

async function setup() {
  const view = await render(WidgetList, {
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: 'http://api.test' },
    ],
  });
  const http = view.fixture.debugElement.injector.get(HttpTestingController);
  return { view, http };
}

describe('WidgetList', () => {
  it('[AC-0001-06] loads items on init and lists them', async () => {
    const { http } = await setup();
    expect(screen.getByRole('status')).toBeTruthy();

    http.expectOne('http://api.test/api/sample/widgets').flush([
      { id: 'a', sku: 'WGT-A', name: 'Alpha', isPublished: false },
      { id: 'b', sku: 'WGT-B', name: 'Beta', isPublished: true },
    ]);

    expect(await screen.findByRole('link', { name: 'Alpha' })).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Beta' })).toBeTruthy();
    expect(screen.getByText(/WGT-B/).textContent).toContain('published');
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('[AC-0001-06] shows an empty state when there are no items', async () => {
    const { http } = await setup();
    http.expectOne('http://api.test/api/sample/widgets').flush([]);

    expect(await screen.findByText('No widgets yet.')).toBeTruthy();
  });

  it('shows an error when the API fails', async () => {
    const { http } = await setup();
    http.expectOne('http://api.test/api/sample/widgets').flush(null, { status: 500, statusText: 'Server Error' });

    expect((await screen.findByRole('alert')).textContent).toContain('Could not load the widget catalogue.');
  });
});
