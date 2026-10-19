import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ApiError } from '../api/api-error';
import { API_BASE_URL } from '../config/api-config';

/**
 * For requests to the Balboa API: tags each request with a correlation id and converts
 * HTTP failures into `ApiError`. Other origins pass through untouched.
 */
export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  const baseUrl = inject(API_BASE_URL);
  if (!req.url.startsWith(baseUrl)) {
    return next(req);
  }

  const tagged = req.clone({ setHeaders: { 'X-Correlation-Id': crypto.randomUUID() } });
  return next(tagged).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const title = (error.error as { title?: string } | null)?.title;
        return throwError(() => new ApiError(error.status, title ?? error.message, error.error));
      }
      return throwError(() => error);
    }),
  );
};
