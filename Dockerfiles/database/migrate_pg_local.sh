#!/bin/bash

set -e

CONTAINER_NAME="DeepLynx"
DOCKER_IMAGE_TAG="deeplynx-db"
NEW_VOLUME_NAME="deeplynx_pgdata"
OLD_VOLUME_NAME=""  # Will be set dynamically
BACKUP_FILE="storage/deeplynx_backup_$(date +%Y%m%d_%H%M%S).sql"

echo "=== DeepLynx Nexus PostgreSQL Migration Script ( -> 18) ==="
echo ""

# Stop any docker containers competing on 5432
docker ps --filter "publish=5432" -q | xargs docker stop

# Check if docker container exists
if ! docker inspect ${CONTAINER_NAME} > /dev/null 2>&1; then
    echo "Error: Container '${CONTAINER_NAME}' not found."
    exit 1
fi

# 1. Figure out volume name for given container and rename to ${NAME}_OLD
echo "Step 1: Identifying current volume..."
OLD_VOLUME_NAME=$(docker inspect ${CONTAINER_NAME} --format '{{range .Mounts}}{{if or (eq .Destination "/var/lib/postgresql/data") (eq .Destination "/var/lib/postgresql")}}{{.Name}}{{end}}{{end}}')

if [ -z "$OLD_VOLUME_NAME" ]; then
    echo "Error: Could not find mounted volume for PostgreSQL data."
    exit 1
fi

echo "Found volume: ${OLD_VOLUME_NAME}"
echo ""

NEW_CONTAINER_NAME="${CONTAINER_NAME}"
OLD_CONTAINER_NAME="${CONTAINER_NAME}_OLD"

# Check if old backup exists and remove it
if docker ps -a --format '{{.Names}}' | grep -q "^${OLD_CONTAINER_NAME}$"; then
    echo "Removing previous backup container ${OLD_CONTAINER_NAME}"
    docker rm -f "${OLD_CONTAINER_NAME}"
fi

# Stop the container first (if running)
docker stop "${CONTAINER_NAME}" || true

# Rename the container
docker rename "${CONTAINER_NAME}" "${OLD_CONTAINER_NAME}"

echo "Renamed current container from ${NEW_CONTAINER_NAME} to ${OLD_CONTAINER_NAME}"
echo ""

# 2. Create backup dump from Postgres 16
echo "Step 2: Creating database dump from ${OLD_CONTAINER_NAME}..."

# Check if container is running
if [ "$(docker inspect -f '{{.State.Running}}' ${OLD_CONTAINER_NAME})" != "true" ]; then
    echo "Starting container ${OLD_CONTAINER_NAME}..."
    docker start ${OLD_CONTAINER_NAME}
    sleep 5
fi

# Test connection first
echo "Testing database connection..."
if ! docker exec ${OLD_CONTAINER_NAME} psql -U postgres -c "SELECT version();" > /dev/null 2>&1; then
    echo "Error: Cannot connect to PostgreSQL in container ${OLD_CONTAINER_NAME}"
    echo "Checking container logs:"
    docker logs --tail 20 ${OLD_CONTAINER_NAME}
    exit 1
fi

mkdir -p storage

# Create the dump with error checking
echo "Creating dump..."
if ! docker exec ${OLD_CONTAINER_NAME} pg_dumpall -U postgres > ${BACKUP_FILE} 2>&1; then
    echo "Error: pg_dumpall failed"
    cat ${BACKUP_FILE}
    exit 1
fi

# Verify the backup file has content
if [ ! -s ${BACKUP_FILE} ]; then
    echo "Error: Backup file is empty"
    echo "Trying to manually check database:"
    docker exec ${OLD_CONTAINER_NAME} psql -U postgres -c "\l"
    exit 1
fi

echo "Backup created: ${BACKUP_FILE} ($(wc -l < ${BACKUP_FILE}) lines)"
echo ""

# 3. Build new Postgres 18 image
echo "Step 3: Building new Docker image with Postgres 18..."
docker build \
    -t ${DOCKER_IMAGE_TAG} \
    -f ./Dockerfiles/database/Dockerfile.local .
echo "New docker image created: ${DOCKER_IMAGE_TAG}"
echo ""

# 4. Stop and remove old container
echo "Step 4: Stopping old container..."
docker stop ${OLD_CONTAINER_NAME}
echo ""

# 5. Remove new volume and container if they exist to ensure clean state
echo "Step 5: Preparing new volume..."
if [ "$NEW_VOLUME_NAME" = "$OLD_VOLUME_NAME" ]; then
    NEW_VOLUME_NAME="${NEW_VOLUME_NAME}_new"
    echo "Volume name conflict - using ${NEW_VOLUME_NAME}"
fi

if [ "$NEW_CONTAINER_NAME" = "$OLD_CONTAINER_NAME" ]; then
    NEW_CONTAINER_NAME="${NEW_CONTAINER_NAME}_new"
    echo "Volume name conflict - using ${NEW_CONTAINER_NAME}"
fi

docker volume rm ${NEW_VOLUME_NAME} 2>/dev/null || true
docker volume create ${NEW_VOLUME_NAME} 2>/dev/null || true
echo "Created clean volume: ${NEW_VOLUME_NAME}"
docker rm -v ${NEW_CONTAINER_NAME} 2>/dev/null || true
echo ""

# 6. Start new Postgres 18 container
echo "Step 6: Starting new Postgres 18 container..."
docker run -d \
    --name ${NEW_CONTAINER_NAME} \
    -e POSTGRES_PASSWORD=postgres \
    -e POSTGRES_DB=deeplynx \
    -e POSTGRES_USER=postgres \
    -v ${NEW_VOLUME_NAME}:/var/lib/postgresql \
    -p 5432:5432 \
    ${DOCKER_IMAGE_TAG}
echo ""

# Wait for Postgres to be ready
echo "Step 7: Waiting for PostgreSQL to be ready..."
sleep 10
for i in {1..30}; do
    if docker exec ${NEW_CONTAINER_NAME} pg_isready -U postgres > /dev/null 2>&1; then
        echo "PostgreSQL is ready!"
        break
    fi
    echo "Waiting... ($i/30)"
    sleep 2
done
echo ""

# Step 8: Install pgvector extension
echo "Step 8: Installing pgvector extension..."
docker exec ${NEW_CONTAINER_NAME} psql -U postgres -d deeplynx -c "CREATE EXTENSION IF NOT EXISTS vector;" > /dev/null 2>&1
echo "✓ pgvector extension installed"
echo ""

# Step 9: Restore the backup
echo "Step 9: Restoring database from backup..."
if ! cat ${BACKUP_FILE} | docker exec -i ${NEW_CONTAINER_NAME} psql -U postgres > /dev/null 2>&1; then
    echo "Error: Database restore failed"
    exit 1
fi
echo "✓ Database restored successfully"
echo ""

echo "=== Migration Complete ==="
echo "Backup file: ${BACKUP_FILE}"
echo "Old volume: ${OLD_VOLUME_NAME} (can be removed after verification)"
echo "New volume: ${NEW_VOLUME_NAME}"
echo "Old container: ${OLD_CONTAINER_NAME} (can be removed after verification)"
echo "New container: ${NEW_CONTAINER_NAME}"
echo ""
echo "Test your application, then remove the old container (and its associated volume) with:"
echo "  docker rm -v ${OLD_CONTAINER_NAME}"
