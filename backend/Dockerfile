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

# Runtime stage (minimal ASP.NET runtime image)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Published output
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SwAIvyn.dll"]
