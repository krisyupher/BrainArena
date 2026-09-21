import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { Subject } from 'rxjs';
import { Lobby } from './lobby';
import { RoomHubService } from '../../core/services/room-hub.service';

describe('Lobby', () => {
  let component: Lobby;
  let fixture: ComponentFixture<Lobby>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Lobby],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({ config: { availableLangs: ['es', 'en'], defaultLang: 'es' } }),
        {
          provide: RoomHubService,
          useValue: {
            roomListChanged: new Subject<void>(),
            connect: () => Promise.resolve(),
            disconnect: () => Promise.resolve()
          }
        }
      ]
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Lobby);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('should create and load the open room list', () => {
    expect(component).toBeTruthy();
    httpMock.expectOne('/api/rooms').flush([]);
    expect(component.loading()).toBe(false);
  });
});
