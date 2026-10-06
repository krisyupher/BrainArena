import { Component, OnDestroy, effect, input, output, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { QuestionRevealedEvent, QuestionStartedEvent } from '../../../../core/models/match.model';
import { ErrorBanner } from '../../../../shared/error-banner/error-banner';

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

  /** 'flash' = a number is showing, 'gap' = black screen between numbers, 'input' = all shown, answer now. */
  readonly stage = signal<Stage>('flash');
  readonly currentNumber = signal<number | null>(null);

  private numbers: number[] = [];
  private timers: ReturnType<typeof setTimeout>[] = [];

  constructor() {
    // A fresh question arrives as a new object reference each time — reset local flash state and
    // (for a question-phase instance only; a reveal-phase instance never flashes anything) kick
    // off the flash sequence. Client-side validation is UX only; the server remains the sole
    // authority on correctness.
    effect(() => {
      const q = this.question();
      this.clearTimers();
      this.answerControl.reset('');
      this.numbers = q.text.split(',').map(Number);
      this.stage.set('flash');
      this.currentNumber.set(null);

      if (this.phase() === 'question') {
        this.runFlashSequence(q.level ?? 1);
      }
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

  private runFlashSequence(level: number): void {
    // Frontend-only speed-up as Level rises — the number count itself is capped server-side, so a
    // long streak can never eat the whole answer window before the player gets to respond.
    const flashMs = Math.max(350, 900 - level * 30);
    const gapMs = Math.max(150, Math.round(flashMs * 0.35));

    let i = 0;
    const showNext = (): void => {
      if (i >= this.numbers.length) {
        this.stage.set('input');
        this.currentNumber.set(null);
        return;
      }

      this.stage.set('flash');
      this.currentNumber.set(this.numbers[i]);
      i++;
      this.timers.push(
        setTimeout(() => {
          this.stage.set('gap');
          this.currentNumber.set(null);
          this.timers.push(setTimeout(showNext, gapMs));
        }, flashMs)
      );
    };

    showNext();
  }

  ngOnDestroy(): void {
    this.clearTimers();
  }

  private clearTimers(): void {
    this.timers.forEach((t) => clearTimeout(t));
    this.timers = [];
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
