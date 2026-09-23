import { Component, effect, input, output } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { QuestionRevealedEvent, QuestionStartedEvent } from '../../../../core/models/match.model';
import { ErrorBanner } from '../../../../shared/error-banner/error-banner';

@Component({
  imports: [ReactiveFormsModule, TranslocoPipe, ErrorBanner],
  selector: 'app-calculation-panel',
  styleUrl: './calculation-panel.scss',
  templateUrl: './calculation-panel.html'
})
export class CalculationPanel {
  readonly phase = input.required<'question' | 'reveal'>();
  readonly question = input.required<QuestionStartedEvent>();
  /** Only set once the question has closed. */
  readonly reveal = input<QuestionRevealedEvent | null>(null);
  readonly answerLocked = input(false);
  /** False for a spectator — the input is hidden entirely. */
  readonly canAnswer = input(true);
  readonly errorMessage = input<string | null>(null);

  readonly answerSubmitted = output<number>();

  readonly answerControl = new FormControl('', { nonNullable: true, validators: [Validators.required] });

  constructor() {
    // A fresh question arrives as a new object reference each time — clear any leftover input.
    // Client-side validation is UX only; the server remains the sole authority on correctness.
    effect(() => {
      this.question();
      this.answerControl.reset('');
    });

    // Disabled state is driven through the FormControl itself, not a template [disabled]
    // binding — Angular warns (and can desync) when both are used on the same formControl element.
    effect(() => {
      const shouldDisable = this.answerLocked() || this.phase() !== 'question' || !this.canAnswer();
      if (shouldDisable) {
        this.answerControl.disable();
      } else {
        this.answerControl.enable();
      }
    });
  }

  submit(): void {
    if (!this.canAnswer() || this.answerLocked() || this.phase() !== 'question' || this.answerControl.invalid) {
      return;
    }

    const parsed = Number(this.answerControl.value);
    if (Number.isNaN(parsed)) {
      return;
    }

    this.answerSubmitted.emit(parsed);
  }
}
