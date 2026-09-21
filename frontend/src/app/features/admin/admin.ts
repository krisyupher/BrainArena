import { Component, OnInit, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { QuestionService } from '../../core/services/question.service';
import { QuestionDto, QuestionImportResult, QuestionUpsertRequest } from '../../core/models/question.model';
import { ROOM_TOPICS, RoomTopic } from '../../core/models/room.model';
import { QuestionForm } from './question-form/question-form';

@Component({
  imports: [TranslocoPipe, QuestionForm],
  selector: 'app-admin',
  styleUrl: './admin.scss',
  templateUrl: './admin.html'
})
export class Admin implements OnInit {
  private readonly questionService = inject(QuestionService);

  readonly topics = ROOM_TOPICS;
  readonly topicFilter = signal<RoomTopic | null>(null);
  readonly questions = signal<QuestionDto[]>([]);
  readonly loading = signal(true);
  readonly showForm = signal(false);
  readonly editingQuestion = signal<QuestionDto | null>(null);
  readonly importResult = signal<QuestionImportResult | null>(null);
  readonly importError = signal<string | null>(null);
  readonly importing = signal(false);

  ngOnInit(): void {
    this.loadQuestions();
  }

  setFilter(topic: RoomTopic | null): void {
    this.topicFilter.set(topic);
    this.loadQuestions();
  }

  openCreateForm(): void {
    this.editingQuestion.set(null);
    this.showForm.set(true);
  }

  openEditForm(question: QuestionDto): void {
    this.editingQuestion.set(question);
    this.showForm.set(true);
  }

  onSaved(): void {
    this.showForm.set(false);
    this.loadQuestions();
  }

  onCancelled(): void {
    this.showForm.set(false);
  }

  async onFileSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.importError.set(null);
    this.importResult.set(null);
    this.importing.set(true);

    try {
      const text = await file.text();
      const parsed = JSON.parse(text);
      if (!Array.isArray(parsed)) {
        throw new Error('not-an-array');
      }

      this.questionService.import(parsed as QuestionUpsertRequest[]).subscribe({
        next: (result) => {
          this.importing.set(false);
          this.importResult.set(result);
          this.loadQuestions();
        },
        error: (err: { error?: { title?: string } }) => {
          this.importing.set(false);
          this.importError.set(err?.error?.title ?? null);
        }
      });
    } catch {
      this.importing.set(false);
      this.importError.set('invalid-json');
    } finally {
      input.value = '';
    }
  }

  private loadQuestions(): void {
    this.loading.set(true);
    this.questionService.getAll(this.topicFilter() ?? undefined).subscribe({
      next: (questions) => {
        this.questions.set(questions);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
