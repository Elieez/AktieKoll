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

- .NET 9 SDK
- PostgreSQL database

## Quick Start

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

See [SECURITY.md](SECURITY.md) for production deployment checklist and security best practices.

**Key Steps:**
1. Set all required environment variables
2. Configure SSL/TLS certificates
3. Run database migrations
4. Test health check endpoints
5. Configure frontend with API URL and key

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `ConnectionStrings__PostgresConnection` | Yes | PostgreSQL connection string |
| `ApiKey` | Yes | API authentication key |
| `FrontendUrl` | Yes | Frontend domain for CORS |
| `ASPNETCORE_ENVIRONMENT` | No | Runtime environment (Development/Production) |
