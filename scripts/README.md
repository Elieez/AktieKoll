# Database Management Scripts

This directory contains scripts for managing database migrations, backups, and restores in the AktieKoll application.

## Scripts Overview

| Script | Purpose | Usage |
|--------|---------|-------|
| `migrate.sh` | Run database migrations | `./migrate.sh <environment>` |
| `backup-database.sh` | Backup PostgreSQL database | `./backup-database.sh <environment>` |
| `restore-database.sh` | Restore database from backup | `./restore-database.sh <environment> <backup_file>` |
| `check-migrations.sh` | Check migration status | `./check-migrations.sh <environment>` |

## Making Scripts Executable

Before using these scripts, make them executable:

```bash
chmod +x scripts/*.sh
```

## Quick Reference

### Run Migrations

```bash
# Local development
./scripts/migrate.sh local

# Docker environment
./scripts/migrate.sh docker

# Production (with automatic backup)
./scripts/migrate.sh production
```

### Backup Database

```bash
# Backup Docker database
./scripts/backup-database.sh docker

# Backup and compress
./scripts/backup-database.sh docker --compress

# Production backup
./scripts/backup-database.sh production --compress
```

### Restore Database

```bash
# Restore from backup
./scripts/restore-database.sh docker backups/aktiekoll_2025-01-15_10-30-00.sql

# Restore compressed backup
./scripts/restore-database.sh docker backups/aktiekoll_2025-01-15_10-30-00.sql.gz
```

### Check Migration Status

```bash
# Check what migrations are applied/pending
./scripts/check-migrations.sh docker
./scripts/check-migrations.sh production
```

## Environments

All scripts support three environments:

- **local** - Local PostgreSQL database (native installation)
- **docker** - PostgreSQL running in Docker container
- **production** - Production database (usually Docker-based)

## Backup Location

Backups are stored in the `backups/` directory at the project root:

```
AktieKoll/
├── backups/
│   ├── aktiekoll_2025-01-15_10-30-00.sql
│   ├── aktiekoll_2025-01-15_14-45-30.sql.gz
│   └── ...
```

**Note:** The `backups/` directory is ignored by git (see `.gitignore`).

## Safety Features

### Automatic Backups

- `migrate.sh` automatically creates backups before running production migrations
- Can be skipped with `--skip-backup` flag (not recommended)

### Confirmation Prompts

- Production operations require explicit confirmation
- Restore operations require typing "yes" or "RESTORE PRODUCTION"

### Health Checks

- After production migrations, the script verifies application health
- Alerts if health checks fail

## Examples

### Deploy with Migration

```bash
# 1. Pull latest code
git pull origin main

# 2. Rebuild containers
docker-compose up -d --build

# 3. Run migrations (creates backup automatically)
./scripts/migrate.sh production

# 4. Verify
./scripts/check-migrations.sh production
curl https://api.yourdomain.com/health/ready
```

### Rollback to Previous Migration

```bash
# Check current migrations
./scripts/check-migrations.sh production

# Rollback to specific migration
./scripts/migrate.sh production --target "Update publishingDate and drop old date"

# Verify
./scripts/check-migrations.sh production
```

### Disaster Recovery

```bash
# Restore from backup
./scripts/restore-database.sh production backups/aktiekoll_2025-01-15_10-00-00.sql.gz

# Run migrations to bring schema up to date
./scripts/migrate.sh production

# Verify application
curl https://api.yourdomain.com/health/ready
```

### Manual Backup Before Changes

```bash
# Create backup before making risky changes
./scripts/backup-database.sh production --compress

# Make changes...

# If something goes wrong, restore
./scripts/restore-database.sh production backups/aktiekoll_latest.sql.gz
```

## Automated Backups

For production, set up automated daily backups using cron:

```bash
# Edit crontab
crontab -e

# Add daily backup at 2 AM
0 2 * * * /path/to/AktieKoll/scripts/backup-database.sh production --compress
```

## Troubleshooting

### Script Permission Denied

```bash
chmod +x scripts/*.sh
```

### Docker Container Not Running

```bash
docker-compose up -d
```

### Database Connection Failed

```bash
# Check if PostgreSQL is running
docker-compose ps postgres

# Check logs
docker-compose logs postgres
```

### Migration Already Applied

This is normal - EF Core tracks applied migrations. Check status:

```bash
./scripts/check-migrations.sh docker
```

## Additional Documentation

- See `DATABASE_MIGRATIONS.md` for comprehensive migration strategy
- See `DOCKER.md` for Docker deployment details
- See `DEPLOYMENT_CHECKLIST.md` for full deployment checklist

## Support

For issues with these scripts:
1. Check script output for error messages
2. Review `DATABASE_MIGRATIONS.md` troubleshooting section
3. Check Docker logs: `docker-compose logs api postgres`
4. Open an issue on GitHub
