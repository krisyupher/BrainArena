import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { Subject } from 'rxjs';
import { MatchPlay } from './match-play';
import { RoomHubService } from '../../../core/services/room-hub.service';
import { QuestionRevealedEvent, QuestionStartedEvent } from '../../../core/models/match.model';

describe('MatchPlay', () => {
  let component: MatchPlay;
  let fixture: ComponentFixture<MatchPlay>;
  let hub: {
    questionStarted: Subject<QuestionStartedEvent>;
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
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('tallies flash-arithmetic streak, best streak and accuracy from questionRevealed events, and resets the per-round submission on the next question', () => {
    component.isParticipant.set(true);

    const question = (index: number, level: number): QuestionStartedEvent => ({
      matchId: 'm',
      matchQuestionId: `q-${index}`,
      index,
      totalQuestions: 5,
      kind: 'flash-arithmetic',
      text: '5,7,3',
      options: null,
      endsAtUtc: new Date(Date.now() + 20000).toISOString(),
      level
    });

    const reveal = (index: number, correct: number): QuestionRevealedEvent => ({
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

    // Round 0: answered correctly.
    hub.questionStarted.next(question(0, 3));
    component.submitNumeric(15);
    hub.questionRevealed.next(reveal(0, 15));

    expect(component.flashStreak()).toBe(1);
    expect(component.flashBestStreak()).toBe(1);
    expect(component.flashAccuracy()).toBe(100);

    // Round 1: a fresh question must clear the previous round's submission before answering again.
    hub.questionStarted.next(question(1, 4));
    expect(component.mySubmittedAnswer()).toBeNull();

    component.submitNumeric(99); // deliberately wrong
    hub.questionRevealed.next(reveal(1, 15));

    expect(component.flashStreak()).toBe(0);
    expect(component.flashBestStreak()).toBe(1); // best streak survives a later miss
    expect(component.flashAccuracy()).toBe(50);
  });
});
