using BrainArena.Application.Abstractions;
using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;

namespace BrainArena.Application.Tests.Fakes;

public class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _users = new();

    public User Seed(string displayName = "Player")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@example.com",
            DisplayName = displayName,
            PasswordHash = "irrelevant",
            Role = UserRole.Player,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _users[user.Id] = user;
        return user;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(_users.Values.FirstOrDefault(u => u.Email == email));

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_users.GetValueOrDefault(id));

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public class FakeRoomRepository : IRoomRepository
{
    private readonly Dictionary<Guid, Room> _rooms = new();

    public Task<List<Room>> GetOpenRoomsAsync(CancellationToken ct = default) =>
        Task.FromResult(_rooms.Values
            .Where(r => !r.IsPrivate && (r.Status == RoomStatus.Waiting || r.Status == RoomStatus.InProgress))
            .ToList());

    public Task<Room?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_rooms.GetValueOrDefault(id));

    public Task<Room?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default) =>
        Task.FromResult(_rooms.Values.FirstOrDefault(r => r.ShareCode == shareCode));

    public Task AddAsync(Room room, CancellationToken ct = default)
    {
        _rooms[room.Id] = room;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public class FakeRoomNotifier : IRoomNotifier
{
    public int NotificationCount { get; private set; }
    public int RoomUpdatedCount { get; private set; }

    public Task NotifyRoomListChangedAsync(CancellationToken ct = default)
    {
        NotificationCount++;
        return Task.CompletedTask;
    }

    public Task NotifyRoomUpdatedAsync(Guid roomId, CancellationToken ct = default)
    {
        RoomUpdatedCount++;
        return Task.CompletedTask;
    }
}

public class FakeQuestionRepository : IQuestionRepository
{
    private readonly Dictionary<Guid, Question> _questions = new();

    public Task<List<Question>> GetRandomByTopicAsync(RoomTopic topic, int count, CancellationToken ct = default) =>
        Task.FromResult(_questions.Values.Where(q => q.Topic == topic).Take(count).ToList());

    public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(_questions.Count);

    public Task<List<Question>> GetAllAsync(RoomTopic? topic, CancellationToken ct = default) =>
        Task.FromResult(_questions.Values.Where(q => topic == null || q.Topic == topic).ToList());

    public Task<Question?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_questions.GetValueOrDefault(id));

    public Task AddAsync(Question question, CancellationToken ct = default)
    {
        _questions[question.Id] = question;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

public class FakeChatRepository : IChatRepository
{
    private readonly Dictionary<Guid, ChatMessage> _messages = new();

    public Task<List<ChatMessage>> GetByRoomIdAsync(Guid roomId, CancellationToken ct = default) =>
        Task.FromResult(_messages.Values.Where(m => m.RoomId == roomId).OrderBy(m => m.SentAt).ToList());

    public Task<ChatMessage?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_messages.GetValueOrDefault(id));

    public Task AddAsync(ChatMessage message, CancellationToken ct = default)
    {
        _messages[message.Id] = message;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Always allows sends — used by tests that aren't specifically exercising the rate limit.</summary>
public class AlwaysAllowRateLimiter : IChatRateLimiter
{
    public bool TryConsume(Guid userId) => true;
}

public class FakeMatchOrchestrator : IMatchOrchestrator
{
    public int AutoStartAttempts { get; private set; }
    public Guid? LastAutoStartRoomId { get; private set; }

    public bool HasActiveMatch(Guid roomId) => false;

    public Task TryAutoStartAsync(Guid roomId, CancellationToken ct = default)
    {
        AutoStartAttempts++;
        LastAutoStartRoomId = roomId;
        return Task.CompletedTask;
    }

    public Task StartNowAsync(Guid roomId, Guid requestingUserId, CancellationToken ct = default) => Task.CompletedTask;

    public Task SubmitAnswerAsync(Guid roomId, Guid userId, Guid matchQuestionId, BrainArena.Application.Matches.SubmittedAnswer answer) => Task.CompletedTask;

    public BrainArena.Application.Matches.MatchResyncPayload? Join(Guid roomId, Guid userId) => null;

    public BrainArena.Application.Matches.MatchSpectatorSyncPayload? Snapshot(Guid roomId) => null;

    public void MarkDisconnected(Guid roomId, Guid userId)
    {
    }
}
