using BrainArena.Domain.Enums;

namespace BrainArena.Domain.Entities;

/// <summary>
/// A multi-round, top-K-advance competition. Each round splits its active players into rooms of
/// (at most) RoomSize; the top AdvancesPerRoom scorers from each room advance to the next round —
/// see TournamentRound/TournamentRoundRoom. Reuses the existing Room/Match machinery entirely for
/// actual gameplay; a tournament only orchestrates *which* rooms get created and who's in them.
/// </summary>
public class Tournament
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public RoomTopic Topic { get; set; }
    public string GameMode { get; set; } = "multiple-choice";
    public int QuestionCount { get; set; }
    public int SecondsPerQuestion { get; set; }

    /// <summary>Total participants the tournament accepts before it can start.</summary>
    public int TournamentSize { get; set; }

    /// <summary>Players per room each round (the existing Room MaxPlayers bounds apply).</summary>
    public int RoomSize { get; set; }

    /// <summary>Top-N scorers per room who advance to the next round.</summary>
    public int AdvancesPerRoom { get; set; }

    public int MinPlayersToStart { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.Waiting;
    public Guid CreatorUserId { get; set; }

    /// <summary>0 before the tournament starts; the round currently in progress otherwise.</summary>
    public int CurrentRoundNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User? CreatorUser { get; set; }
    public ICollection<TournamentPlayer> Players { get; set; } = new List<TournamentPlayer>();
    public ICollection<TournamentRound> Rounds { get; set; } = new List<TournamentRound>();
}
