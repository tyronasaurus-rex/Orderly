import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { environment } from '../environments/environment';
import { AppComponent } from './app.component';
import { routes } from './app.routes';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter(routes)],
    }).compileComponents();
  });

  it('shows a welcome splash', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    await TestBed.inject(Router).navigateByUrl('/');
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Welcome to Orderly');
    expect(text).toContain('Continue');
  });

  it('continues to the purchase order screen', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    await TestBed.inject(Router).navigateByUrl('/');
    fixture.detectChanges();

    const continueLink = (fixture.nativeElement as HTMLElement).querySelector(
      'a.splash-continue',
    ) as HTMLAnchorElement;
    continueLink.click();
    await fixture.whenStable();
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/api/orders`).flush([]);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Create order');
    expect(text).toContain('Orders');
    expect(text).toContain('Open');
    expect(text).toContain('Fulfilled');
    expect(text).toContain('Newest first');
    http.verify();
  });
});
