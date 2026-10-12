import { Order } from './order.models';
import { summarizeOrders } from './order-summary';

function order(status: string, total: number): Order {
  return {
    id: `${status}-${total}`,
    externalReference: 'PO',
    customerName: 'Acme',
    lines: [],
    total,
    status,
    createdAtUtc: '2026-01-01T00:00:00Z',
  };
}

describe('summarizeOrders', () => {
  it('counts each status and sums value without cancelled orders', () => {
    const summary = summarizeOrders([
      order('Submitted', 10.1),
      order('Confirmed', 0.2),
      order('Fulfilled', 3.33),
      order('Cancelled', 99),
    ]);

    expect(summary).toEqual({
      count: 4,
      open: 2,
      fulfilled: 1,
      cancelled: 1,
      value: 13.63,
    });
  });

  it('returns zeros when there are no orders', () => {
    expect(summarizeOrders([])).toEqual({
      count: 0,
      open: 0,
      fulfilled: 0,
      cancelled: 0,
      value: 0,
    });
  });
});
