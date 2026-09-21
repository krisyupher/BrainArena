import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { MatchResults } from './match-results';
import { RoomHubService } from '../../../core/services/room-hub.service';

describe('MatchResults', () => {
  let component: MatchResults;
  let fixture: ComponentFixture<MatchResults>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MatchResults],
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
          useValue: { joinRoomGroup: () => Promise.resolve() }
        }
      ]
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(MatchResults);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('should create and load results', () => {
    expect(component).toBeTruthy();
    httpMock.expectOne('/api/rooms/room-1/results').flush({
      matchId: 'm1',
      roomId: 'room-1',
      roomName: 'Test Room',
      ranking: [],
      review: []
    });
    expect(component.loading()).toBe(false);
  });
});
