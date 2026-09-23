import { Component, input, output } from '@angular/core';

/** Consistent error affordance — an icon + message, with an optional dismiss control. */
@Component({
  selector: 'app-error-banner',
  styleUrl: './error-banner.scss',
  templateUrl: './error-banner.html'
})
export class ErrorBanner {
  readonly message = input.required<string>();
  readonly dismissible = input(false);
  readonly dismissed = output<void>();
}
