#!/bin/bash
set -e

echo "Starting entrypoint script..."

# Wait for database to be ready
echo "Waiting for database to be ready..."
until ./efbundle; do
  echo "Database is unavailable - sleeping"
  sleep 2
done

echo "Database is ready!"

# Run migrations (already done by the check above, but good to be explicit if needed, though bundle does it)
echo "Ensuring migrations are applied..."
./efbundle

echo "Migrations completed successfully!"

# Start the application
echo "Starting application..."
cd /app
exec dotnet Amethral.Api.dll
