import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import { QuestionService } from '../../../core/services/question.service';
import { QuestionDto, QuestionUpsertRequest } from '../../../core/models/question.model';
import { ROOM_TOPICS, RoomTopic } from '../../../core/models/room.model';

const EMPTY_FORM_VALUE = {
  topic: 'Math' as RoomTopic,
  difficulty: 1,
  language: 'es',
  text: '',
  option0: '',
  option1: '',
  option2: '',
  option3: '',
  correctOptionIndex: 0,
  explanation: ''
};

@Component({
  imports: [ReactiveFormsModule, TranslocoPipe],
  selector: 'app-question-form',
  styleUrl: './question-form.scss',
  templateUrl: './question-form.html'
})
export class QuestionForm {
  private readonly fb = inject(FormBuilder);
  private readonly questionService = inject(QuestionService);

  readonly question = input<QuestionDto | null>(null);
  readonly saved = output<QuestionDto>();
  readonly cancelled = output<void>();

  readonly topics = ROOM_TOPICS;
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    topic: [EMPTY_FORM_VALUE.topic, [Validators.required]],
    difficulty: [EMPTY_FORM_VALUE.difficulty, [Validators.required, Validators.min(1), Validators.max(3)]],
    language: [EMPTY_FORM_VALUE.language, [Validators.required]],
    text: [EMPTY_FORM_VALUE.text, [Validators.required, Validators.minLength(5), Validators.maxLength(500)]],
    option0: [EMPTY_FORM_VALUE.option0, [Validators.required, Validators.maxLength(200)]],
    option1: [EMPTY_FORM_VALUE.option1, [Validators.required, Validators.maxLength(200)]],
    option2: [EMPTY_FORM_VALUE.option2, [Validators.required, Validators.maxLength(200)]],
    option3: [EMPTY_FORM_VALUE.option3, [Validators.required, Validators.maxLength(200)]],
    correctOptionIndex: [EMPTY_FORM_VALUE.correctOptionIndex, [Validators.required]],
    explanation: [EMPTY_FORM_VALUE.explanation, [Validators.required, Validators.minLength(5), Validators.maxLength(1000)]]
  });

  constructor() {
    effect(() => {
      const q = this.question();
      this.form.reset(
        q
          ? {
              topic: q.topic,
              difficulty: q.difficulty,
              language: q.language,
              text: q.text,
              option0: q.options[0] ?? '',
              option1: q.options[1] ?? '',
              option2: q.options[2] ?? '',
              option3: q.options[3] ?? '',
              correctOptionIndex: q.correctOptionIndex,
              explanation: q.explanation
            }
          : EMPTY_FORM_VALUE
      );
    });
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.errorMessage.set(null);
    this.submitting.set(true);

    const value = this.form.getRawValue();
    const request: QuestionUpsertRequest = {
      topic: value.topic,
      difficulty: value.difficulty,
      language: value.language,
      text: value.text,
      options: [value.option0, value.option1, value.option2, value.option3],
      correctOptionIndex: value.correctOptionIndex,
      explanation: value.explanation
    };

    const existing = this.question();
    const request$ = existing ? this.questionService.update(existing.id, request) : this.questionService.create(request);

    request$.subscribe({
      next: (saved) => {
        this.submitting.set(false);
        this.saved.emit(saved);
      },
      error: (err: { error?: { title?: string } }) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.error?.title ?? null);
      }
    });
  }

  cancel(): void {
    this.cancelled.emit();
  }
}
