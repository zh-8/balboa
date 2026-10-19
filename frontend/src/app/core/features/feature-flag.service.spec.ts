import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../config/api-config';
import { FeatureFlagService } from './feature-flag.service';

describe('FeatureFlagService', () => {
  let service: FeatureFlagService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://api.test' },
      ],
    });
    service = TestBed.inject(FeatureFlagService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('[AC-0001-04] reads flags from GET /api/features', () => {
    service.load().subscribe();
    http.expectOne('http://api.test/api/features').flush({ sample: true, beta: false });

    expect(service.isEnabled('sample')).toBe(true);
    expect(service.isEnabled('beta')).toBe(false);
    expect(service.enabledCount()).toBe(1);
    expect(service.loaded()).toBe(true);
  });

  it('[AC-0001-04] treats unknown flags as disabled', () => {
    service.load().subscribe();
    http.expectOne('http://api.test/api/features').flush({ sample: true });

    expect(service.isEnabled('does-not-exist')).toBe(false);
  });

  it('[AC-0001-04] fails closed when the API is unreachable', () => {
    service.load().subscribe();
    http.expectOne('http://api.test/api/features').flush(null, { status: 503, statusText: 'Unavailable' });

    expect(service.flags()).toEqual({});
    expect(service.isEnabled('sample')).toBe(false);
    expect(service.loaded()).toBe(true);
  });
});
