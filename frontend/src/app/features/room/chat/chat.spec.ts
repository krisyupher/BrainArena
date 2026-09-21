import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { Subject } from 'rxjs';
import { Chat } from './chat';
import { RoomHubService } from '../../../core/services/room-hub.service';

describe('Chat', () => {
  let component: Chat;
  let fixture: ComponentFixture<Chat>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Chat],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({ config: { availableLangs: ['es', 'en'], defaultLang: 'es' } }),
        {
          provide: RoomHubService,
          useValue: {
            chatMessageReceived: new Subject(),
            chatMessageReported: new Subject(),
            sendChatMessage: () => Promise.resolve(),
            reportChatMessage: () => Promise.resolve()
          }
        }
      ]
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Chat);
    fixture.componentRef.setInput('roomId', 'room-1');
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('should create and load chat history', () => {
    expect(component).toBeTruthy();
    httpMock.expectOne('/api/rooms/room-1/chat').flush([]);
    expect(component.messages()).toEqual([]);
  });

  it('starts invalid because the message text is required', () => {
    httpMock.expectOne('/api/rooms/room-1/chat').flush([]);
    expect(component.form.invalid).toBe(true);
  });
});
