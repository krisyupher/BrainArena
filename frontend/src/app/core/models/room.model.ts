export type RoomTopic = 'Math' | 'Geography' | 'Chemistry' | 'IcfesGeneral';
export type RoomStatus = 'Waiting' | 'InProgress' | 'Finished';
export type GameMode = 'multiple-choice' | 'calculation' | 'flash-arithmetic';
export type RoomKind = 'Multiplayer' | 'Solitary';
export type Difficulty = 'Easy' | 'Medium' | 'Hard';

export const ROOM_TOPICS: RoomTopic[] = ['Math', 'Geography', 'Chemistry', 'IcfesGeneral'];
export const GAME_MODES: GameMode[] = ['multiple-choice', 'calculation', 'flash-arithmetic'];
export const DIFFICULTIES: Difficulty[] = ['Easy', 'Medium', 'Hard'];

export interface RoomSummary {
  id: string;
  name: string;
  topic: RoomTopic;
  gameMode: GameMode;
  questionCount: number;
  secondsPerQuestion: number;
  playerCount: number;
  maxPlayers: number;
  status: RoomStatus;
  isPrivate: boolean;
  kind: RoomKind;
  difficulty: Difficulty;
}

export interface RoomPlayer {
  userId: string;
  displayName: string;
}

export interface RoomDetail {
  id: string;
  name: string;
  topic: RoomTopic;
  maxPlayers: number;
  minPlayersToStart: number;
  questionCount: number;
  secondsPerQuestion: number;
  isPrivate: boolean;
  shareCode: string | null;
  status: RoomStatus;
  hostUserId: string;
  kind: RoomKind;
  difficulty: Difficulty;
  players: RoomPlayer[];
}

export interface CreateRoomRequest {
  name: string;
  topic: RoomTopic;
  maxPlayers: number;
  minPlayersToStart: number;
  questionCount: number;
  secondsPerQuestion: number;
  isPrivate: boolean;
  gameMode: GameMode;
  kind: RoomKind;
  difficulty: Difficulty;
}
