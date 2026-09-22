namespace BrainArena.Application.Reactions;

/// <summary>Ephemeral, never persisted — purely a live broadcast for the competitors panel to animate.</summary>
public record ReactionSentPayload(Guid FromUserId, Guid TargetUserId, string Emoji);
