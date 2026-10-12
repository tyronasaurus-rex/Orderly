import { DecimalPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { buildCreateOrderBody } from './create-order';
import { filterOrders, findOrderByExternalReference } from './order-list';
import { OrderFieldLimits, Order, OrderStatus } from './order.models';
import { summarizeOrders } from './order-summary';
import { maxTrimmedLength, nonNegativePrice, requiredTrimmed, wholeQuantity } from './order-field.validators';
import { OrderService, readOrderProblem } from './order.service';
import { allowedStatusChanges, statusActionLabel } from './order-status';

interface LineFormControls {
  description: FormControl<string>;
  quantity: FormControl<number | null>;
  unitPrice: FormControl<number | null>;
}

interface OrderFormControls {
  externalReference: FormControl<string>;
  customerName: FormControl<string>;
  lines: FormArray<FormGroup<LineFormControls>>;
}

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [ReactiveFormsModule, DecimalPipe, DatePipe],
  templateUrl: './orders.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrdersComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly ordersService = inject(OrderService);
  private readonly destroyRef = inject(DestroyRef);

  readonly limits = OrderFieldLimits;
  readonly statusActionLabel = statusActionLabel;

  readonly orders = signal<Order[]>([]);
  readonly selected = signal<Order | null>(null);
  readonly filterQuery = signal('');
  readonly creating = signal(false);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly checkingReference = signal(false);
  readonly existingOrder = signal<Order | null>(null);
  readonly referenceAvailable = signal(false);
  readonly statusUpdating = signal(false);
  readonly notice = signal<string | null>(null);
  readonly formError = signal<string | null>(null);
  readonly listError = signal<string | null>(null);
  readonly statusError = signal<string | null>(null);

  readonly visibleOrders = computed(() => filterOrders(this.orders(), this.filterQuery()));
  readonly stats = computed(() => summarizeOrders(this.orders()));
  readonly allowedChanges = computed(() => {
    const order = this.selected();
    return order ? allowedStatusChanges(order.status) : [];
  });

  private referenceLookup = 0;
  private checkedReference = '';
  private referenceCheckTimer: ReturnType<typeof setTimeout> | undefined;

  readonly form: FormGroup<OrderFormControls> = this.formBuilder.group({
    externalReference: this.formBuilder.nonNullable.control('', {
      validators: [requiredTrimmed, maxTrimmedLength(OrderFieldLimits.externalReference)],
    }),
    customerName: this.formBuilder.nonNullable.control('', {
      validators: [requiredTrimmed, maxTrimmedLength(OrderFieldLimits.customerName)],
    }),
    lines: this.formBuilder.array<FormGroup<LineFormControls>>([this.createLine()]),
  });

  ngOnInit(): void {
    this.loadOrders();
    this.destroyRef.onDestroy(() => this.clearReferenceTimer());
    this.form.controls.externalReference.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.clearReferenceLookup());
  }

  get lines(): FormArray<FormGroup<LineFormControls>> {
    return this.form.controls.lines;
  }

  get lineGroups(): FormGroup<LineFormControls>[] {
    return this.lines.controls;
  }

  addLine(): void {
    this.lines.push(this.createLine());
  }

  removeLine(index: number): void {
    if (this.lines.length === 1) {
      return;
    }

    this.lines.removeAt(index);
  }

  onFilterInput(event: Event): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement)) {
      return;
    }

    this.filterQuery.set(target.value);
  }

  select(order: Order): void {
    this.selected.set(order);
    this.statusError.set(null);
  }

  openCreate(): void {
    this.formError.set(null);
    this.clearReferenceLookup();
    this.creating.set(true);
  }

  closeCreate(): void {
    if (this.submitting()) {
      return;
    }

    this.clearReferenceLookup();
    this.creating.set(false);
  }

  onReferenceKeyup(): void {
    if (this.submitting()) {
      return;
    }

    const reference = this.form.controls.externalReference.value.trim();
    if (reference.length === 0 || this.form.controls.externalReference.invalid || reference === this.checkedReference) {
      this.clearReferenceTimer();
      return;
    }

    this.clearReferenceTimer();
    this.referenceCheckTimer = setTimeout(() => {
      this.referenceCheckTimer = undefined;
      this.checkedReference = reference;
      this.checkReference();
    }, 300);
  }

  checkReference(): void {
    const control = this.form.controls.externalReference;
    this.formError.set(null);
    if (control.invalid || this.submitting()) {
      return;
    }

    const reference = control.value.trim();
    const lookup = ++this.referenceLookup;
    this.existingOrder.set(null);
    this.referenceAvailable.set(false);
    this.checkingReference.set(true);
    this.ordersService
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (orders) => {
          this.applyLoadedOrders(orders);
          if (lookup !== this.referenceLookup || !this.creating()) {
            return;
          }

          this.checkingReference.set(false);
          const match = findOrderByExternalReference(orders, reference);
          if (match) {
            this.existingOrder.set(match);
            return;
          }

          this.referenceAvailable.set(true);
        },
        error: (error: unknown) => {
          if (lookup !== this.referenceLookup || !this.creating()) {
            return;
          }

          this.checkingReference.set(false);
          this.formError.set(readOrderProblem(error).message);
        },
      });
  }

  selectExistingOrder(): void {
    const order = this.existingOrder();
    if (!order || this.submitting()) {
      return;
    }

    if (!this.visibleOrders().some((item) => item.id === order.id)) {
      this.filterQuery.set('');
    }

    this.select(order);
    this.closeCreate();
  }

  submit(): void {
    if (this.submitting()) {
      return;
    }

    this.formError.set(null);
    this.notice.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const body = buildCreateOrderBody({
      externalReference: raw.externalReference,
      customerName: raw.customerName,
      lines: raw.lines.map((line) => ({
        description: line.description,
        quantity: line.quantity ?? Number.NaN,
        unitPrice: line.unitPrice ?? Number.NaN,
      })),
    });

    this.submitting.set(true);
    this.ordersService
      .create(body)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.submitting.set(false);
          const order = response.body;
          if (!order) {
            this.formError.set('The order service returned an empty response.');
            return;
          }

          this.notice.set(
            response.status === 200
              ? 'That reference is already on file with the same details. Showing the stored order.'
              : 'Order submitted. Check the server total and status.',
          );
          this.resetForm();
          this.creating.set(false);
          this.select(order);
          this.loadOrders();
        },
        error: (error: unknown) => {
          this.submitting.set(false);
          const problem = readOrderProblem(error);
          this.formError.set(problem.message);
          this.loadOrders(() => {
            if (problem.code !== 'duplicate_reference') {
              return;
            }

            const existing = this.orders().find((order) => order.externalReference === body.externalReference);
            if (existing) {
              this.select(existing);
            }
          });
        },
      });
  }

  changeStatus(status: OrderStatus): void {
    const current = this.selected();
    if (!current || this.statusUpdating()) {
      return;
    }

    this.statusUpdating.set(true);
    this.statusError.set(null);
    this.ordersService
      .changeStatus(current.id, status)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (order) => {
          this.statusUpdating.set(false);
          this.selected.set(order);
          this.loadOrders();
        },
        error: (error: unknown) => {
          this.statusUpdating.set(false);
          this.statusError.set(readOrderProblem(error).message);
        },
      });
  }

  loadOrders(afterLoad?: () => void): void {
    this.ordersService
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (orders) => {
          this.applyLoadedOrders(orders);
          afterLoad?.();
        },
        error: (error: unknown) => {
          this.loading.set(false);
          this.listError.set(readOrderProblem(error).message);
        },
      });
  }

  private applyLoadedOrders(orders: Order[]): void {
    this.orders.set(orders);
    this.listError.set(null);
    this.loading.set(false);
    const current = this.selected();
    if (!current) {
      return;
    }

    const refreshed = orders.find((order) => order.id === current.id);
    if (refreshed) {
      this.selected.set(refreshed);
    }
  }

  private clearReferenceTimer(): void {
    if (this.referenceCheckTimer === undefined) {
      return;
    }

    clearTimeout(this.referenceCheckTimer);
    this.referenceCheckTimer = undefined;
  }

  private clearReferenceLookup(): void {
    this.clearReferenceTimer();
    this.referenceLookup += 1;
    this.checkedReference = '';
    this.checkingReference.set(false);
    this.existingOrder.set(null);
    this.referenceAvailable.set(false);
  }

  private createLine(): FormGroup<LineFormControls> {
    return this.formBuilder.group({
      description: this.formBuilder.nonNullable.control('', {
        validators: [requiredTrimmed, maxTrimmedLength(OrderFieldLimits.description)],
      }),
      quantity: this.formBuilder.control<number | null>(null, { validators: [wholeQuantity] }),
      unitPrice: this.formBuilder.control<number | null>(null, { validators: [nonNegativePrice] }),
    });
  }

  private resetForm(): void {
    this.form.controls.externalReference.setValue('');
    this.form.controls.customerName.setValue('');
    this.lines.clear();
    this.lines.push(this.createLine());
    this.form.markAsUntouched();
    this.form.markAsPristine();
  }
}
