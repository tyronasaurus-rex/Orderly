import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export function requiredTrimmed(control: AbstractControl): ValidationErrors | null {
  const value: unknown = control.value;
  if (typeof value !== 'string' || value.trim().length === 0) {
    return { required: true };
  }

  return null;
}

export function maxTrimmedLength(max: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (typeof control.value !== 'string') {
      return null;
    }

    const length = control.value.trim().length;
    if (length > max) {
      return { maxlength: { requiredLength: max, actualLength: length } };
    }

    return null;
  };
}

export function wholeQuantity(control: AbstractControl): ValidationErrors | null {
  const quantity = asNumber(control.value);
  if (quantity === null || !Number.isInteger(quantity) || quantity < 1) {
    return { quantity: true };
  }

  return null;
}

export function nonNegativePrice(control: AbstractControl): ValidationErrors | null {
  const price = asNumber(control.value);
  if (price === null || price < 0) {
    return { unitPrice: true };
  }

  return null;
}

function asNumber(value: unknown): number | null {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : null;
  }

  if (typeof value === 'string' && value.trim().length > 0) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}
