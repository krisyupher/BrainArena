using System.Collections.Concurrent;
using BrainArena.Api.Hubs;
using BrainArena.Application.Abstractions;
using BrainArena.Application.Common;
using BrainArena.Application.Matches;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;
using BrainArena.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BrainArena.Api.Matches;

/// <summary>
/// Singleton that owns every active match's in-memory state and drives its countdown -> question
/// -> reveal -> ... -> finished loop with a plain async delay loop (one background Task per
/// active match — plenty for this app's scale). Persists match/answer rows as it goes so results
/// survive a client disconnect, but a full server restart mid-match does lose live state; that
/// tradeoff is accepted for this phase (see the project plan's cross-cutting architecture notes).
/// </summary>
public class MatchOrchestrator(
    IServiceScopeFactory scopeFactory,
    IHubContext<RoomHub> hub,
    IGameModeRegistry gameModeRegistry,
    IOptions<MatchTimingOptions> timingOptions,
    ILogger<MatchOrchestrator> logger) : IMatchOrchestrator
{
    private readonly TimeSpan _countdownDuration = TimeSpan.FromSeconds(timingOptions.Value.CountdownSeconds);
    private readonly TimeSpan _revealDuration = TimeSpan.FromSeconds(timingOptions.Value.RevealSeconds);

    private readonly ConcurrentDictionary<Guid, byte> _startingRooms = new();
    private readonly ConcurrentDictionary<Guid, MatchRuntimeState> _matches = new();

    public bool HasActiveMatch(Guid roomId) => _matches.ContainsKey(roomId) || _startingRooms.ContainsKey(roomId);

    public async Task TryAutoStartAsync(Guid roomId, CancellationToken ct = default)
    {
        if (HasActiveMatch(roomId))
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var rooms = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var room = await rooms.GetByIdAsync(roomId, ct);
        if (room is null || !MatchStartRules.CanAutoStart(room))
        {
            return;
        }

        await StartMatchAsync(room, scope, ct);
    }

    public async Task StartNowAsync(Guid roomId, Guid requestingUserId, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var rooms = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
        var room = await rooms.GetByIdAsync(roomId, ct) ?? throw new AppException("Room not found.", 404);

        if (HasActiveMatch(roomId))
        {
            throw new AppException("The match has already started.", 409);
        }

        MatchStartRules.EnsureCanStartNow(room, requestingUserId);

        await StartMatchAsync(room, scope, ct);
    }

    public async Task SubmitAnswerAsync(Guid roomId, Guid userId, Guid matchQuestionId, int selectedOptionIndex)
    {
        if (!_matches.TryGetValue(roomId, out var state))
        {
            throw new AppException("No active match for this room.", 404);
        }

        if (state.Phase != MatchPhase.Question)
        {
            throw new AppException("This question is closed.", 409);
        }

        var current = state.CurrentQuestion;
        if (current.MatchQuestion.Id != matchQuestionId)
        {
            throw new AppException("That's not the current question.", 409);
        }

        if (!state.Players.TryGetValue(userId, out var player))
        {
            throw new AppException("You're not part of this match.", 403);
        }

        var question = current.MatchQuestion.Question!;
        if (selectedOptionIndex < 0 || selectedOptionIndex >= question.Options.Length)
        {
            throw new AppException("Invalid option.", 400);
        }

        var timeRemaining = state.PhaseEndsAtUtc - DateTimeOffset.UtcNow;
        if (timeRemaining < TimeSpan.Zero)
        {
            timeRemaining = TimeSpan.Zero;
        }

        var timeLimit = TimeSpan.FromSeconds(state.SecondsPerQuestion);
        var result = state.GameMode.EvaluateAnswer(current.MatchQuestion, selectedOptionIndex, timeRemaining, timeLimit);

        if (!current.Answers.TryAdd(userId, new PlayerAnswerRuntime(selectedOptionIndex, result.Points, result.IsCorrect)))
        {
            throw new AppException("You already answered this question.", 409);
        }

        player.Score += result.Points;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainArenaDbContext>();
        db.MatchAnswers.Add(new MatchAnswer
        {
            MatchQuestionId = matchQuestionId,
            UserId = userId,
            SelectedOptionIndex = selectedOptionIndex,
            PointsAwarded = result.Points,
            AnsweredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public MatchResyncPayload? Join(Guid roomId, Guid userId)
    {
        if (!_matches.TryGetValue(roomId, out var state) || !state.Players.TryGetValue(userId, out var player))
        {
            return null;
        }

        player.IsConnected = true;
        player.DisconnectedAt = null;

        var scoreboard = state.BuildScoreboard();

        return state.Phase switch
        {
            MatchPhase.Question => new MatchResyncPayload(
                state.MatchId,
                "Question",
                state.GameMode.ToClientPayload(state.CurrentQuestion.MatchQuestion, state.CurrentIndex, state.Questions.Count, state.PhaseEndsAtUtc),
                null,
                player.Score,
                scoreboard),
            MatchPhase.Reveal => new MatchResyncPayload(
                state.MatchId,
                "Reveal",
                null,
                state.GameMode.ToRevealPayload(state.CurrentQuestion.MatchQuestion, state.CurrentIndex, state.PhaseEndsAtUtc, scoreboard),
                player.Score,
                scoreboard),
            MatchPhase.Countdown => new MatchResyncPayload(state.MatchId, "Countdown", null, null, player.Score, scoreboard),
            _ => null
        };
    }

    public void MarkDisconnected(Guid roomId, Guid userId)
    {
        if (_matches.TryGetValue(roomId, out var state) && state.Players.TryGetValue(userId, out var player))
        {
            player.IsConnected = false;
            player.DisconnectedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task StartMatchAsync(Room room, IServiceScope scope, CancellationToken ct)
    {
        if (!_startingRooms.TryAdd(room.Id, 0))
        {
            return;
        }

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<BrainArenaDbContext>();
            var questionRepo = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();

            var questions = await questionRepo.GetRandomByTopicAsync(room.Topic, room.QuestionCount, ct);
            if (questions.Count < room.QuestionCount)
            {
                await hub.Clients.Group(RoomHub.RoomGroupName(room.Id)).SendAsync(
                    "MatchStartFailed",
                    $"Not enough questions available for this topic yet (need {room.QuestionCount}, have {questions.Count}).",
                    ct);
                return;
            }

            var matchId = Guid.NewGuid();
            var match = new Match
            {
                Id = matchId,
                RoomId = room.Id,
                Status = MatchStatus.Countdown,
                CreatedAt = DateTimeOffset.UtcNow,
                StartedAt = DateTimeOffset.UtcNow
            };

            var matchQuestions = questions
                .Select((q, index) => new MatchQuestion
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId,
                    QuestionId = q.Id,
                    OrderIndex = index,
                    Question = q
                })
                .ToList();

            var matchPlayers = room.Players
                .Select(p => new MatchPlayer { MatchId = matchId, UserId = p.UserId, Score = 0 })
                .ToList();

            db.Matches.Add(match);
            db.MatchQuestions.AddRange(matchQuestions);
            db.MatchPlayers.AddRange(matchPlayers);
            room.Status = RoomStatus.InProgress;

            await db.SaveChangesAsync(ct);

            var runtimePlayers = room.Players.ToDictionary(
                p => p.UserId,
                p => new PlayerRuntime { UserId = p.UserId, DisplayName = p.User?.DisplayName ?? string.Empty });

            var state = new MatchRuntimeState
            {
                MatchId = matchId,
                RoomId = room.Id,
                GameMode = gameModeRegistry.Resolve(room.GameMode),
                SecondsPerQuestion = room.SecondsPerQuestion,
                Questions = matchQuestions.Select(mq => new MatchQuestionRuntime { MatchQuestion = mq }).ToList(),
                Players = runtimePlayers
            };
            _matches[room.Id] = state;

            await hub.Clients.Group("lobby").SendAsync("RoomListChanged", cancellationToken: ct);
            await hub.Clients.Group(RoomHub.RoomGroupName(room.Id)).SendAsync(
                "MatchStarting", matchId, (int)_countdownDuration.TotalSeconds, ct);

            _ = Task.Run(() => RunMatchLoopAsync(room.Id));
        }
        finally
        {
            _startingRooms.TryRemove(room.Id, out _);
        }
    }

    private async Task RunMatchLoopAsync(Guid roomId)
    {
        try
        {
            var state = _matches[roomId];
            var groupName = RoomHub.RoomGroupName(roomId);

            await Task.Delay(_countdownDuration);

            for (var i = 0; i < state.Questions.Count; i++)
            {
                state.CurrentIndex = i;
                var current = state.Questions[i];
                var endsAt = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(state.SecondsPerQuestion));
                state.PhaseEndsAtUtc = endsAt;
                state.Phase = MatchPhase.Question;

                var payload = state.GameMode.ToClientPayload(current.MatchQuestion, i, state.Questions.Count, endsAt);
                await hub.Clients.Group(groupName).SendAsync("QuestionStarted", payload);

                var remaining = endsAt - DateTimeOffset.UtcNow;
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining);
                }

                state.Phase = MatchPhase.Reveal;
                var revealEndsAt = DateTimeOffset.UtcNow.Add(_revealDuration);
                state.PhaseEndsAtUtc = revealEndsAt;

                var scoreboard = state.BuildScoreboard();
                var revealPayload = state.GameMode.ToRevealPayload(current.MatchQuestion, i, revealEndsAt, scoreboard);
                await hub.Clients.Group(groupName).SendAsync("QuestionRevealed", revealPayload);

                await Task.Delay(_revealDuration);
            }

            state.Phase = MatchPhase.Finished;
            await FinalizeMatchAsync(state);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Match loop for room {RoomId} failed", roomId);
            _matches.TryRemove(roomId, out _);
        }
    }

    private async Task FinalizeMatchAsync(MatchRuntimeState state)
    {
        var ranked = state.Players.Values
            .OrderByDescending(p => p.Score)
            .Select((p, index) => new RankingEntry(p.UserId, p.DisplayName, p.Score, index + 1))
            .ToList();

        var review = state.Questions
            .Select((q, index) => new QuestionReviewEntry(
                index,
                q.MatchQuestion.Question!.Text,
                q.MatchQuestion.Question!.Options,
                q.MatchQuestion.Question!.CorrectOptionIndex,
                q.MatchQuestion.Question!.Explanation))
            .ToList();

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BrainArenaDbContext>();

            var match = await db.Matches.Include(m => m.Players).FirstAsync(m => m.Id == state.MatchId);
            match.Status = MatchStatus.Finished;
            match.EndedAt = DateTimeOffset.UtcNow;

            foreach (var rankEntry in ranked)
            {
                var matchPlayer = match.Players.First(p => p.UserId == rankEntry.UserId);
                matchPlayer.Score = rankEntry.Score;
                matchPlayer.FinalRank = rankEntry.Rank;
            }

            var room = await db.Rooms.FirstAsync(r => r.Id == state.RoomId);
            room.Status = RoomStatus.Finished;

            await db.SaveChangesAsync();
        }

        var payload = new MatchEndedPayload(state.MatchId, ranked, review);
        await hub.Clients.Group(RoomHub.RoomGroupName(state.RoomId)).SendAsync("MatchEnded", payload);
        await hub.Clients.Group("lobby").SendAsync("RoomListChanged");

        _matches.TryRemove(state.RoomId, out _);
    }
}
