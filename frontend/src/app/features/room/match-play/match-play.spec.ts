import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { Subject } from 'rxjs';
import { MatchPlay } from './match-play';
import { RoomHubService } from '../../../core/services/room-hub.service';

describe('MatchPlay', () => {
  let component: MatchPlay;
  let fixture: ComponentFixture<MatchPlay>;

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
            joinAsSpectator: () => Promise.resolve()
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(MatchPlay);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
