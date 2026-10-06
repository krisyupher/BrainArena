import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { Subject } from 'rxjs';
import { MatchPlay } from './match-play';
import { RoomHubService } from '../../../core/services/room-hub.service';
import {
  AnswerWindowOpenedEvent,
  FlashNumberEvent,
  QuestionRevealedEvent,
  QuestionStartedEvent
} from '../../../core/models/match.model';

const flashQuestion = (index: number, level: number): QuestionStartedEvent => ({
  matchId: 'm',
  matchQuestionId: `q-${index}`,
  index,
  totalQuestions: 5,
  kind: 'flash-arithmetic',
  text: '',
  options: null,
  endsAtUtc: new Date(Date.now() + 20000).toISOString(),
  level
});

const flashReveal = (index: number, correct: number): QuestionRevealedEvent => ({
  matchId: 'm',
  matchQuestionId: `q-${index}`,
  index,
  kind: 'flash-arithmetic',
  correctOptionIndex: null,
  correctNumericAnswer: correct,
  explanation: '',
  endsAtUtc: new Date().toISOString(),
  scoreboard: []
});

const windowOpened = (index: number): AnswerWindowOpenedEvent => ({
  matchQuestionId: `q-${index}`,
  endsAtUtc: new Date(Date.now() + 10000).toISOString()
});

describe('MatchPlay', () => {
  let component: MatchPlay;
  let fixture: ComponentFixture<MatchPlay>;
  let hub: {
    questionStarted: Subject<QuestionStartedEvent>;
    flashNumber: Subject<FlashNumberEvent>;
    answerWindowOpened: Subject<AnswerWindowOpenedEvent>;
    questionRevealed: Subject<QuestionRevealedEvent>;
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MatchPlay],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({ config: { availableLangs: ['es', 'en'], defaultLang: 'es' } }),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: 'room-1' }) } }
        },
        {
          provide: RoomHubService,
          useValue: {
            matchStarting: new Subject(),
            questionStarted: new Subject(),
            flashNumber: new Subject(),
            answerWindowOpened: new Subject(),
            answerAccepted: new Subject(),
            questionRevealed: new Subject(),
            matchEnded: new Subject(),
            matchResync: new Subject(),
            matchSpectatorSync: new Subject(),
            chatMessageReceived: new Subject(),
            chatMessageReported: new Subject(),
            reactionSent: new Subject(),
            joinRoomGroup: () => Promise.resolve(),
            joinAsSpectator: () => Promise.resolve(),
            submitNumericAnswer: () => Promise.resolve()
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MatchPlay);
    component = fixture.componentInstance;
    hub = TestBed.inject(RoomHubService) as unknown as typeof hub;
    await fixture.whenStable();
    component.isParticipant.set(true);
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('ignores a flash-arithmetic answer until the server opens the answer window', () => {
    hub.questionStarted.next(flashQuestion(0, 3));
    hub.flashNumber.next({ matchQuestionId: 'q-0', position: 0, count: 3, value: 5, visibleMs: 800 });

    expect(component.flashNumber()?.value).toBe(5);
    expect(component.answerWindowOpen()).toBe(false);

    component.submitNumeric(15);
    expect(component.mySubmittedAnswer()).toBeNull();

    hub.answerWindowOpened.next(windowOpened(0));
    component.submitNumeric(15);
    expect(component.mySubmittedAnswer()).toBe(15);
  });

  it('drops flash events that belong to a different question', () => {
    hub.questionStarted.next(flashQuestion(1, 3));
    hub.flashNumber.next({ matchQuestionId: 'q-0', position: 2, count: 3, value: 9, visibleMs: 800 });
    hub.answerWindowOpened.next(windowOpened(0));

    expect(component.flashNumber()).toBeNull();
    expect(component.answerWindowOpen()).toBe(false);
  });

  it('tallies flash-arithmetic streak, best streak and accuracy from questionRevealed events, and resets the per-round submission on the next question', () => {
    // Round 0: answered correctly.
    hub.questionStarted.next(flashQuestion(0, 3));
    hub.answerWindowOpened.next(windowOpened(0));
    component.submitNumeric(15);
    hub.questionRevealed.next(flashReveal(0, 15));

    expect(component.flashStreak()).toBe(1);
    expect(component.flashBestStreak()).toBe(1);
    expect(component.flashAccuracy()).toBe(100);

    // Round 1: a fresh question must clear the previous round's submission before answering again.
    hub.questionStarted.next(flashQuestion(1, 4));
    expect(component.mySubmittedAnswer()).toBeNull();

    hub.answerWindowOpened.next(windowOpened(1));
    component.submitNumeric(99); // deliberately wrong
    hub.questionRevealed.next(flashReveal(1, 15));

    expect(component.flashStreak()).toBe(0);
    expect(component.flashBestStreak()).toBe(1); // best streak survives a later miss
    expect(component.flashAccuracy()).toBe(50);
  });
});
