import { RoomTopic } from './room.model';

export interface QuestionDto {
  id: string;
  topic: RoomTopic;
  difficulty: number;
  text: string;
  options: string[];
  correctOptionIndex: number;
  explanation: string;
  language: string;
}

export interface QuestionUpsertRequest {
  topic: RoomTopic;
  difficulty: number;
  text: string;
  options: string[];
  correctOptionIndex: number;
  explanation: string;
  language: string;
}

export interface QuestionImportResult {
  importedCount: number;
  errors: string[];
}
