#!/bin/bash

# AktieKoll Migration Check Script
# This script checks the status of database migrations

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
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
    echo "Usage: $0 <environment>"
    echo ""
    echo "Environments:"
    echo "  local      - Check local database migrations"
    echo "  docker     - Check Docker database migrations"
    echo "  production - Check production database migrations"
    echo ""
    echo "Examples:"
    echo "  $0 docker      # Check Docker migration status"
    echo "  $0 production  # Check production migration status"
    exit 1
}

# Parse arguments
if [ $# -lt 1 ]; then
    print_error "Missing required environment argument"
    usage
fi

ENVIRONMENT=$1

# Validate environment
case $ENVIRONMENT in
    local|docker|production)
        ;;
    *)
        print_error "Invalid environment: $ENVIRONMENT"
        usage
        ;;
esac

# Function to check local migrations
check_local() {
    print_info "Checking local database migrations..."
    echo ""

    cd "$PROJECT_ROOT/src/AktieKoll"

    echo -e "${BLUE}Available migrations:${NC}"
    dotnet ef migrations list

    echo ""
    print_info "Migration check complete!"
}

# Function to check Docker migrations
check_docker() {
    print_info "Checking Docker database migrations..."
    echo ""

    cd "$PROJECT_ROOT"

    # Check if containers are running
    if ! docker-compose ps | grep -q "api"; then
        print_error "Docker containers not running. Start them first with: docker-compose up -d"
        exit 1
    fi

    echo -e "${BLUE}Available migrations:${NC}"
    docker-compose exec api dotnet ef migrations list

    echo ""
    echo -e "${BLUE}Database migration history:${NC}"
    docker-compose exec postgres psql -U aktiekoll_user aktiekoll \
        -c "SELECT \"MigrationId\", \"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";" \
        2>/dev/null || print_warn "Could not read migration history table"

    echo ""
    print_info "Migration check complete!"
}

# Function to check production migrations
check_production() {
    print_info "Checking PRODUCTION database migrations..."
    echo ""

    cd "$PROJECT_ROOT"

    echo -e "${BLUE}Available migrations in code:${NC}"
    docker-compose exec api dotnet ef migrations list

    echo ""
    echo -e "${BLUE}Applied migrations in database:${NC}"
    docker-compose exec postgres psql -U aktiekoll_user aktiekoll \
        -c "SELECT \"MigrationId\", \"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"

    echo ""

    # Check for pending migrations
    MIGRATIONS_OUTPUT=$(docker-compose exec api dotnet ef migrations list 2>&1)

    if echo "$MIGRATIONS_OUTPUT" | grep -q "Pending"; then
        print_warn "⚠ There are PENDING migrations that need to be applied!"
        print_warn "Run: ./scripts/migrate.sh production"
    else
        print_info "✓ All migrations are applied. Database is up to date."
    fi

    echo ""
    print_info "Migration check complete!"
}

# Main execution
echo "========================================="
echo "  AktieKoll Migration Status Check"
echo "  Environment: $ENVIRONMENT"
echo "========================================="
echo ""

case $ENVIRONMENT in
    local)
        check_local
        ;;
    docker)
        check_docker
        ;;
    production)
        check_production
        ;;
esac
