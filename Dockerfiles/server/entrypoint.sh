#!/bin/sh
set -e

echo "Waiting for PostgreSQL to be ready..."

export PGPASSWORD="$POSTGRES_PASSWORD"
until pg_isready -h "$POSTGRES_DB_HOST" -U "$POSTGRES_USER"; do
  sleep 2
done

echo "Checking for existence of the deeplynx database"
if psql -h "$POSTGRES_DB_HOST" -U "$POSTGRES_USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='deeplynx'" | grep -q 1; then
  echo "Database 'deeplynx' already exists, skipping creation."
else
  echo "Database 'deeplynx' does not exist, creating now."
  psql -h "$POSTGRES_DB_HOST" -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" -d postgres <<EOSQL
    CREATE DATABASE deeplynx;
EOSQL
fi

# Install pgvector extension
echo "Ensuring pgvector extension is installed..."
psql -h "$POSTGRES_DB_HOST" -U "$POSTGRES_USER" -d deeplynx -c "CREATE EXTENSION IF NOT EXISTS vector;" || true

# Execute the dotnet application
dotnet deeplynx.api.dll --urls http://*:5000