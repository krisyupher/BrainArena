import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { Subject } from 'rxjs';
import { Room } from './room';
import { RoomHubService } from '../../core/services/room-hub.service';

describe('Room', () => {
  let component: Room;
  let fixture: ComponentFixture<Room>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Room],
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
            roomUpdated: new Subject<void>(),
            matchStarting: new Subject<{ matchId: string; countdownSeconds: number }>(),
            matchResync: new Subject<unknown>(),
            matchSpectatorSync: new Subject<unknown>(),
            matchStartFailed: new Subject<string>(),
            chatMessageReceived: new Subject<unknown>(),
            chatMessageReported: new Subject<string>(),
            reactionSent: new Subject<unknown>(),
            joinRoomGroup: () => Promise.resolve(),
            leaveRoomGroup: () => Promise.resolve(),
            joinAsSpectator: () => Promise.resolve(),
            leaveAsSpectator: () => Promise.resolve(),
            startNow: () => Promise.resolve()
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(Room);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
