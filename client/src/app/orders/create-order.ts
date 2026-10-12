import { CreateOrderRequest } from './order.models';

export interface CreateOrderLineInput {
  description: string;
  quantity: number | string;
  unitPrice: number | string;
}

export interface CreateOrderInput {
  externalReference: string;
  customerName: string;
  lines: readonly CreateOrderLineInput[];
}

export function buildCreateOrderBody(input: CreateOrderInput): CreateOrderRequest {
  return {
    externalReference: input.externalReference.trim(),
    customerName: input.customerName.trim(),
    lines: input.lines.map((line) => ({
      description: line.description.trim(),
      quantity: Number(line.quantity),
      unitPrice: Number(line.unitPrice),
    })),
  };
}
