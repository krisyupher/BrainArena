import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { FlashArithmeticPanel } from './flash-arithmetic-panel';
import { FlashNumberEvent, QuestionRevealedEvent, QuestionStartedEvent } from '../../../../core/models/match.model';

function buildQuestion(level = 1): QuestionStartedEvent {
  return {
    matchId: 'match-1',
    matchQuestionId: 'q-1',
    index: 0,
    totalQuestions: 5,
    kind: 'flash-arithmetic',
    text: '',
    options: null,
    endsAtUtc: new Date(Date.now() + 20000).toISOString(),
    level
  };
}

function flash(position: number, value: number, visibleMs = 800): FlashNumberEvent {
  return { matchQuestionId: 'q-1', position, count: 3, value, visibleMs };
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
    fixture.componentRef.setInput('phase', 'question');
    fixture.componentRef.setInput('question', buildQuestion());
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows each server-sent number, then blanks to the black screen after its display time', () => {
    vi.useFakeTimers();
    fixture.detectChanges();
    expect(component.stage()).toBe('gap');

    fixture.componentRef.setInput('flashNumber', flash(0, 7, 800));
    fixture.detectChanges();
    expect(component.stage()).toBe('flash');
    expect(component.currentNumber()).toBe(7);

    vi.advanceTimersByTime(800);
    fixture.detectChanges();
    expect(component.stage()).toBe('gap');
    expect(component.currentNumber()).toBeNull();

    fixture.componentRef.setInput('flashNumber', flash(1, 12));
    fixture.detectChanges();
    expect(component.currentNumber()).toBe(12);
  });

  it('lets the last number finish its display time even if the answer window opens meanwhile', () => {
    vi.useFakeTimers();
    fixture.componentRef.setInput('flashNumber', flash(2, 69, 800));
    fixture.componentRef.setInput('answerWindowOpen', true);
    fixture.detectChanges();
    expect(component.stage()).toBe('flash');
    expect(component.currentNumber()).toBe(69);

    vi.advanceTimersByTime(800);
    fixture.detectChanges();
    expect(component.stage()).toBe('input');
  });

  it('keeps the answer input closed until the server opens the answer window', () => {
    vi.useFakeTimers();
    fixture.componentRef.setInput('flashNumber', flash(2, 3));
    fixture.detectChanges();
    vi.advanceTimersByTime(800);
    fixture.detectChanges();

    let emitted: number | null = null;
    component.answerSubmitted.subscribe((v) => (emitted = v));
    component.answerControl.setValue('22');
    component.submit();
    expect(emitted).toBeNull();
    expect(component.answerControl.disabled).toBe(true);

    fixture.componentRef.setInput('answerWindowOpen', true);
    fixture.detectChanges();
    expect(component.stage()).toBe('input');
    expect(component.answerControl.enabled).toBe(true);

    component.answerControl.setValue('22');
    component.submit();
    expect(emitted).toBe(22);
  });

  it('submits through the form element without letting the browser do a native, page-reloading submit', () => {
    fixture.componentRef.setInput('answerWindowOpen', true);
    fixture.detectChanges();

    let emitted: number | null = null;
    component.answerSubmitted.subscribe((v) => (emitted = v));

    const host = fixture.nativeElement as HTMLElement;
    const input = host.querySelector('.numeric-form input') as HTMLInputElement;
    input.value = '22';
    input.dispatchEvent(new Event('input'));

    const submit = new Event('submit', { cancelable: true });
    host.querySelector('.numeric-form')!.dispatchEvent(submit);

    expect(submit.defaultPrevented).toBe(true);
    expect(emitted).toBe(22);
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
