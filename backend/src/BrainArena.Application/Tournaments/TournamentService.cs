using BrainArena.Application.Abstractions;
using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tournaments;

public class TournamentService(
    ITournamentRepository tournaments,
    IRoomRepository rooms,
    IUserRepository users,
    IGameModeRegistry gameModeRegistry,
    ITournamentNotifier notifier,
    IMatchOrchestrator matchOrchestrator,
    TournamentAdvancementLock advancementLock) : ITournamentService
{
    public async Task<IReadOnlyList<TournamentSummaryDto>> GetOpenTournamentsAsync(CancellationToken ct = default)
    {
        var open = await tournaments.GetOpenTournamentsAsync(ct);
        return open.Select(MapSummary).ToList();
    }

    public async Task<TournamentDetailDto> CreateTournamentAsync(Guid creatorUserId, CreateTournamentRequest request, CancellationToken ct = default)
    {
        TournamentValidation.Validate(request, gameModeRegistry.ModeKeys);

        var creator = await users.GetByIdAsync(creatorUserId, ct)
            ?? throw new AppException("Creator user not found.", 404);

        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Topic = request.Topic,
            GameMode = request.GameMode,
            QuestionCount = request.QuestionCount,
            SecondsPerQuestion = request.SecondsPerQuestion,
            TournamentSize = request.TournamentSize,
            RoomSize = request.RoomSize,
            AdvancesPerRoom = request.AdvancesPerRoom,
            MinPlayersToStart = request.MinPlayersToStart,
            Status = TournamentStatus.Waiting,
            CreatorUserId = creatorUserId,
            CurrentRoundNumber = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        tournament.Players.Add(new TournamentPlayer
        {
            TournamentId = tournament.Id,
            UserId = creatorUserId,
            JoinedAt = DateTimeOffset.UtcNow,
            User = creator
        });

        await tournaments.AddAsync(tournament, ct);
        await tournaments.SaveChangesAsync(ct);
        await notifier.NotifyTournamentListChangedAsync(ct);

        return MapDetail(tournament);
    }

    public async Task<TournamentDetailDto> GetTournamentDetailAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct)
            ?? throw new AppException("Tournament not found.", 404);

        return MapDetail(tournament);
    }

    public async Task<TournamentDetailDto> JoinTournamentAsync(Guid userId, Guid tournamentId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct)
            ?? throw new AppException("Tournament not found.", 404);

        if (tournament.Players.Any(p => p.UserId == userId))
        {
            return MapDetail(tournament);
        }

        if (tournament.Status != TournamentStatus.Waiting)
            throw new AppException("This tournament has already started.", 409);

        if (tournament.Players.Count >= tournament.TournamentSize)
            throw new AppException("This tournament is full.", 409);

        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new AppException("User not found.", 404);

        tournament.Players.Add(new TournamentPlayer
        {
            TournamentId = tournament.Id,
            UserId = userId,
            JoinedAt = DateTimeOffset.UtcNow,
            User = user
        });

        var isFull = tournament.Players.Count >= tournament.TournamentSize;

        await tournaments.SaveChangesAsync(ct);
        await notifier.NotifyTournamentListChangedAsync(ct);
        await notifier.NotifyTournamentUpdatedAsync(tournament.Id, ct);

        if (isFull)
        {
            await StartFirstRoundAsync(tournament, ct);
        }

        return MapDetail(tournament);
    }

    public async Task LeaveTournamentAsync(Guid userId, Guid tournamentId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct)
            ?? throw new AppException("Tournament not found.", 404);

        if (tournament.Status != TournamentStatus.Waiting)
        {
            return; // once started, leaving no longer applies — mirrors RoomService.LeaveRoomAsync
        }

        var player = tournament.Players.FirstOrDefault(p => p.UserId == userId);
        if (player is null)
        {
            return;
        }

        tournament.Players.Remove(player);

        if (tournament.CreatorUserId == userId)
        {
            var nextCreator = tournament.Players.OrderBy(p => p.JoinedAt).FirstOrDefault();
            if (nextCreator is not null)
            {
                tournament.CreatorUserId = nextCreator.UserId;
            }
        }

        await tournaments.SaveChangesAsync(ct);
        await notifier.NotifyTournamentListChangedAsync(ct);
        await notifier.NotifyTournamentUpdatedAsync(tournament.Id, ct);
    }

    public async Task StartTournamentAsync(Guid tournamentId, Guid requestingUserId, CancellationToken ct = default)
    {
        var tournament = await tournaments.GetByIdAsync(tournamentId, ct)
            ?? throw new AppException("Tournament not found.", 404);

        if (tournament.Status != TournamentStatus.Waiting)
            throw new AppException("This tournament has already started.", 409);

        if (tournament.CreatorUserId != requestingUserId)
            throw new AppException("Only the creator can start the tournament.", 403);

        if (tournament.Players.Count < tournament.MinPlayersToStart)
            throw new AppException($"Need at least {tournament.MinPlayersToStart} players to start.", 409);

        await StartFirstRoundAsync(tournament, ct);
    }

    public async Task HandleRoomMatchFinishedAsync(Guid roomId, CancellationToken ct = default)
    {
        var tournamentId = await tournaments.GetTournamentIdForRoomAsync(roomId, ct);
        if (tournamentId is null)
        {
            return; // not a tournament room
        }

        using (await advancementLock.AcquireAsync(tournamentId.Value, ct))
        {
            // First (and only) tracked fetch on this DbContext — deliberately not preceded by any
            // other query touching the same Tournament entity (see GetTournamentIdForRoomAsync's
            // remarks), so CurrentRoundNumber here is always a genuinely fresh read.
            var roundRoom = await tournaments.GetRoundRoomByRoomIdAsync(roomId, ct);
            var round = roundRoom?.TournamentRound;
            var tournament = round?.Tournament;
            if (round is null || tournament is null || tournament.Status == TournamentStatus.Finished)
            {
                return;
            }

            if (round.RoundNumber != tournament.CurrentRoundNumber)
            {
                return; // this round was already advanced past by a concurrent call
            }

            var allRoomsFinished = round.RoundRooms.All(rr => rr.Room!.Status == RoomStatus.Finished);
            if (!allRoomsFinished)
            {
                return; // other rooms in this round are still playing
            }

            var advancing = new List<Guid>();
            foreach (var rr in round.RoundRooms)
            {
                var ranking = await tournaments.GetRoomRankingAsync(rr.RoomId, ct);
                // At least one elimination per room, even in an undersized remainder room.
                var advancesFromThisRoom = Math.Min(tournament.AdvancesPerRoom, ranking.Count - 1);

                for (var i = 0; i < ranking.Count; i++)
                {
                    var userId = ranking[i].UserId;
                    var player = tournament.Players.First(p => p.UserId == userId);
                    if (i < advancesFromThisRoom)
                    {
                        advancing.Add(userId);
                    }
                    else
                    {
                        player.Status = TournamentPlayerStatus.Eliminated;
                        player.EliminatedAtRound = round.RoundNumber;
                    }
                }
            }

            if (round.IsFinal)
            {
                // The final round's single room's winner is the champion — advancesFromThisRoom
                // above capped at (ranking.Count - 1), so exactly the winner was excluded from
                // elimination and is the sole entry left in `advancing`.
                if (advancing.Count == 1)
                {
                    var champion = tournament.Players.First(p => p.UserId == advancing[0]);
                    champion.Status = TournamentPlayerStatus.Champion;
                }

                tournament.Status = TournamentStatus.Finished;
                await tournaments.SaveChangesAsync(ct);
                await notifier.NotifyTournamentUpdatedAsync(tournament.Id, ct);
                return;
            }

            await CreateNextRoundAsync(tournament, round.RoundNumber + 1, advancing, ct);
        }
    }

    private async Task StartFirstRoundAsync(Tournament tournament, CancellationToken ct)
    {
        tournament.Status = TournamentStatus.InProgress;
        var playerIds = tournament.Players.Select(p => p.UserId).ToList();
        await CreateNextRoundAsync(tournament, 1, playerIds, ct);
    }

    private async Task CreateNextRoundAsync(Tournament tournament, int roundNumber, IReadOnlyList<Guid> playerIds, CancellationToken ct)
    {
        var isFinal = playerIds.Count <= tournament.RoomSize;
        var round = new TournamentRound
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            RoundNumber = roundNumber,
            IsFinal = isFinal
        };

        var roomGroups = SplitEvenly(playerIds, tournament.RoomSize);

        for (var i = 0; i < roomGroups.Count; i++)
        {
            var group = roomGroups[i];
            var room = new Room
            {
                Id = Guid.NewGuid(),
                Name = $"{tournament.Name} — Round {roundNumber} — Room {i + 1}",
                Topic = tournament.Topic,
                MaxPlayers = group.Count,
                MinPlayersToStart = group.Count,
                QuestionCount = tournament.QuestionCount,
                SecondsPerQuestion = tournament.SecondsPerQuestion,
                IsPrivate = false,
                Status = RoomStatus.Waiting,
                // Kind intentionally left at its Multiplayer default — a tournament round room
                // always has 2+ players (RoomSize), never a solitary practice room.
                HostUserId = group[0],
                GameMode = tournament.GameMode,
                CreatedAt = DateTimeOffset.UtcNow
            };

            foreach (var userId in group)
            {
                room.Players.Add(new RoomPlayer { RoomId = room.Id, UserId = userId, JoinedAt = DateTimeOffset.UtcNow });
            }

            await rooms.AddAsync(room, ct);

            var roundRoom = new TournamentRoundRoom
            {
                Id = Guid.NewGuid(),
                TournamentRoundId = round.Id,
                RoomId = room.Id,
                Room = room
            };
            round.RoundRooms.Add(roundRoom); // in-memory only, for the notifier/auto-start loop below
            await tournaments.AddRoundRoomAsync(roundRoom, ct);
        }

        await tournaments.AddRoundAsync(round, ct);
        tournament.CurrentRoundNumber = roundNumber;
        await tournaments.SaveChangesAsync(ct);

        await notifier.NotifyTournamentUpdatedAsync(tournament.Id, ct);
        await notifier.NotifyTournamentListChangedAsync(ct);

        // Every room was created already full — this reuses the exact same auto-start path a
        // normal room's Nth player joining would trigger, no new orchestrator method needed.
        foreach (var rr in round.RoundRooms)
        {
            await matchOrchestrator.TryAutoStartAsync(rr.RoomId, ct);
        }
    }

    private static List<List<Guid>> SplitEvenly(IReadOnlyList<Guid> playerIds, int roomSize)
    {
        var roomCount = Math.Max(1, (int)Math.Ceiling(playerIds.Count / (double)roomSize));
        var groups = Enumerable.Range(0, roomCount).Select(_ => new List<Guid>()).ToList();
        for (var i = 0; i < playerIds.Count; i++)
        {
            groups[i % roomCount].Add(playerIds[i]);
        }
        return groups;
    }

    private static TournamentSummaryDto MapSummary(Tournament t) =>
        new(t.Id, t.Name, t.Topic, t.GameMode, t.Players.Count, t.TournamentSize, t.Status);

    private static TournamentDetailDto MapDetail(Tournament t)
    {
        var champion = t.Players.FirstOrDefault(p => p.Status == TournamentPlayerStatus.Champion);

        return new TournamentDetailDto(
            t.Id, t.Name, t.Topic, t.GameMode, t.QuestionCount, t.SecondsPerQuestion,
            t.TournamentSize, t.RoomSize, t.AdvancesPerRoom, t.MinPlayersToStart,
            t.Status, t.CreatorUserId, t.CurrentRoundNumber, champion?.UserId,
            t.Players
                .Select(p => new TournamentPlayerDto(p.UserId, p.User?.DisplayName ?? string.Empty, p.Status, p.EliminatedAtRound))
                .ToList(),
            t.Rounds
                .OrderBy(r => r.RoundNumber)
                .Select(r => new TournamentRoundDto(
                    r.RoundNumber,
                    r.IsFinal,
                    r.RoundRooms
                        .Select(rr => new TournamentRoundRoomDto(
                            rr.RoomId,
                            rr.Room!.Status,
                            rr.Room.Players
                                .Select(rp => new TournamentRoundRoomPlayerDto(rp.UserId, rp.User?.DisplayName ?? string.Empty))
                                .ToList()))
                        .ToList()))
                .ToList());
    }
}
