import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { QuestionForm } from './question-form';

describe('QuestionForm', () => {
  let component: QuestionForm;
  let fixture: ComponentFixture<QuestionForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QuestionForm],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({ config: { availableLangs: ['es', 'en'], defaultLang: 'es' } })
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(QuestionForm);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('starts invalid because the question text and options are required', () => {
    expect(component.form.invalid).toBe(true);
  });

  it('pre-fills the form when editing an existing question', () => {
    fixture.componentRef.setInput('question', {
      id: 'q1',
      topic: 'Math',
      difficulty: 2,
      text: 'What is 1 + 1?',
      options: ['1', '2', '3', '4'],
      correctOptionIndex: 1,
      explanation: '1 + 1 = 2',
      language: 'en'
    });
    fixture.detectChanges();

    expect(component.form.controls.text.value).toBe('What is 1 + 1?');
    expect(component.form.controls.option1.value).toBe('2');
  });
});
