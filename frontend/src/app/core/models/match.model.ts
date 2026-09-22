import { GameMode } from './room.model';

export interface QuestionStartedEvent {
  matchId: string;
  matchQuestionId: string;
  index: number;
  totalQuestions: number;
  kind: GameMode;
  text: string;
  /** Multiple-choice only. */
  options: string[] | null;
  endsAtUtc: string;
}

export interface ScoreboardEntry {
  userId: string;
  displayName: string;
  score: number;
  isConnected: boolean;
}

export interface QuestionRevealedEvent {
  matchId: string;
  matchQuestionId: string;
  index: number;
  kind: GameMode;
  /** Multiple-choice only. */
  correctOptionIndex: number | null;
  /** Calculation only. */
  correctNumericAnswer: number | null;
  explanation: string;
  endsAtUtc: string;
  scoreboard: ScoreboardEntry[];
}

export interface RankingEntry {
  userId: string;
  displayName: string;
  score: number;
  rank: number;
}

export interface QuestionReviewEntry {
  index: number;
  kind: GameMode;
  text: string;
  options: string[] | null;
  correctOptionIndex: number | null;
  correctNumericAnswer: number | null;
  explanation: string;
}

export interface MatchEndedEvent {
  matchId: string;
  ranking: RankingEntry[];
  review: QuestionReviewEntry[];
}

export type MatchPhase = 'Countdown' | 'Question' | 'Reveal';

export interface MatchResyncEvent {
  matchId: string;
  phase: MatchPhase;
  currentQuestion: QuestionStartedEvent | null;
  currentReveal: QuestionRevealedEvent | null;
  yourScore: number;
  scoreboard: ScoreboardEntry[];
}

/** Non-personalized counterpart of MatchResyncEvent, for a spectator joining mid-match. */
export interface MatchSpectatorSyncEvent {
  matchId: string;
  phase: MatchPhase;
  currentQuestion: QuestionStartedEvent | null;
  currentReveal: QuestionRevealedEvent | null;
  scoreboard: ScoreboardEntry[];
}

export interface MatchResultsDto {
  matchId: string;
  roomId: string;
  roomName: string;
  ranking: RankingEntry[];
  review: QuestionReviewEntry[];
}
