export type RoomTopic = 'Math' | 'Geography' | 'Chemistry' | 'IcfesGeneral';
export type RoomStatus = 'Waiting' | 'InProgress' | 'Finished';
export type GameMode = 'multiple-choice' | 'calculation';
export type RoomKind = 'Multiplayer' | 'Solitary';

export const ROOM_TOPICS: RoomTopic[] = ['Math', 'Geography', 'Chemistry', 'IcfesGeneral'];
export const GAME_MODES: GameMode[] = ['multiple-choice', 'calculation'];

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
}
