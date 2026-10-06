# Build context: backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only, so the layer is cached until a dependency changes.
COPY src/BrainArena.Domain/BrainArena.Domain.csproj src/BrainArena.Domain/
COPY src/BrainArena.Application/BrainArena.Application.csproj src/BrainArena.Application/
COPY src/BrainArena.Infrastructure/BrainArena.Infrastructure.csproj src/BrainArena.Infrastructure/
COPY src/BrainArena.Api/BrainArena.Api.csproj src/BrainArena.Api/
RUN dotnet restore src/BrainArena.Api/BrainArena.Api.csproj

COPY src/ src/
RUN dotnet publish src/BrainArena.Api/BrainArena.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "BrainArena.Api.dll"]
