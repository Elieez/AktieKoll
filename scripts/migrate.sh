#!/bin/bash

# AktieKoll Database Migration Script
# This script handles database migrations for different environments

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
    echo "  local      - Run migrations on local machine"
    echo "  docker     - Run migrations in Docker container"
    echo "  production - Run migrations on production (with backup)"
    echo ""
    echo "Options:"
    echo "  --skip-backup    Skip automatic backup (not recommended for production)"
    echo "  --target <name>  Migrate to specific migration instead of latest"
    echo "  --dry-run        Show what would be done without doing it"
    echo ""
    echo "Examples:"
    echo "  $0 local                    # Run all pending migrations locally"
    echo "  $0 docker                   # Run migrations in Docker"
    echo "  $0 production               # Run migrations in production with backup"
    echo "  $0 docker --target Symbol   # Migrate to Symbol migration"
    exit 1
}

# Parse arguments
ENVIRONMENT=""
SKIP_BACKUP=false
TARGET_MIGRATION=""
DRY_RUN=false

while [[ $# -gt 0 ]]; do
    case $1 in
        local|docker|production)
            ENVIRONMENT="$1"
            shift
            ;;
        --skip-backup)
            SKIP_BACKUP=true
            shift
            ;;
        --target)
            TARGET_MIGRATION="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
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

# Create backup directory if it doesn't exist
mkdir -p "$BACKUP_DIR"

# Function to run migration locally
migrate_local() {
    print_info "Running migrations locally..."

    cd "$PROJECT_ROOT/src/AktieKoll"

    if [ "$DRY_RUN" = true ]; then
        print_info "DRY RUN: Would execute migrations"
        dotnet ef migrations list
        return 0
    fi

    if [ -n "$TARGET_MIGRATION" ]; then
        print_info "Migrating to: $TARGET_MIGRATION"
        dotnet ef database update "$TARGET_MIGRATION"
    else
        print_info "Applying all pending migrations"
        dotnet ef database update
    fi

    print_info "Migration completed successfully!"
}

# Function to run migration in Docker
migrate_docker() {
    print_info "Running migrations in Docker..."

    cd "$PROJECT_ROOT"

    # Check if containers are running
    if ! docker-compose ps | grep -q "api"; then
        print_error "Docker containers not running. Start them first with: docker-compose up -d"
        exit 1
    fi

    if [ "$DRY_RUN" = true ]; then
        print_info "DRY RUN: Would execute migrations in Docker"
        docker-compose exec api dotnet ef migrations list
        return 0
    fi

    # Backup database first (unless skipped)
    if [ "$SKIP_BACKUP" = false ]; then
        print_info "Creating backup before migration..."
        "$SCRIPT_DIR/backup-database.sh" docker
    fi

    if [ -n "$TARGET_MIGRATION" ]; then
        print_info "Migrating to: $TARGET_MIGRATION"
        docker-compose exec api dotnet ef database update "$TARGET_MIGRATION"
    else
        print_info "Applying all pending migrations"
        docker-compose exec api dotnet ef database update
    fi

    print_info "Migration completed successfully!"
}

# Function to run migration in production
migrate_production() {
    print_info "Running migrations in PRODUCTION..."
    print_warn "This will modify the production database!"

    cd "$PROJECT_ROOT"

    if [ "$DRY_RUN" = true ]; then
        print_info "DRY RUN: Would execute migrations in production"
        docker-compose exec api dotnet ef migrations list
        return 0
    fi

    # Always backup in production (unless explicitly skipped)
    if [ "$SKIP_BACKUP" = false ]; then
        print_info "Creating backup before migration..."
        "$SCRIPT_DIR/backup-database.sh" production

        print_warn "Backup created. Press ENTER to continue with migration, or CTRL+C to cancel"
        read -r
    else
        print_warn "Skipping backup! Press ENTER to continue, or CTRL+C to cancel"
        read -r
    fi

    if [ -n "$TARGET_MIGRATION" ]; then
        print_info "Migrating to: $TARGET_MIGRATION"
        docker-compose exec api dotnet ef database update "$TARGET_MIGRATION"
    else
        print_info "Applying all pending migrations"
        docker-compose exec api dotnet ef database update
    fi

    print_info "Migration completed successfully!"
    print_info "Verifying application health..."

    # Wait a moment for app to restart
    sleep 5

    # Check health endpoint
    if curl -sf http://localhost:5000/health/ready > /dev/null; then
        print_info "✓ Health check passed!"
    else
        print_error "✗ Health check failed! Check logs with: docker-compose logs api"
        exit 1
    fi
}

# Main execution
print_info "AktieKoll Database Migration Tool"
print_info "Environment: $ENVIRONMENT"

case $ENVIRONMENT in
    local)
        migrate_local
        ;;
    docker)
        migrate_docker
        ;;
    production)
        migrate_production
        ;;
esac

print_info "Done!"
