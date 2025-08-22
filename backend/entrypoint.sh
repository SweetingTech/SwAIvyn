#!/bin/bash
set -e

# Apply database migrations
echo "Running migrations..."
dotnet ef database update --no-build --project SwAIvyn.csproj

# Start the application
echo "Starting SwAIvyn backend..."
dotnet SwAIvyn.dll
