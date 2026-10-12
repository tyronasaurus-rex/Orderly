import { filterOrders, findOrderByExternalReference } from './order-list';
import { Order } from './order.models';

describe('filterOrders', () => {
  const newer = order('newer', 'PO-2000', 'Contoso', 'Confirmed');
  const older = order('older', 'PO-1000', 'Northwind', 'Submitted');

  it('keeps the newest-first sequence from the server', () => {
    const orders = [newer, older];

    expect(filterOrders(orders, '').map((item) => item.id)).toEqual(['newer', 'older']);
    expect(filterOrders(orders, ' po ').map((item) => item.id)).toEqual(['newer', 'older']);
  });

  it('matches reference, customer, or status without reordering', () => {
    const orders = [newer, older];

    expect(filterOrders(orders, 'north').map((item) => item.id)).toEqual(['older']);
    expect(filterOrders(orders, 'confirmed').map((item) => item.id)).toEqual(['newer']);
  });
});

describe('findOrderByExternalReference', () => {
  const stored = order('stored', 'PO-1001', 'Northwind', 'Submitted');

  it('matches the trimmed reference and keeps case distinct', () => {
    expect(findOrderByExternalReference([stored], '  PO-1001  ')?.id).toBe('stored');
    expect(findOrderByExternalReference([stored], 'po-1001')).toBeNull();
  });

  it('returns null when the reference is blank or unused', () => {
    expect(findOrderByExternalReference([stored], '   ')).toBeNull();
    expect(findOrderByExternalReference([stored], 'PO-9')).toBeNull();
  });
});

function order(id: string, externalReference: string, customerName: string, status: string): Order {
  return {
    id,
    externalReference,
    customerName,
    lines: [],
    total: 0,
    status,
    createdAtUtc: '2026-10-12T08:00:00Z',
  };
}
