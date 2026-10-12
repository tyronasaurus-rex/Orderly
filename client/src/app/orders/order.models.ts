export const OrderFieldLimits = {
  externalReference: 64,
  customerName: 200,
  description: 200,
} as const;

export const orderStatuses = ['Submitted', 'Confirmed', 'Fulfilled', 'Cancelled'] as const;

export type OrderStatus = (typeof orderStatuses)[number];

export interface OrderLine {
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: string;
  externalReference: string;
  customerName: string;
  lines: OrderLine[];
  total: number;
  status: string;
  createdAtUtc: string;
}

export interface CreateOrderLine {
  description: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateOrderRequest {
  externalReference: string;
  customerName: string;
  lines: CreateOrderLine[];
}

export function isOrderStatus(value: string): value is OrderStatus {
  return (orderStatuses as readonly string[]).includes(value);
}
