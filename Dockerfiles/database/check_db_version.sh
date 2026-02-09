#!/bin/bash

set -e

REQUIRED_PG_VERSION="18"
DB_HOST="${DB_HOST:-nx-postgres}"
DB_USER="${DB_USER:-postgres}"
DB_PASSWORD="${DB_PASSWORD:-nexuscore}"
DB_NAME="${DB_NAME:-deeplynx}"

echo "=== Database Version Check ==="
echo ""

# Wait for database to be available
echo "Waiting for database at ${DB_HOST}..."
for i in {1..30}; do
    if PGPASSWORD="${DB_PASSWORD}" psql -h "${DB_HOST}" -U "${DB_USER}" -d postgres -c "SELECT 1;" > /dev/null 2>&1; then
        echo "✓ Database is accessible"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "Error: Database not accessible after 30 attempts"
        exit 1
    fi
    sleep 2
done

# Get current PostgreSQL version
CURRENT_PG_VERSION=$(PGPASSWORD="${DB_PASSWORD}" psql -h "${DB_HOST}" -U "${DB_USER}" -d postgres -t -c "SHOW server_version;" 2>/dev/null | awk '{print $1}' | cut -d. -f1)

if [ -z "${CURRENT_PG_VERSION}" ]; then
    echo "Error: Could not determine PostgreSQL version"
    exit 1
fi

echo "Current PostgreSQL version: ${CURRENT_PG_VERSION}"
echo "Required PostgreSQL version: ${REQUIRED_PG_VERSION}"
echo ""

# Check for pgvector extension
echo "Checking for pgvector extension..."
PGVECTOR_INSTALLED=$(PGPASSWORD="${DB_PASSWORD}" psql -h "${DB_HOST}" -U "${DB_USER}" -d "${DB_NAME}" -t -c "SELECT COUNT(*) FROM pg_extension WHERE extname='vector';" 2>/dev/null | tr -d ' ')

NEEDS_UPGRADE=false

if [ "${PGVECTOR_INSTALLED}" = "0" ]; then
    echo "pgvector extension not found. Attempting to install..."
    if PGPASSWORD="${DB_PASSWORD}" psql -h "${DB_HOST}" -U "${DB_USER}" -d "${DB_NAME}" -c "CREATE EXTENSION IF NOT EXISTS vector;" > /dev/null 2>&1; then
        echo "✓ pgvector extension installed successfully"
    else
        echo "⚠️  Could not install pgvector extension"
        echo "This indicates an upgrade is needed."
        NEEDS_UPGRADE=true
    fi
else
    echo "✓ pgvector extension is already installed"
fi

# Check if upgrade is needed
if [ "${CURRENT_PG_VERSION}" -lt "${REQUIRED_PG_VERSION}" ] || [ "${NEEDS_UPGRADE}" = true ]; then
    echo "=========================================="
    echo "DATABASE UPGRADE REQUIRED"
    echo "=========================================="
    echo ""
    echo "Your dockerized database image is out of date."
    echo ""
    echo "To upgrade your database, do the following from the root Nexus directory:"
    echo ""
    echo "1. Stop all services:"
    echo "   docker compose down"
    echo ""
    echo "2. Run the migration script:"
    echo "   ./migrate_postgres.sh"
    echo ""
    echo "3. Restart services:"
    echo "   docker compose up --build"
    echo ""
    echo "The migration script will safely backup and upgrade your database."
    echo ""
    exit 1
fi

echo ""
echo "✓ Database version check passed!"
echo ""
exit 0