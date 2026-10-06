import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { FlashArithmeticPanel } from './flash-arithmetic-panel';
import { QuestionRevealedEvent, QuestionStartedEvent } from '../../../../core/models/match.model';

function buildQuestion(text: string, level = 1): QuestionStartedEvent {
  return {
    matchId: 'match-1',
    matchQuestionId: 'q-1',
    index: 0,
    totalQuestions: 5,
    kind: 'flash-arithmetic',
    text,
    options: null,
    endsAtUtc: new Date(Date.now() + 20000).toISOString(),
    level
  };
}

describe('FlashArithmeticPanel', () => {
  let component: FlashArithmeticPanel;
  let fixture: ComponentFixture<FlashArithmeticPanel>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FlashArithmeticPanel],
      providers: [provideTransloco({ config: { availableLangs: ['es', 'en'], defaultLang: 'es' } })]
    }).compileComponents();

    fixture = TestBed.createComponent(FlashArithmeticPanel);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('flashes every number in sequence and then reveals the numeric input', () => {
    vi.useFakeTimers();
    fixture.componentRef.setInput('phase', 'question');
    fixture.componentRef.setInput('question', buildQuestion('5,7,3', 1));
    fixture.detectChanges();

    expect(component.stage()).toBe('flash');
    expect(component.currentNumber()).toBe(5);

    vi.advanceTimersByTime(10000);
    fixture.detectChanges();

    expect(component.stage()).toBe('input');
    expect(component.currentNumber()).toBeNull();
  });

  it('emits the submitted sum once the flash sequence has finished', () => {
    vi.useFakeTimers();
    fixture.componentRef.setInput('phase', 'question');
    fixture.componentRef.setInput('question', buildQuestion('5,7,3', 1));
    fixture.detectChanges();
    vi.advanceTimersByTime(10000);
    fixture.detectChanges();

    let emitted: number | null = null;
    component.answerSubmitted.subscribe((v) => (emitted = v));
    component.answerControl.setValue('15');
    component.submit();

    expect(emitted).toBe(15);
  });

  it('reveal phase shows a checkmark when the submitted answer matched, otherwise the correct sum', () => {
    const reveal: QuestionRevealedEvent = {
      matchId: 'match-1',
      matchQuestionId: 'q-1',
      index: 0,
      kind: 'flash-arithmetic',
      correctOptionIndex: null,
      correctNumericAnswer: 15,
      explanation: '5 + 7 + 3 = 15',
      endsAtUtc: new Date().toISOString(),
      scoreboard: []
    };

    fixture.componentRef.setInput('phase', 'reveal');
    fixture.componentRef.setInput('question', buildQuestion('5,7,3', 1));
    fixture.componentRef.setInput('reveal', reveal);
    fixture.componentRef.setInput('mySubmittedAnswer', 15);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('✓');

    fixture.componentRef.setInput('mySubmittedAnswer', 9);
    fixture.detectChanges();

    const wrongText = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(wrongText).toContain('15');
    expect(wrongText).not.toContain('✓');
  });
});
