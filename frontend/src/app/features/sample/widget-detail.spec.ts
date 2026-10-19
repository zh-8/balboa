// PLACEHOLDER: demonstrates the pattern only; will be replaced by the first real bounded context.
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { render, screen } from '@testing-library/angular';
import { API_BASE_URL } from '../../core/config/api-config';
import { apiInterceptor } from '../../core/http/api.interceptor';
import { WidgetDetail } from './widget-detail';
import { withInterceptors } from '@angular/common/http';

async function setup(id: string) {
  const view = await render(WidgetDetail, {
    inputs: { id },
    providers: [
      provideRouter([]),
      provideHttpClient(withInterceptors([apiInterceptor])),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: 'http://api.test' },
    ],
  });
  return view.fixture.debugElement.injector.get(HttpTestingController);
}

describe('WidgetDetail', () => {
  it('[AC-0001-02] loads the item identified by the route id', async () => {
    const http = await setup('a');
    http.expectOne('http://api.test/api/sample/widgets/a').flush({ id: 'a', name: 'Alpha', description: 'first' });

    expect(await screen.findByRole('heading', { name: 'Alpha' })).toBeTruthy();
    expect(screen.getByText('first')).toBeTruthy();
  });

  it('[AC-0001-03] shows not found when the item does not exist', async () => {
    const http = await setup('nope');
    http
      .expectOne('http://api.test/api/sample/widgets/nope')
      .flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });

    expect((await screen.findByRole('alert')).textContent).toContain('Widget not found.');
  });

  it('[AC-0001-05] shows a generic error on server failure', async () => {
    const http = await setup('a');
    http.expectOne('http://api.test/api/sample/widgets/a').flush(null, { status: 500, statusText: 'Server Error' });

    expect((await screen.findByRole('alert')).textContent).toContain('Could not load the widget.');
  });
});
