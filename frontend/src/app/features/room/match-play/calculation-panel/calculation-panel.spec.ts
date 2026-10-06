import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { CalculationPanel } from './calculation-panel';
import { QuestionStartedEvent } from '../../../../core/models/match.model';

const question: QuestionStartedEvent = {
  matchId: 'match-1',
  matchQuestionId: 'q-1',
  index: 0,
  totalQuestions: 5,
  kind: 'calculation',
  text: '20 + 22 = ?',
  options: null,
  endsAtUtc: new Date(Date.now() + 20000).toISOString(),
  level: null
};

describe('CalculationPanel', () => {
  let component: CalculationPanel;
  let fixture: ComponentFixture<CalculationPanel>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CalculationPanel],
      providers: [provideTransloco({ config: { availableLangs: ['es', 'en'], defaultLang: 'es' } })]
    }).compileComponents();

    fixture = TestBed.createComponent(CalculationPanel);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('phase', 'question');
    fixture.componentRef.setInput('question', question);
    fixture.detectChanges();
  });

  it('submits through the form element without letting the browser do a native, page-reloading submit', () => {
    let emitted: number | null = null;
    component.answerSubmitted.subscribe((v) => (emitted = v));

    const host = fixture.nativeElement as HTMLElement;
    const input = host.querySelector('.numeric-form input') as HTMLInputElement;
    input.value = '42';
    input.dispatchEvent(new Event('input'));

    const submit = new Event('submit', { cancelable: true });
    host.querySelector('.numeric-form')!.dispatchEvent(submit);

    expect(submit.defaultPrevented).toBe(true);
    expect(emitted).toBe(42);
  });

  it('does not emit once the answer is locked', () => {
    fixture.componentRef.setInput('answerLocked', true);
    fixture.detectChanges();

    let emitted: number | null = null;
    component.answerSubmitted.subscribe((v) => (emitted = v));
    component.answerControl.setValue('42');
    component.submit();

    expect(emitted).toBeNull();
  });
});
