import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api-config';
import { apiInterceptor } from '../http/api.interceptor';
import { ApiClient } from './api-client';
import { ApiError } from './api-error';

describe('ApiClient', () => {
  let client: ApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://api.test' },
      ],
    });
    client = TestBed.inject(ApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('[AC-0001-01] resolves paths against the configured base URL', () => {
    let result: unknown;
    client.get('/api/sample/widgets').subscribe((r) => (result = r));

    http.expectOne('http://api.test/api/sample/widgets').flush([{ id: '1' }]);
    expect(result).toEqual([{ id: '1' }]);
  });

  it('[AC-0001-05] sends a correlation id header on API calls', () => {
    client.get('api/features').subscribe();

    const req = http.expectOne('http://api.test/api/features');
    expect(req.request.headers.get('X-Correlation-Id')).toBeTruthy();
    req.flush({});
  });

  it('[AC-0001-03] maps a 404 to an ApiError with isNotFound', () => {
    let error: unknown;
    client.get('api/sample/widgets/missing').subscribe({ error: (e) => (error = e) });

    http
      .expectOne('http://api.test/api/sample/widgets/missing')
      .flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).isNotFound).toBe(true);
    expect((error as ApiError).message).toBe('Not Found');
  });

  it('[AC-0001-05] maps server failures to an ApiError carrying the status', () => {
    let error: unknown;
    client.post('api/sample/widgets', {}).subscribe({ error: (e) => (error = e) });

    http.expectOne('http://api.test/api/sample/widgets').flush(null, { status: 500, statusText: 'Server Error' });

    expect((error as ApiError).status).toBe(500);
    expect((error as ApiError).isNotFound).toBe(false);
  });
});
