import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { CreateRoomForm } from './create-room-form';

describe('CreateRoomForm', () => {
  let component: CreateRoomForm;
  let fixture: ComponentFixture<CreateRoomForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateRoomForm],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({ config: { availableLangs: ['es', 'en'], defaultLang: 'es' } })
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CreateRoomForm);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('starts invalid because the room name is required', () => {
    expect(component.form.invalid).toBe(true);
  });
});
