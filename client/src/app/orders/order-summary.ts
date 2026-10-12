import { Order } from './order.models';

export interface OrderSummary {
  count: number;
  open: number;
  fulfilled: number;
  cancelled: number;
  value: number;
}

export function summarizeOrders(orders: readonly Order[]): OrderSummary {
  let open = 0;
  let fulfilled = 0;
  let cancelled = 0;
  let valueCents = 0;

  for (const order of orders) {
    if (order.status === 'Submitted' || order.status === 'Confirmed') {
      open += 1;
      valueCents += toCents(order.total);
    } else if (order.status === 'Fulfilled') {
      fulfilled += 1;
      valueCents += toCents(order.total);
    } else if (order.status === 'Cancelled') {
      cancelled += 1;
    }
  }

  return {
    count: orders.length,
    open,
    fulfilled,
    cancelled,
    value: valueCents / 100,
  };
}

function toCents(amount: number): number {
  return Math.round(amount * 100);
}
