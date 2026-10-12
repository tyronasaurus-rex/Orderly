import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-splash',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './splash.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SplashComponent {}
