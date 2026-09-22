using BrainArena.Application.Abstractions;
using BrainArena.Application.Matches;
using BrainArena.Infrastructure.Auth;
using BrainArena.Infrastructure.Data;
using BrainArena.Infrastructure.Matches;
using BrainArena.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BrainArena.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<BrainArenaDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("Default")));

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<ITournamentRepository, TournamentRepository>();
        services.AddScoped<IMatchResultsService, MatchResultsService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
