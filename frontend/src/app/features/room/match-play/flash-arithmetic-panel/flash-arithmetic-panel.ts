import { Component, OnDestroy, computed, effect, input, output, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { FlashNumberEvent, QuestionRevealedEvent, QuestionStartedEvent } from '../../../../core/models/match.model';
import { ErrorBanner } from '../../../../shared/error-banner/error-banner';

/** 'flash' = a number is showing, 'gap' = black screen between numbers, 'input' = answering is open. */
type Stage = 'flash' | 'gap' | 'input';

@Component({
  imports: [ReactiveFormsModule, TranslocoPipe, ErrorBanner],
  selector: 'app-flash-arithmetic-panel',
  styleUrl: './flash-arithmetic-panel.scss',
  templateUrl: './flash-arithmetic-panel.html'
})
export class FlashArithmeticPanel implements OnDestroy {
  readonly phase = input.required<'question' | 'reveal'>();
  readonly question = input.required<QuestionStartedEvent>();
  /** Only set once the question has closed. */
  readonly reveal = input<QuestionRevealedEvent | null>(null);
  /** Server-paced: each number arrives at its own display moment and is never known in advance. */
  readonly flashNumber = input<FlashNumberEvent | null>(null);
  /** Opens only after the server has shown the last number. */
  readonly answerWindowOpen = input(false);
  readonly answerLocked = input(false);
  /** False for a spectator — the input is hidden entirely. */
  readonly canAnswer = input(true);
  readonly errorMessage = input<string | null>(null);
  /**
   * HUD state — owned by MatchPlay, not this component. A fresh instance of this panel is
   * created every time the parent switches between its question-panel and reveal-panel @if
   * blocks (structural directives destroy/recreate), so Level/Streak/BestStreak/Accuracy would
   * silently reset to zero on every single reveal if they lived here instead.
   */
  readonly streak = input(0);
  readonly bestStreak = input(0);
  readonly accuracy = input(0);
  /** The player's own last submitted sum, for the reveal panel's correct/wrong indicator. */
  readonly mySubmittedAnswer = input<number | null>(null);

  readonly answerSubmitted = output<number>();

  readonly answerControl = new FormControl('', { nonNullable: true, validators: [Validators.required] });
  // (ngSubmit) only exists under a FormGroupDirective — without one, the native submit reloads the page.
  readonly answerForm = new FormGroup({ answer: this.answerControl });

  readonly currentNumber = signal<number | null>(null);
  // A number still on screen wins over an already-open answer window: on a lagging device the last
  // number and AnswerWindowOpened can land in the same render, and the player must still see it.
  readonly stage = computed<Stage>(() =>
    this.currentNumber() !== null ? 'flash' : this.answerWindowOpen() ? 'input' : 'gap'
  );

  private hideTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    // A fresh question arrives as a new object reference each time — clear any leftover input.
    effect(() => {
      this.question();
      this.answerControl.reset('');
    });

    // Show each number for its full display time from when it arrives, then blank to the black screen.
    effect(() => {
      const flash = this.flashNumber();
      clearTimeout(this.hideTimer);
      if (!flash) {
        this.currentNumber.set(null);
        return;
      }
      this.currentNumber.set(flash.value);
      this.hideTimer = setTimeout(() => this.currentNumber.set(null), flash.visibleMs);
    });

    // Disabled state is driven through the FormControl itself, not a template [disabled]
    // binding — Angular warns (and can desync) when both are used on the same formControl element.
    effect(() => {
      const shouldDisable =
        this.answerLocked() || this.phase() !== 'question' || this.stage() !== 'input' || !this.canAnswer();
      if (shouldDisable) {
        this.answerControl.disable();
      } else {
        this.answerControl.enable();
      }
    });
  }

  ngOnDestroy(): void {
    clearTimeout(this.hideTimer);
  }

  submit(): void {
    if (!this.canAnswer() || this.answerLocked() || this.phase() !== 'question' || this.stage() !== 'input' || this.answerControl.invalid) {
      return;
    }

    const parsed = Number(this.answerControl.value);
    if (Number.isNaN(parsed)) {
      return;
    }

    this.answerSubmitted.emit(parsed);
  }
}
