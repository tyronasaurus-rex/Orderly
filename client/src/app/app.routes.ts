import { Routes } from '@angular/router';
import { OrdersComponent } from './orders/orders.component';
import { SplashComponent } from './splash/splash.component';

export const routes: Routes = [
  { path: '', component: SplashComponent, title: 'Welcome to Orderly' },
  { path: 'orders', component: OrdersComponent, title: 'Orderly' },
];
