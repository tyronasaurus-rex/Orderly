import { buildCreateOrderBody } from './create-order';

describe('buildCreateOrderBody', () => {
  it('trims text, keeps line order, and omits client totals', () => {
    const binder = {
      description: '  Binder  ',
      quantity: '2',
      unitPrice: 3.5,
      lineTotal: 999,
    };
    const pen = {
      description: ' Pen ',
      quantity: 1,
      unitPrice: 0,
      lineTotal: 1,
    };

    const body = buildCreateOrderBody({
      externalReference: '  PO-1001  ',
      customerName: '  Northwind  ',
      lines: [binder, pen],
    });

    expect(body).toEqual({
      externalReference: 'PO-1001',
      customerName: 'Northwind',
      lines: [
        { description: 'Binder', quantity: 2, unitPrice: 3.5 },
        { description: 'Pen', quantity: 1, unitPrice: 0 },
      ],
    });
    expect(Object.keys(body)).toEqual(['externalReference', 'customerName', 'lines']);
    expect(Object.keys(body.lines[0])).toEqual(['description', 'quantity', 'unitPrice']);
    expect(Object.keys(body.lines[1])).toEqual(['description', 'quantity', 'unitPrice']);
  });
});
