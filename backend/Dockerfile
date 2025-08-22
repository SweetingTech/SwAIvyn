# Windows containers: make sure Docker Desktop is set to "Switch to Windows containers..."
# Use ltsc2022 to match modern hosts. If your host is older/newer, pick the tag that matches.
# Check your host with:  winver  and  docker info

FROM mcr.microsoft.com/dotnet/aspnet:8.0-nanoserver-ltsc2022 AS base
WORKDIR /app
# Kestrel will listen on these. Map with -p at runtime or in compose.
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0-nanoserver-ltsc2022 AS build
WORKDIR /src

# Copy project file and restore (leverages Docker layer caching)
COPY ["backend/SwAIvyn.csproj", "backend/"]
RUN dotnet restore "backend/SwAIvyn.csproj"

# Copy the rest and build
COPY . .
WORKDIR "/src/backend"
RUN dotnet build "SwAIvyn.csproj" -c Release -o /app/build

FROM build AS publish
# Framework-dependent publish is fine because aspnet base provides the runtime.
# If you want self-contained, see the note below.
RUN dotnet publish "SwAIvyn.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish ./

# Optional: if your native DLLs end up in subfolders, ensure they’re on PATH
# ENV PATH="C:\\app;${PATH}"

ENTRYPOINT ["dotnet", "SwAIvyn.dll"]
