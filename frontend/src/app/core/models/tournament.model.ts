import { GameMode, RoomStatus, RoomTopic } from './room.model';

export type TournamentStatus = 'Waiting' | 'InProgress' | 'Finished';
export type TournamentPlayerStatus = 'Active' | 'Eliminated' | 'Champion';

export interface CreateTournamentRequest {
  name: string;
  topic: RoomTopic;
  gameMode: GameMode;
  questionCount: number;
  secondsPerQuestion: number;
  tournamentSize: number;
  roomSize: number;
  advancesPerRoom: number;
  minPlayersToStart: number;
}

export interface TournamentSummary {
  id: string;
  name: string;
  topic: RoomTopic;
  gameMode: GameMode;
  playerCount: number;
  tournamentSize: number;
  status: TournamentStatus;
}

export interface TournamentPlayer {
  userId: string;
  displayName: string;
  status: TournamentPlayerStatus;
  eliminatedAtRound: number | null;
}

export interface TournamentRoundRoomPlayer {
  userId: string;
  displayName: string;
}

export interface TournamentRoundRoom {
  roomId: string;
  roomStatus: RoomStatus;
  players: TournamentRoundRoomPlayer[];
}

export interface TournamentRound {
  roundNumber: number;
  isFinal: boolean;
  rooms: TournamentRoundRoom[];
}

export interface TournamentDetail {
  id: string;
  name: string;
  topic: RoomTopic;
  gameMode: GameMode;
  questionCount: number;
  secondsPerQuestion: number;
  tournamentSize: number;
  roomSize: number;
  advancesPerRoom: number;
  minPlayersToStart: number;
  status: TournamentStatus;
  creatorUserId: string;
  currentRoundNumber: number;
  championUserId: string | null;
  players: TournamentPlayer[];
  rounds: TournamentRound[];
}
