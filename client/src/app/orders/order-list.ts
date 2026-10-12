import { Order } from './order.models';

export function filterOrders(orders: readonly Order[], query: string): readonly Order[] {
  const needle = query.trim().toLocaleLowerCase();
  if (needle.length === 0) {
    return orders;
  }

  return orders.filter((order) =>
    [order.externalReference, order.customerName, order.status].some((value) =>
      value.toLocaleLowerCase().includes(needle),
    ),
  );
}
