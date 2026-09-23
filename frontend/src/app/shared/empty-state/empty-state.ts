import { Component, input } from '@angular/core';

/** Icon + message panel for an empty list, with an optional projected action (e.g. a button). */
@Component({
  selector: 'app-empty-state',
  styleUrl: './empty-state.scss',
  templateUrl: './empty-state.html'
})
export class EmptyState {
  readonly message = input.required<string>();
}
