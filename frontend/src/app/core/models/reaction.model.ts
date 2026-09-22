/** Small fixed emoji set, kept in sync with backend ReactionValidation.AllowedEmojis. */
export const ALLOWED_REACTIONS: readonly string[] = ['👏', '🎉', '😂', '😮', '❤️', '🔥'];

/** Ephemeral, never persisted — a live broadcast only. */
export interface ReactionSentEvent {
  fromUserId: string;
  targetUserId: string;
  emoji: string;
}
