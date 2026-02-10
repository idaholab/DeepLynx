#!/bin/bash

set -e

SERVICE_NAME="nx-postgres"
DOCKER_IMAGE_TAG="deeplynx-db"
BACKUP_FILE="storage/deeplynx_backup_$(date +%Y%m%d_%H%M%S).sql"
NEW_VOLUME_NAME="nx_postgres_data_pg18"

echo "=== DeepLynx Nexus PostgreSQL Migration Script ==="
echo ""

# Create storage directory if it doesn't exist
mkdir -p storage

# Find the actual container name (handles docker-compose prefixes)
CONTAINER_NAME=$(docker ps -a --format '{{.Names}}' | grep -E ".*-${SERVICE_NAME}-[0-9]+$" | head -n 1)

if [ -z "${CONTAINER_NAME}" ]; then
    echo "Error: Could not find container matching pattern '*-${SERVICE_NAME}-*'"
    echo "Available containers:"
    docker ps -a --format '{{.Names}}'
    exit 1
fi

echo "Found container: ${CONTAINER_NAME}"

# Get the volume name
VOLUME_NAME=$(docker inspect ${CONTAINER_NAME} --format '{{range .Mounts}}{{if or (eq .Destination "/var/lib/postgresql/data") (eq .Destination "/var/lib/postgresql")}}{{.Name}}{{end}}{{end}}' 2>/dev/null)

if [ -z "${VOLUME_NAME}" ]; then
    echo "Error: Could not find volume for container ${CONTAINER_NAME}"
    exit 1
fi

echo "Found volume: ${VOLUME_NAME}"

# Get the container's network
NETWORK_NAME=$(docker inspect ${CONTAINER_NAME} --format '{{range $key, $value := .NetworkSettings.Networks}}{{$key}}{{end}}' 2>/dev/null)

if [ -z "${NETWORK_NAME}" ]; then
    # Fallback to detecting network from docker-compose
    NETWORK_NAME=$(docker network ls --format '{{.Name}}' | grep -E ".*-network$" | head -n 1)
fi

if [ -z "${NETWORK_NAME}" ]; then
    echo "Error: Could not determine network name"
    exit 1
fi

echo "Using network: ${NETWORK_NAME}"
echo ""

# Step 1: Create backup
echo "Step 1: Creating database backup from ${CONTAINER_NAME}..."

# Ensure container is running
if [ "$(docker inspect -f '{{.State.Running}}' ${CONTAINER_NAME} 2>/dev/null)" != "true" ]; then
    echo "Starting container ${CONTAINER_NAME}..."
    docker start ${CONTAINER_NAME}
    sleep 5
fi

# Test connection
echo "Testing database connection..."
if ! docker exec ${CONTAINER_NAME} psql -U postgres -c "SELECT version();" > /dev/null 2>&1; then
    echo "Error: Cannot connect to PostgreSQL in container ${CONTAINER_NAME}"
    echo "Checking container logs:"
    docker logs --tail 20 ${CONTAINER_NAME}
    exit 1
fi

# Create the dump
echo "Creating backup..."
if ! docker exec ${CONTAINER_NAME} pg_dumpall -U postgres > ${BACKUP_FILE} 2>&1; then
    echo "Error: pg_dumpall failed"
    cat ${BACKUP_FILE}
    exit 1
fi

# Verify the backup file has content
if [ ! -s ${BACKUP_FILE} ]; then
    echo "Error: Backup file is empty"
    echo "Checking database:"
    docker exec ${CONTAINER_NAME} psql -U postgres -c "\l"
    exit 1
fi

echo "✓ Backup created: ${BACKUP_FILE} ($(wc -l < ${BACKUP_FILE}) lines)"
echo ""

# Step 2: Stop and remove old container
echo "Step 2: Stopping and removing old container..."
docker stop ${CONTAINER_NAME}
docker rm ${CONTAINER_NAME}
echo "✓ Old container removed"
echo ""

# Step 3: Remove old volume
echo "Step 3: Removing old database volume..."
docker volume rm ${VOLUME_NAME}
echo "✓ Old volume removed"
echo ""

# Step 4: Create new volume (PG18 uses /var/lib/postgresql instead of /var/lib/postgresql/data)
echo "Step 4: Creating new database volume..."
# remove the volume if exists before creating anew
docker volume rm ${NEW_VOLUME_NAME} 2>/dev/null || true
docker volume create ${NEW_VOLUME_NAME}
echo "✓ New volume created: ${NEW_VOLUME_NAME}"
echo ""

# Step 5: Build new Postgres 18 image
echo "Step 5: Building new PostgreSQL 18 image with pgvector..."
docker build \
    -t ${DOCKER_IMAGE_TAG} \
    -f ./Dockerfiles/database/Dockerfile.local .
echo "✓ New docker image created: ${DOCKER_IMAGE_TAG}"
echo ""

# Step 6: Start new Postgres 18 container (note: mount at /var/lib/postgresql, not /var/lib/postgresql/data)
echo "Step 6: Starting PostgreSQL 18 container for databaase restoration..."
docker run -d \
    --name ${CONTAINER_NAME} \
    --network ${NETWORK_NAME} \
    -e POSTGRES_USER=postgres \
    -e POSTGRES_PASSWORD=nexuscore \
    -e POSTGRES_DB=deeplynx \
    -p 5432:5432 \
    -v ${NEW_VOLUME_NAME}:/var/lib/postgresql \
    ${DOCKER_IMAGE_TAG}
echo "✓ New container started"
echo ""

# Step 7: Wait for Postgres to be ready
echo "Step 7: Waiting for PostgreSQL to be ready..."
sleep 10

for i in {1..30}; do
    if docker exec ${CONTAINER_NAME} pg_isready -U postgres > /dev/null 2>&1; then
        echo "✓ PostgreSQL is ready!"
        break
    fi
    echo "Waiting... ($i/30)"
    sleep 2
done

echo ""

# Step 8: Install pgvector extension
echo "Step 8: Installing pgvector extension..."
docker exec ${CONTAINER_NAME} psql -U postgres -d deeplynx -c "CREATE EXTENSION IF NOT EXISTS vector;" > /dev/null 2>&1
echo "✓ pgvector extension installed"
echo ""

# Step 9: Restore the backup
echo "Step 9: Restoring database from backup..."
if ! cat ${BACKUP_FILE} | docker exec -i ${CONTAINER_NAME} psql -U postgres > /dev/null 2>&1; then
    echo "Error: Database restore failed"
    exit 1
fi
echo "✓ Database restored successfully"
echo ""

# Step 10: Stop and remove the container we used for data restoration
# The docker compose will create a new container using the new image
# The data volume (nx_postgres_data_pg18) is what needs to be preserved
# Which it should be as specified in docker-compose.yml
echo "Step 10: Stopping and removing database migration container..."
docker stop ${CONTAINER_NAME}
docker rm ${CONTAINER_NAME}
echo "✓ Old container removed"
echo ""

echo "=========================================="
echo "MIGRATION COMPLETE"
echo "=========================================="
echo ""
echo "✓ Database successfully upgraded to PostgreSQL 18"
echo "✓ pgvector extension is installed"
echo "✓ All data has been restored"
echo ""
echo "Backup file: ${BACKUP_FILE}"
echo ""
echo "You can now restart your services with:"
echo "  docker-compose up --build"
echo ""