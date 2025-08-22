## SwAIvyn backend Dockerfile
# Uses Linux-based .NET images so it can run alongside other services

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["SwAIvyn.csproj", "./"]
RUN dotnet restore "SwAIvyn.csproj"

# Copy source and publish
COPY . .
RUN dotnet publish "SwAIvyn.csproj" -c Release -o /app/publish

# Runtime stage (uses SDK to allow EF migrations)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Project file and migrations needed for dotnet-ef
COPY --from=build /src/SwAIvyn.csproj ./
COPY --from=build /src/Migrations ./Migrations

# Published output
COPY --from=build /app/publish .

# Entrypoint runs migrations then starts app
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
