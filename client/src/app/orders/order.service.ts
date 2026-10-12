import { HttpClient, HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { CreateOrderRequest, Order, OrderStatus } from './order.models';

export interface OrderRequestError {
  message: string;
  code: string | null;
}

@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly http = inject(HttpClient);
  private readonly collectionUrl = `${environment.apiUrl}/api/orders`;
  private readonly jsonHeaders = { 'Content-Type': 'application/json' };

  list(): Observable<Order[]> {
    return this.http.get<Order[]>(this.collectionUrl);
  }

  create(body: CreateOrderRequest): Observable<HttpResponse<Order>> {
    return this.http.post<Order>(this.collectionUrl, body, {
      observe: 'response',
      headers: this.jsonHeaders,
    });
  }

  changeStatus(orderId: string, status: OrderStatus): Observable<Order> {
    return this.http.patch<Order>(`${this.collectionUrl}/${orderId}/status`, { status }, {
      headers: this.jsonHeaders,
    });
  }
}

export function readOrderProblem(error: unknown): OrderRequestError {
  if (error instanceof HttpErrorResponse) {
    const problem = problemDetails(error.error);
    if (problem) {
      return problem;
    }

    if (error.status === 0) {
      return {
        message: 'The order service could not be reached. Start the API at https://localhost:7222.',
        code: null,
      };
    }
  }

  return { message: 'The request could not be completed.', code: null };
}

function problemDetails(body: unknown): OrderRequestError | null {
  if (!body || typeof body !== 'object') {
    return null;
  }

  const detail = 'detail' in body && typeof body.detail === 'string' ? body.detail.trim() : '';
  if (detail.length === 0) {
    return null;
  }

  const code = 'code' in body && typeof body.code === 'string' ? body.code : null;
  return { message: detail, code };
}
