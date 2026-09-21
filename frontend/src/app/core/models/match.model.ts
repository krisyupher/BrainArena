export interface QuestionStartedEvent {
  matchId: string;
  matchQuestionId: string;
  index: number;
  totalQuestions: number;
  text: string;
  options: string[];
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
  correctOptionIndex: number;
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
  text: string;
  options: string[];
  correctOptionIndex: number;
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

export interface MatchResultsDto {
  matchId: string;
  roomId: string;
  roomName: string;
  ranking: RankingEntry[];
  review: QuestionReviewEntry[];
}
