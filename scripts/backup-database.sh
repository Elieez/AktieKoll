#!/bin/bash

# AktieKoll Database Backup Script
# This script creates backups of the PostgreSQL database

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BACKUP_DIR="$PROJECT_ROOT/backups"
TIMESTAMP=$(date +"%Y-%m-%d_%H-%M-%S")

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
    echo "Usage: $0 <environment> [options]"
    echo ""
    echo "Environments:"
    echo "  local      - Backup local PostgreSQL database"
    echo "  docker     - Backup database from Docker container"
    echo "  production - Backup production database"
    echo ""
    echo "Options:"
    echo "  --output <file>  Specify output file (default: backups/aktiekoll_TIMESTAMP.sql)"
    echo "  --compress       Compress backup with gzip (.sql.gz)"
    echo ""
    echo "Examples:"
    echo "  $0 docker                         # Backup Docker database"
    echo "  $0 docker --compress              # Backup and compress"
    echo "  $0 production --output backup.sql # Specific output file"
    exit 1
}

# Parse arguments
ENVIRONMENT=""
OUTPUT_FILE=""
COMPRESS=false

while [[ $# -gt 0 ]]; do
    case $1 in
        local|docker|production)
            ENVIRONMENT="$1"
            shift
            ;;
        --output)
            OUTPUT_FILE="$2"
            shift 2
            ;;
        --compress)
            COMPRESS=true
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            print_error "Unknown option: $1"
            usage
            ;;
    esac
done

# Validate environment
if [ -z "$ENVIRONMENT" ]; then
    print_error "Environment is required"
    usage
fi

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Set default output file if not specified
if [ -z "$OUTPUT_FILE" ]; then
    if [ "$COMPRESS" = true ]; then
        OUTPUT_FILE="$BACKUP_DIR/aktiekoll_${TIMESTAMP}.sql.gz"
    else
        OUTPUT_FILE="$BACKUP_DIR/aktiekoll_${TIMESTAMP}.sql"
    fi
fi

# Function to backup local database
backup_local() {
    print_info "Backing up local database..."

    if [ "$COMPRESS" = true ]; then
        print_info "Creating compressed backup: $OUTPUT_FILE"
        pg_dump -U postgres aktiekoll | gzip > "$OUTPUT_FILE"
    else
        print_info "Creating backup: $OUTPUT_FILE"
        pg_dump -U postgres aktiekoll > "$OUTPUT_FILE"
    fi

    local size=$(du -h "$OUTPUT_FILE" | cut -f1)
    print_info "✓ Backup created successfully! Size: $size"
}

# Function to backup Docker database
backup_docker() {
    print_info "Backing up Docker database..."

    cd "$PROJECT_ROOT"

    # Check if containers are running
    if ! docker-compose ps | grep -q "postgres"; then
        print_error "PostgreSQL container not running. Start it first with: docker-compose up -d postgres"
        exit 1
    fi

    if [ "$COMPRESS" = true ]; then
        print_info "Creating compressed backup: $OUTPUT_FILE"
        docker-compose exec -T postgres pg_dump -U aktiekoll_user aktiekoll | gzip > "$OUTPUT_FILE"
    else
        print_info "Creating backup: $OUTPUT_FILE"
        docker-compose exec -T postgres pg_dump -U aktiekoll_user aktiekoll > "$OUTPUT_FILE"
    fi

    local size=$(du -h "$OUTPUT_FILE" | cut -f1)
    print_info "✓ Backup created successfully! Size: $size"
}

# Function to backup production database
backup_production() {
    print_info "Backing up PRODUCTION database..."
    print_warn "This is a production backup!"

    cd "$PROJECT_ROOT"

    # Check if containers are running
    if ! docker-compose ps | grep -q "postgres"; then
        print_error "PostgreSQL container not running. Check production environment!"
        exit 1
    fi

    if [ "$COMPRESS" = true ]; then
        print_info "Creating compressed backup: $OUTPUT_FILE"
        docker-compose exec -T postgres pg_dump -U aktiekoll_user aktiekoll | gzip > "$OUTPUT_FILE"
    else
        print_info "Creating backup: $OUTPUT_FILE"
        docker-compose exec -T postgres pg_dump -U aktiekoll_user aktiekoll > "$OUTPUT_FILE"
    fi

    local size=$(du -h "$OUTPUT_FILE" | cut -f1)
    print_info "✓ Production backup created successfully! Size: $size"

    # Store backup metadata
    echo "Backup created: $(date)" >> "$OUTPUT_FILE.meta"
    echo "Environment: $ENVIRONMENT" >> "$OUTPUT_FILE.meta"
    echo "Size: $size" >> "$OUTPUT_FILE.meta"

    print_warn "IMPORTANT: Copy this backup to a secure off-site location!"
}

# Function to cleanup old backups
cleanup_old_backups() {
    print_info "Cleaning up old backups..."

    # Keep last 7 daily backups
    cd "$BACKUP_DIR"
    ls -t aktiekoll_*.sql* 2>/dev/null | tail -n +8 | xargs -r rm --

    print_info "✓ Old backups cleaned up (keeping last 7)"
}

# Main execution
print_info "AktieKoll Database Backup Tool"
print_info "Environment: $ENVIRONMENT"

case $ENVIRONMENT in
    local)
        backup_local
        ;;
    docker)
        backup_docker
        ;;
    production)
        backup_production
        ;;
esac

# Cleanup old backups (only for automated backups)
if [ -z "$OUTPUT_FILE" ] || [[ "$OUTPUT_FILE" == *"$BACKUP_DIR"* ]]; then
    cleanup_old_backups
fi

print_info "Backup location: $OUTPUT_FILE"
print_info "Done!"
