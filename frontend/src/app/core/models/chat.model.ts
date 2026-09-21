export interface ChatMessageDto {
  id: string;
  userId: string;
  displayName: string;
  text: string;
  sentAt: string;
  isReported: boolean;
}
