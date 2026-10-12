import { isOrderStatus, OrderStatus } from './order.models';

const nextStatus: Record<OrderStatus, readonly OrderStatus[]> = {
  Submitted: ['Confirmed', 'Cancelled'],
  Confirmed: ['Fulfilled', 'Cancelled'],
  Fulfilled: [],
  Cancelled: [],
};

export function allowedStatusChanges(status: string): OrderStatus[] {
  if (!isOrderStatus(status)) {
    return [];
  }

  return [...nextStatus[status]];
}

export function statusActionLabel(status: OrderStatus): string {
  switch (status) {
    case 'Confirmed':
      return 'Confirm';
    case 'Fulfilled':
      return 'Fulfill';
    case 'Cancelled':
      return 'Cancel';
    case 'Submitted':
      return 'Submitted';
  }
}
