import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { CreateGameForm } from './create-game-form';

describe('CreateGameForm', () => {
  let component: CreateGameForm;
  let fixture: ComponentFixture<CreateGameForm>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateGameForm],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({ config: { availableLangs: ['es', 'en'], defaultLang: 'es' } })
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CreateGameForm);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('defaults to the multiplayer kind and starts valid since every field has a sensible default', () => {
    expect(component.form.controls.kind.value).toBe('multiplayer');
    expect(component.form.valid).toBe(true);
  });

  it('switching to solitary hides the multiplayer-only fields but keeps the form valid', () => {
    component.setKind('solitary');
    fixture.detectChanges();

    expect(component.form.controls.kind.value).toBe('solitary');
    expect(component.form.valid).toBe(true);
  });

  it('switching to tournament keeps the form valid with its own defaults', () => {
    component.setKind('tournament');
    fixture.detectChanges();

    expect(component.form.controls.kind.value).toBe('tournament');
    expect(component.form.valid).toBe(true);
  });

  it('becomes invalid when maxPlayers is out of range', () => {
    component.form.controls.maxPlayers.setValue(1);
    expect(component.form.invalid).toBe(true);
  });
});
