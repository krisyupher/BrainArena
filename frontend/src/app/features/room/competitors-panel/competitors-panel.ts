import { Component, OnDestroy, OnInit, effect, inject, input, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { RoomHubService } from '../../../core/services/room-hub.service';
import { ALLOWED_REACTIONS } from '../../../core/models/reaction.model';

export interface CompetitorViewModel {
  userId: string;
  displayName: string;
  /** Omitted where there's no notion of a score yet (e.g. the waiting room). */
  score?: number;
  /** Omitted where connection status isn't tracked (e.g. the waiting room). */
  isConnected?: boolean;
  isHost?: boolean;
}

interface ActiveReaction {
  emoji: string;
  /** Unique per burst — lets @for's track force a fresh DOM node so the CSS animation restarts. */
  key: number;
}

const REACTION_DISPLAY_MS = 1800;

@Component({
  imports: [TranslocoPipe],
  selector: 'app-competitors-panel',
  styleUrl: './competitors-panel.scss',
  templateUrl: './competitors-panel.html'
})
export class CompetitorsPanel implements OnInit, OnDestroy {
  private readonly roomHub = inject(RoomHubService);

  readonly competitors = input.required<CompetitorViewModel[]>();
  readonly myUserId = input<string | undefined>(undefined);
  readonly roomId = input<string | undefined>(undefined);
  /** False (default) when the current visitor can't send reactions — not signed in. */
  readonly canReact = input(false);

  readonly reactions = ALLOWED_REACTIONS;
  readonly openPickerFor = signal<string | null>(null);

  private readonly activeReactions = signal<Record<string, ActiveReaction>>({});
  private readonly clearTimers = new Map<string, ReturnType<typeof setTimeout>>();
  private reactionKeyCounter = 0;
  private readonly subscriptions = new Subscription();

  /** Briefly highlighted score numbers — set whenever a competitor's score just went up. */
  private readonly bumpedIds = signal<Set<string>>(new Set());
  private readonly lastScores = new Map<string, number>();
  private readonly bumpTimers = new Map<string, ReturnType<typeof setTimeout>>();

  constructor() {
    effect(() => {
      const bumped: string[] = [];
      for (const c of this.competitors()) {
        if (c.score === undefined) {
          continue;
        }
        const previous = this.lastScores.get(c.userId);
        if (previous !== undefined && c.score > previous) {
          bumped.push(c.userId);
        }
        this.lastScores.set(c.userId, c.score);
      }
      if (bumped.length > 0) {
        this.bumpedIds.update((set) => new Set([...set, ...bumped]));
        for (const userId of bumped) {
          this.scheduleBumpClear(userId);
        }
      }
    });
  }

  ngOnInit(): void {
    this.subscriptions.add(
      this.roomHub.reactionSent.subscribe((event) => this.showReaction(event.targetUserId, event.emoji))
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    for (const timer of this.clearTimers.values()) {
      clearTimeout(timer);
    }
    for (const timer of this.bumpTimers.values()) {
      clearTimeout(timer);
    }
  }

  isBumped(userId: string): boolean {
    return this.bumpedIds().has(userId);
  }

  private scheduleBumpClear(userId: string): void {
    const existing = this.bumpTimers.get(userId);
    if (existing) {
      clearTimeout(existing);
    }
    const timer = setTimeout(() => {
      this.bumpedIds.update((set) => {
        const next = new Set(set);
        next.delete(userId);
        return next;
      });
      this.bumpTimers.delete(userId);
    }, 700);
    this.bumpTimers.set(userId, timer);
  }

  togglePicker(userId: string): void {
    this.openPickerFor.set(this.openPickerFor() === userId ? null : userId);
  }

  react(targetUserId: string, emoji: string): void {
    this.openPickerFor.set(null);
    const roomId = this.roomId();
    if (!roomId) {
      return;
    }
    this.roomHub.sendReaction(roomId, targetUserId, emoji).catch(() => {
      // Best-effort, purely cosmetic — a failed reaction isn't worth surfacing an error for.
    });
  }

  activeReactionsFor(userId: string): ActiveReaction[] {
    const reaction = this.activeReactions()[userId];
    return reaction ? [reaction] : [];
  }

  private showReaction(userId: string, emoji: string): void {
    const key = ++this.reactionKeyCounter;
    this.activeReactions.update((map) => ({ ...map, [userId]: { emoji, key } }));

    const existingTimer = this.clearTimers.get(userId);
    if (existingTimer) {
      clearTimeout(existingTimer);
    }
    const timer = setTimeout(() => {
      this.activeReactions.update((map) => {
        if (map[userId]?.key !== key) {
          return map; // a newer reaction already replaced this one
        }
        const { [userId]: _removed, ...rest } = map;
        return rest;
      });
      this.clearTimers.delete(userId);
    }, REACTION_DISPLAY_MS);
    this.clearTimers.set(userId, timer);
  }
}
