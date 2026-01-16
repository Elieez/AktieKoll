# AktieKoll

AktieKoll is a C# project for tracking and analyzing insider trades on the stock market. It provides functionality for fetching, storing, and querying insider trade data, with supporting services and tests for robust functionality.

## Features

- **Fetch Insider Trades**: Retrieve insider trade data (e.g., from CSV sources) using `CsvFetchService`.
- **Database Storage**: Store trades in a PostgreSQL database via Entity Framework Core.
- **Data Analysis**: Query for top companies by transaction volume, filter and process insider trades.
- **REST API**: Expose controllers for integration with frontend applications.
- **Security**: API key authentication, rate limiting, CORS protection, and security headers.
- **Health Checks**: Monitoring endpoints with database connectivity checks.
- **Test Coverage**: Includes unit tests for core services and database interactions.

## Technologies Used

- C#
- .NET Core / ASP.NET Core
- Entity Framework Core (PostgreSQL)
- CsvHelper
- xUnit (for testing)

## Project Structure

```
src/
  AktieKoll/            # Main Web API application
  FetchTrades/          # Standalone tool for fetching trades via CLI
  AktieKoll.Tests/      # Unit tests for services and database
```

## Prerequisites

### Option 1: Docker (Recommended for deployment)
- Docker 20.10+
- Docker Compose 2.0+

### Option 2: Local Development
- .NET 9 SDK
- PostgreSQL database

## Quick Start

### Option A: Using Docker (Recommended)

The easiest way to run the application with all dependencies.

**1. Create environment file**

```bash
# Copy the example file
cp .env.example .env

# Edit .env and set your values
nano .env
```

**2. Start all services**

```bash
docker-compose up -d
```

This will start:
- PostgreSQL database on port 5432
- API on port 5000

**3. Run database migrations**

```bash
# Run migrations inside the container
docker-compose exec api dotnet ef database update
```

**4. Check health**

```bash
curl http://localhost:5000/health/ready
```

**Docker Commands:**

```bash
# View logs
docker-compose logs -f api

# Stop all services
docker-compose down

# Rebuild after code changes
docker-compose up -d --build

# Remove volumes (WARNING: deletes database data)
docker-compose down -v
```

### Option B: Local Development (Without Docker)

### 1. Clone and Restore

```bash
cd src
dotnet restore AktieKoll.slnx
```

### 2. Configure Database

Set your PostgreSQL connection string:

```bash
# Linux/macOS
export ConnectionStrings__PostgresConnection="Host=localhost;Database=aktiekoll;Username=your_user;Password=your_password"

# Windows PowerShell
$env:ConnectionStrings__PostgresConnection="Host=localhost;Database=aktiekoll;Username=your_user;Password=your_password"
```

### 3. Run Migrations

```bash
cd AktieKoll
dotnet ef database update
```

### 4. Run the API

```bash
dotnet run
```

The API will be available at `https://localhost:5001` (HTTPS) and `http://localhost:5000` (HTTP).

## Security Configuration

This API implements multiple security layers to protect against unauthorized access and abuse. See [SECURITY.md](SECURITY.md) for complete details.

**Quick Setup:**

1. Set your API key (required for all API endpoints):
   ```bash
   export ApiKey="your-secure-api-key-here"
   ```

2. Configure your frontend URL for CORS:
   ```bash
   export FrontendUrl="https://your-frontend-domain.com"
   ```

3. Your frontend must include the API key in requests:
   ```javascript
   headers: {
     'X-API-Key': 'your-api-key-here'
   }
   ```

**Security Features:**
- API key authentication on all endpoints (except health checks)
- Rate limiting: 100 requests/minute per IP
- CORS restricted to configured frontend domain
- Security headers (XSS protection, frame denial, etc.)
- HTTPS enforcement in production
- Health check endpoints at `/health` and `/health/ready`

## API Endpoints

All endpoints require `X-API-Key` header except health checks.

### Health Checks (No authentication required)
- `GET /health` - Basic health status
- `GET /health/ready` - Health status with database connectivity check

### Insider Trades
- `POST /api/InsiderTrades` - Add insider trades
- `GET /api/InsiderTrades` - Get all trades
- `GET /api/InsiderTrades/page` - Get paginated trades
- `GET /api/InsiderTrades/top` - Get top trades by value
- `GET /api/InsiderTrades/count-buy` - Buy transaction statistics
- `GET /api/InsiderTrades/count-sell` - Sell transaction statistics
- `GET /api/InsiderTrades/company` - Filter by company name

## Configuration Files

- `appsettings.json` - Base configuration (template)
- `appsettings.Development.json` - Development settings
- `appsettings.Production.json` - Production settings template

**Required Configuration:**
- `ConnectionStrings:PostgresConnection` - Database connection
- `ApiKey` - API authentication key
- `FrontendUrl` - Allowed CORS origin

## Database Migrations

AktieKoll uses Entity Framework Core migrations to manage database schema changes.

### Quick Migration Commands

```bash
# Check migration status
./scripts/check-migrations.sh docker

# Run all pending migrations
./scripts/migrate.sh docker

# Create backup before migration (automatic in production)
./scripts/backup-database.sh docker --compress
```

### Managing Migrations

```bash
# Create new migration
cd src/AktieKoll
dotnet ef migrations add MigrationName

# Apply migrations locally
dotnet ef database update

# Rollback to specific migration
dotnet ef database update PreviousMigrationName
```

### Production Migrations

For production, always use the migration scripts for safety:

```bash
# Run migrations with automatic backup
./scripts/migrate.sh production

# Check status
./scripts/check-migrations.sh production
```

**See [DATABASE_MIGRATIONS.md](DATABASE_MIGRATIONS.md) for comprehensive migration strategy including:**
- Migration workflow and best practices
- Rollback strategies
- Backup and restore procedures
- Troubleshooting guide
- Zero-downtime deployment strategies

## Running Tests

```bash
cd src
dotnet test AktieKoll.slnx --configuration Release
```

## CI/CD

GitHub Actions workflows are configured for:
- **CI Pipeline** (`.github/workflows/ci.yml`) - Build and test on push/PR
- **Scheduled Data Fetch** (`.github/workflows/cron-fetch.yml`) - Fetches insider trades every 6 hours
- **Dependency Updates** (`.github/workflows/renovate.yml`) - Automated dependency management

## Deployment

### Docker Deployment (Recommended)

**Production Deployment with Docker:**

1. **Clone repository on production server**
   ```bash
   git clone https://github.com/yourusername/AktieKoll.git
   cd AktieKoll
   ```

2. **Create production .env file**
   ```bash
   nano .env
   ```

   Add your production values:
   ```env
   POSTGRES_PASSWORD=your-secure-db-password
   API_KEY=your-secure-api-key
   FRONTEND_URL=https://your-frontend-domain.com
   ```

3. **Start services**
   ```bash
   docker-compose up -d
   ```

4. **Run migrations**
   ```bash
   docker-compose exec api dotnet ef database update
   ```

5. **Verify deployment**
   ```bash
   curl http://localhost:5000/health/ready
   ```

6. **Set up reverse proxy (Nginx/Caddy) for HTTPS**
   - Point to `localhost:5000`
   - Configure SSL/TLS certificates
   - Add domain routing

**Building for Production (Manual):**

```bash
# Build API image
cd src
docker build -f AktieKoll/Dockerfile -t aktiekoll-api:latest .

# Build FetchTrades image
docker build -f FetchTrades/Dockerfile -t aktiekoll-fetch:latest .

# Push to registry (Docker Hub, GitHub Container Registry, etc.)
docker tag aktiekoll-api:latest yourusername/aktiekoll-api:latest
docker push yourusername/aktiekoll-api:latest
```

### Platform-Specific Deployment

See [SECURITY.md](SECURITY.md) for production deployment checklist and security best practices.

**Key Steps:**
1. Set all required environment variables
2. Configure SSL/TLS certificates (via reverse proxy)
3. Run database migrations
4. Test health check endpoints
5. Configure frontend with API URL and key
6. Set up monitoring for health endpoints

## Environment Variables

### For Docker Compose (.env file)

| Variable | Required | Description | Example |
|----------|----------|-------------|---------|
| `POSTGRES_PASSWORD` | Yes | PostgreSQL database password | `SecurePass123!` |
| `API_KEY` | Yes | API authentication key | Generated via `openssl rand -base64 32` |
| `FRONTEND_URL` | Yes | Frontend domain for CORS | `https://aktiekoll.com` |

### For Manual/Native Deployment

| Variable | Required | Description | Example |
|----------|----------|-------------|---------|
| `ConnectionStrings__PostgresConnection` | Yes | PostgreSQL connection string | `Host=localhost;Database=aktiekoll;Username=user;Password=pass` |
| `ApiKey` | Yes | API authentication key | Generated via `openssl rand -base64 32` |
| `FrontendUrl` | Yes | Frontend domain for CORS | `https://aktiekoll.com` |
| `ASPNETCORE_ENVIRONMENT` | No | Runtime environment | `Development` or `Production` |
