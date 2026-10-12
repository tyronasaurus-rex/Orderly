import { allowedStatusChanges } from './order-status';

describe('allowedStatusChanges', () => {
  it('follows the sales flow and stops at terminal statuses', () => {
    expect(allowedStatusChanges('Submitted')).toEqual(['Confirmed', 'Cancelled']);
    expect(allowedStatusChanges('Confirmed')).toEqual(['Fulfilled', 'Cancelled']);
    expect(allowedStatusChanges('Fulfilled')).toEqual([]);
    expect(allowedStatusChanges('Cancelled')).toEqual([]);
    expect(allowedStatusChanges('Shipped')).toEqual([]);
  });
});
