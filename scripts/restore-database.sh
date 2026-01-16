#!/bin/bash

# AktieKoll Database Restore Script
# This script restores the PostgreSQL database from a backup

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Function to print colored output
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to show usage
usage() {
    echo "Usage: $0 <environment> <backup_file>"
    echo ""
    echo "Environments:"
    echo "  local      - Restore to local PostgreSQL database"
    echo "  docker     - Restore to Docker container database"
    echo "  production - Restore to production database"
    echo ""
    echo "Examples:"
    echo "  $0 docker backups/aktiekoll_2025-01-15_10-30-00.sql"
    echo "  $0 docker backups/aktiekoll_2025-01-15_10-30-00.sql.gz"
    echo "  $0 production backup.sql"
    echo ""
    echo "WARNING: This will DROP and recreate the database!"
    exit 1
}

# Parse arguments
if [ $# -lt 2 ]; then
    print_error "Missing required arguments"
    usage
fi

ENVIRONMENT=$1
BACKUP_FILE=$2

# Validate environment
case $ENVIRONMENT in
    local|docker|production)
        ;;
    *)
        print_error "Invalid environment: $ENVIRONMENT"
        usage
        ;;
esac

# Check if backup file exists
if [ ! -f "$BACKUP_FILE" ]; then
    print_error "Backup file not found: $BACKUP_FILE"
    exit 1
fi

# Detect if file is compressed
IS_COMPRESSED=false
if [[ "$BACKUP_FILE" == *.gz ]]; then
    IS_COMPRESSED=true
fi

# Function to restore local database
restore_local() {
    print_warn "This will DESTROY the current local database and restore from backup!"
    print_warn "Backup file: $BACKUP_FILE"
    echo -n "Are you sure? Type 'yes' to continue: "
    read -r confirmation

    if [ "$confirmation" != "yes" ]; then
        print_info "Restore cancelled"
        exit 0
    fi

    print_info "Dropping existing database..."
    dropdb -U postgres --if-exists aktiekoll

    print_info "Creating new database..."
    createdb -U postgres aktiekoll

    print_info "Restoring from backup..."
    if [ "$IS_COMPRESSED" = true ]; then
        gunzip -c "$BACKUP_FILE" | psql -U postgres aktiekoll
    else
        psql -U postgres aktiekoll < "$BACKUP_FILE"
    fi

    print_info "✓ Database restored successfully!"
}

# Function to restore Docker database
restore_docker() {
    cd "$PROJECT_ROOT"

    # Check if containers are running
    if ! docker-compose ps | grep -q "postgres"; then
        print_error "PostgreSQL container not running. Start it first with: docker-compose up -d postgres"
        exit 1
    fi

    print_warn "This will DESTROY the current Docker database and restore from backup!"
    print_warn "Backup file: $BACKUP_FILE"
    echo -n "Are you sure? Type 'yes' to continue: "
    read -r confirmation

    if [ "$confirmation" != "yes" ]; then
        print_info "Restore cancelled"
        exit 0
    fi

    print_info "Stopping API container..."
    docker-compose stop api

    print_info "Dropping existing database..."
    docker-compose exec -T postgres psql -U aktiekoll_user postgres -c "DROP DATABASE IF EXISTS aktiekoll;"

    print_info "Creating new database..."
    docker-compose exec -T postgres psql -U aktiekoll_user postgres -c "CREATE DATABASE aktiekoll;"

    print_info "Restoring from backup..."
    if [ "$IS_COMPRESSED" = true ]; then
        gunzip -c "$BACKUP_FILE" | docker-compose exec -T postgres psql -U aktiekoll_user aktiekoll
    else
        cat "$BACKUP_FILE" | docker-compose exec -T postgres psql -U aktiekoll_user aktiekoll
    fi

    print_info "Starting API container..."
    docker-compose start api

    # Wait for health check
    print_info "Waiting for application to be ready..."
    sleep 5

    if curl -sf http://localhost:5000/health/ready > /dev/null; then
        print_info "✓ Database restored and application is healthy!"
    else
        print_warn "Application may not be healthy. Check logs with: docker-compose logs api"
    fi
}

# Function to restore production database
restore_production() {
    cd "$PROJECT_ROOT"

    print_error "==================== PRODUCTION RESTORE ===================="
    print_warn "This will DESTROY the current PRODUCTION database!"
    print_warn "Backup file: $BACKUP_FILE"
    print_warn ""
    print_warn "Before proceeding:"
    print_warn "1. Create a backup of the current database"
    print_warn "2. Notify all stakeholders"
    print_warn "3. Put application in maintenance mode"
    print_warn ""
    echo -n "Type 'RESTORE PRODUCTION' to continue: "
    read -r confirmation

    if [ "$confirmation" != "RESTORE PRODUCTION" ]; then
        print_info "Restore cancelled"
        exit 0
    fi

    # Create a backup before restoring
    print_info "Creating backup of current production database..."
    "$SCRIPT_DIR/backup-database.sh" production --compress

    print_info "Stopping API container..."
    docker-compose stop api

    print_info "Dropping existing database..."
    docker-compose exec -T postgres psql -U aktiekoll_user postgres -c "DROP DATABASE IF EXISTS aktiekoll;"

    print_info "Creating new database..."
    docker-compose exec -T postgres psql -U aktiekoll_user postgres -c "CREATE DATABASE aktiekoll;"

    print_info "Restoring from backup..."
    if [ "$IS_COMPRESSED" = true ]; then
        gunzip -c "$BACKUP_FILE" | docker-compose exec -T postgres psql -U aktiekoll_user aktiekoll
    else
        cat "$BACKUP_FILE" | docker-compose exec -T postgres psql -U aktiekoll_user aktiekoll
    fi

    print_info "Starting API container..."
    docker-compose start api

    # Wait for health check
    print_info "Waiting for application to be ready..."
    sleep 10

    if curl -sf http://localhost:5000/health/ready > /dev/null; then
        print_info "✓ Production database restored successfully!"
        print_info "✓ Application is healthy!"
    else
        print_error "✗ Application health check failed!"
        print_error "Check logs immediately with: docker-compose logs api"
        exit 1
    fi

    print_info "Restore completed. Please verify the application thoroughly."
}

# Main execution
print_info "AktieKoll Database Restore Tool"
print_info "Environment: $ENVIRONMENT"
print_info "Backup file: $BACKUP_FILE"

case $ENVIRONMENT in
    local)
        restore_local
        ;;
    docker)
        restore_docker
        ;;
    production)
        restore_production
        ;;
esac

print_info "Done!"
