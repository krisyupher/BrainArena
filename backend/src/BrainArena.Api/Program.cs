using System.Text;
using System.Text.Json.Serialization;
using BrainArena.Api.Hubs;
using BrainArena.Api.Matches;
using BrainArena.Api.Middleware;
using BrainArena.Application;
using BrainArena.Application.Abstractions;
using BrainArena.Application.Matches;
using BrainArena.Infrastructure;
using BrainArena.Infrastructure.Auth;
using BrainArena.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");
var allowedOrigin = builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:4200";

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IRoomNotifier, RoomNotifier>();
builder.Services.AddScoped<ITournamentNotifier, TournamentNotifier>();
builder.Services.AddSingleton<RoomConnectionTracker>();
builder.Services.AddSingleton<IMatchOrchestrator, MatchOrchestrator>();
builder.Services.Configure<MatchTimingOptions>(builder.Configuration.GetSection(MatchTimingOptions.SectionName));

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
        policy.WithOrigins(allowedOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            // SignalR's WebSocket handshake can't send an Authorization header, so it passes
            // the token as a query string parameter instead.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BrainArenaDbContext>();
    await db.Database.MigrateAsync();
}
await AdminSeeder.SeedAsync(app.Services);
await QuestionSeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors("AngularDev");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<RoomHub>("/hubs/room");

app.Run();

// Marker so WebApplicationFactory<Program> in BrainArena.IntegrationTests can find the entry point.
public partial class Program;
