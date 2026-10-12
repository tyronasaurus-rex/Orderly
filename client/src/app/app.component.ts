import { Component } from '@angular/core';
import { OrdersComponent } from './orders/orders.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [OrdersComponent],
  template: '<app-orders />',
})
export class AppComponent {}
