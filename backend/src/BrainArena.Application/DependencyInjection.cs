using BrainArena.Application.Abstractions;
using BrainArena.Application.Auth;
using BrainArena.Application.Chat;
using BrainArena.Application.Matches;
using BrainArena.Application.Questions;
using BrainArena.Application.Rooms;
using Microsoft.Extensions.DependencyInjection;

namespace BrainArena.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IChatService, ChatService>();

        services.AddSingleton<IGameMode, MultipleChoiceGameMode>();
        services.AddSingleton<IGameModeRegistry, GameModeRegistry>();
        services.AddSingleton<IChatRateLimiter, ChatRateLimiter>();

        return services;
    }
}
