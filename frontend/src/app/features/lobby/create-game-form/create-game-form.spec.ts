import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { CreateGameForm, GameKind } from './create-game-form';

const AUTH_STORAGE_KEY = 'brainarena.auth';
const registeredSession = { token: 't', userId: 'u-1', displayName: 'Player One', role: 'Player' };
const guestSession = { token: 't', userId: 'g-1', displayName: 'Guest', role: 'Guest' };

async function createForm(session: object | null, initialKind?: GameKind): Promise<ComponentFixture<CreateGameForm>> {
  if (session) {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
  }

  await TestBed.configureTestingModule({
    imports: [CreateGameForm],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideTransloco({ config: { availableLangs: ['es', 'en'], defaultLang: 'es' } })
    ]
  }).compileComponents();

  const fixture = TestBed.createComponent(CreateGameForm);
  if (initialKind) {
    fixture.componentRef.setInput('initialKind', initialKind);
  }
  fixture.detectChanges();
  await fixture.whenStable();
  return fixture;
}

describe('CreateGameForm', () => {
  afterEach(() => localStorage.removeItem(AUTH_STORAGE_KEY));

  it('defaults to the multiplayer kind and starts valid since every field has a sensible default', async () => {
    const component = (await createForm(registeredSession)).componentInstance;

    expect(component.form.controls.kind.value).toBe('multiplayer');
    expect(component.form.valid).toBe(true);
  });

  it('pre-selects the kind of the lobby tab it was opened from', async () => {
    const component = (await createForm(registeredSession, 'tournament')).componentInstance;

    expect(component.form.controls.kind.value).toBe('tournament');
  });

  it('switching to solitary hides the multiplayer-only fields but keeps the form valid', async () => {
    const fixture = await createForm(registeredSession);
    fixture.componentInstance.setKind('solitary');
    fixture.detectChanges();

    expect(fixture.componentInstance.form.controls.kind.value).toBe('solitary');
    expect(fixture.componentInstance.form.valid).toBe(true);
  });

  it('switching to tournament keeps the form valid with its own defaults', async () => {
    const fixture = await createForm(registeredSession);
    fixture.componentInstance.setKind('tournament');
    fixture.detectChanges();

    expect(fixture.componentInstance.form.controls.kind.value).toBe('tournament');
    expect(fixture.componentInstance.form.valid).toBe(true);
  });

  it('becomes invalid when maxPlayers is out of range', async () => {
    const component = (await createForm(registeredSession)).componentInstance;
    component.form.controls.maxPlayers.setValue(1);

    expect(component.form.invalid).toBe(true);
  });

  it('offers only solitary practice to an anonymous visitor, whatever tab it was opened from', async () => {
    const component = (await createForm(null, 'multiplayer')).componentInstance;

    expect(component.kinds()).toEqual(['solitary']);
    expect(component.form.controls.kind.value).toBe('solitary');

    component.setKind('tournament');
    expect(component.form.controls.kind.value).toBe('solitary');
  });

  it('offers only solitary practice to a guest session too', async () => {
    const component = (await createForm(guestSession, 'tournament')).componentInstance;

    expect(component.kinds()).toEqual(['solitary']);
    expect(component.form.controls.kind.value).toBe('solitary');
  });
});
