import { Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { QuestionRevealedEvent, QuestionStartedEvent } from '../../../../core/models/match.model';
import { ErrorBanner } from '../../../../shared/error-banner/error-banner';

@Component({
  imports: [TranslocoPipe, ErrorBanner],
  selector: 'app-multiple-choice-panel',
  styleUrl: './multiple-choice-panel.scss',
  templateUrl: './multiple-choice-panel.html'
})
export class MultipleChoicePanel {
  readonly phase = input.required<'question' | 'reveal'>();
  readonly question = input.required<QuestionStartedEvent>();
  /** Only set once the question has closed. */
  readonly reveal = input<QuestionRevealedEvent | null>(null);
  readonly selectedOption = input<number | null>(null);
  readonly answerLocked = input(false);
  /** False for a spectator — options render read-only instead of as clickable buttons. */
  readonly canAnswer = input(true);
  readonly errorMessage = input<string | null>(null);

  readonly answerSelected = output<number>();

  select(index: number): void {
    if (!this.canAnswer() || this.answerLocked() || this.phase() !== 'question') {
      return;
    }
    this.answerSelected.emit(index);
  }

  isCorrectOption(index: number): boolean {
    return this.reveal()?.correctOptionIndex === index;
  }
}
