#!/bin/bash
set -e

echo "Starting entrypoint script..."

# Wait for database to be ready
echo "Waiting for database to be ready..."
until dotnet ef database update --project /app/src --no-build 2>&1 | grep -q "Done\|already"; do
  echo "Database is unavailable - sleeping"
  sleep 2
done

echo "Database is ready!"

# Run migrations
echo "Running EF Core migrations..."
cd /app/src
dotnet ef database update --no-build

echo "Migrations completed successfully!"

# Start the application
echo "Starting application..."
cd /app
exec dotnet Amethral.Api.dll
