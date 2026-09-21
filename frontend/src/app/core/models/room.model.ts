export type RoomTopic = 'Math' | 'Geography' | 'Chemistry' | 'IcfesGeneral';
export type RoomStatus = 'Waiting' | 'InProgress' | 'Finished';

export const ROOM_TOPICS: RoomTopic[] = ['Math', 'Geography', 'Chemistry', 'IcfesGeneral'];

export interface RoomSummary {
  id: string;
  name: string;
  topic: RoomTopic;
  playerCount: number;
  maxPlayers: number;
  status: RoomStatus;
  isPrivate: boolean;
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
}
