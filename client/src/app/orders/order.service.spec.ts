import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { buildCreateOrderBody } from './create-order';
import { Order } from './order.models';
import { OrderService, readOrderProblem } from './order.service';

describe('OrderService', () => {
  let service: OrderService;
  let http: HttpTestingController;
  const ordersUrl = `${environment.apiUrl}/api/orders`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(OrderService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('posts the create payload unchanged and without totals', () => {
    const body = buildCreateOrderBody({
      externalReference: 'PO-1001',
      customerName: 'Northwind',
      lines: [{ description: 'Binder', quantity: 2, unitPrice: 3.5 }],
    });

    service.create(body).subscribe();

    const request = http.expectOne(ordersUrl);
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Content-Type')).toContain('application/json');
    expect(request.request.body).toBe(body);
    expect(Object.keys(request.request.body as object)).not.toContain('total');
    request.flush(storedOrder, { status: 201, statusText: 'Created' });
  });

  it('patches status with the status field only', () => {
    service.changeStatus(storedOrder.id, 'Confirmed').subscribe();

    const request = http.expectOne(`${ordersUrl}/${storedOrder.id}/status`);
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({ status: 'Confirmed' });
    request.flush({ ...storedOrder, status: 'Confirmed' });
  });
});

describe('readOrderProblem', () => {
  it('returns the API detail and code', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: {
        title: 'The external reference is already used.',
        detail: 'The external reference is already used.',
        code: 'duplicate_reference',
      },
    });

    expect(readOrderProblem(error)).toEqual({
      message: 'The external reference is already used.',
      code: 'duplicate_reference',
    });
  });

  it('drops stack traces and file paths when the body has no detail', () => {
    const error = new HttpErrorResponse({
      status: 500,
      error: {
        exception: 'System.IO.IOException',
        stackTrace: 'at JsonFileOrderRepository.Save',
        path: 'C:\\data\\orders.json',
      },
    });

    const problem = readOrderProblem(error);
    expect(problem.message).toBe('The request could not be completed.');
    expect(problem.message).not.toContain('orders.json');
    expect(problem.message).not.toContain('JsonFileOrderRepository');
  });
});

const storedOrder: Order = {
  id: '3f1c2a4e-7b8d-4e9a-8c1d-2b3a4c5d6e7f',
  externalReference: 'PO-1001',
  customerName: 'Northwind',
  lines: [{ description: 'Binder', quantity: 2, unitPrice: 3.5, lineTotal: 7 }],
  total: 7,
  status: 'Submitted',
  createdAtUtc: '2026-10-12T08:00:00Z',
};
