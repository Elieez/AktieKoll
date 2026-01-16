# Step-by-Step Implementation Guide

This guide walks you through implementing all the deployment readiness improvements for AktieKoll. Follow these steps in order.

---

## Overview of Changes

This consolidated PR includes:
1. **Security Features** - API key auth, rate limiting, CORS, security headers
2. **Health Checks** - Monitoring endpoints with database connectivity
3. **Docker Configuration** - Complete containerization setup
4. **Database Migration Strategy** - Scripts and documentation
5. **Production Issues Documentation** - Known issues to address

**Total Files Added/Modified:** ~20 files

---

## Step 1: Review the Changes (15 minutes)

Before merging, understand what's being added:

### New Documentation Files
- [ ] Read `SECURITY.md` - Security configuration guide
- [ ] Read `DOCKER.md` - Docker deployment guide
- [ ] Read `DATABASE_MIGRATIONS.md` - Migration strategy
- [ ] Read `DEPLOYMENT_CHECKLIST.md` - Pre-deployment checklist
- [ ] Read `PRODUCTION_ISSUES.md` - Known issues to address
- [ ] Read `IMPLEMENTATION_GUIDE.md` - This file

### New Code Files
- [ ] Review `src/AktieKoll/Middleware/ApiKeyAuthenticationMiddleware.cs` - API key auth
- [ ] Review `src/AktieKoll/Program.cs` - Security features added
- [ ] Review `src/AktieKoll/Dockerfile` - Containerization
- [ ] Review `docker-compose.yml` - Orchestration

### New Scripts
- [ ] Review `scripts/migrate.sh` - Migration automation
- [ ] Review `scripts/backup-database.sh` - Database backup
- [ ] Review `scripts/restore-database.sh` - Database restore
- [ ] Review `scripts/check-migrations.sh` - Migration status

---

## Step 2: Merge the PR (5 minutes)

### Option A: Merge via GitHub
1. Go to the PR on GitHub
2. Review the files changed
3. Click "Merge pull request"
4. Delete the source branches after merging

### Option B: Merge via Command Line
```bash
# Switch to main branch
git checkout main

# Merge the consolidated branch
git merge --no-ff claude/deployment-ready-consolidated-zhg7a

# Push to remote
git push origin main

# Delete old branches (optional)
git branch -d claude/review-deployment-readiness-zhg7a
git branch -d claude/database-migration-strategy-zhg7a
git branch -d claude/fix-docker-healthcheck-zhg7a
git push origin --delete claude/review-deployment-readiness-zhg7a
git push origin --delete claude/database-migration-strategy-zhg7a
git push origin --delete claude/fix-docker-healthcheck-zhg7a
```

---

## Step 3: Test Locally with Docker (30 minutes)

### 3.1 Set Up Environment

```bash
# Make scripts executable
chmod +x scripts/*.sh

# Copy environment template
cp .env.example .env

# Edit .env with your values
nano .env
```

Required values in `.env`:
```env
POSTGRES_PASSWORD=your-secure-password
API_KEY=generate-with-openssl-rand-base64-32
FRONTEND_URL=http://localhost:3000
```

Generate API key:
```bash
openssl rand -base64 32
```

### 3.2 Start Docker Services

```bash
# Start all services
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f api
```

Expected output:
```
✓ postgres is healthy
✓ api is running
```

### 3.3 Run Database Migrations

```bash
# Check migration status
./scripts/check-migrations.sh docker

# Run migrations
./scripts/migrate.sh docker

# Verify migrations applied
./scripts/check-migrations.sh docker
```

### 3.4 Test Health Checks

```bash
# Basic health check (should return 200 OK)
curl http://localhost:5000/health

# Database health check (should return 200 OK)
curl http://localhost:5000/health/ready
```

Expected response:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567"
}
```

### 3.5 Test API Endpoints

**Without API key (should fail with 401):**
```bash
curl -w "\nHTTP Status: %{http_code}\n" http://localhost:5000/api/InsiderTrades
```

Expected: `HTTP Status: 401`

**With API key (should succeed):**
```bash
curl -H "X-API-Key: your-api-key-from-env" \
  -w "\nHTTP Status: %{http_code}\n" \
  http://localhost:5000/api/InsiderTrades
```

Expected: `HTTP Status: 200` (or 404 if no data)

### 3.6 Test Rate Limiting

```bash
# Send 105 requests rapidly
for i in {1..105}; do
  curl -s -w "%{http_code}\n" http://localhost:5000/health -o /dev/null
done
```

Expected: First 100 return `200`, then `429` (Too Many Requests)

### 3.7 Test Security Headers

```bash
curl -I http://localhost:5000/health
```

Expected headers:
```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
```

### 3.8 Clean Up

```bash
# Stop services
docker-compose down

# Or stop and remove volumes (WARNING: deletes data)
docker-compose down -v
```

---

## Step 4: Configure GitHub Actions Secrets (10 minutes)

Your CI/CD workflows need these secrets configured.

### 4.1 Navigate to Repository Settings
1. Go to your repository on GitHub
2. Click "Settings" → "Secrets and variables" → "Actions"
3. Click "New repository secret"

### 4.2 Add Required Secrets

**RENOVATE_TOKEN** (for dependency updates)
1. Go to https://github.com/settings/tokens
2. Generate new token (classic)
3. Select `repo` scope
4. Copy token
5. Add as `RENOVATE_TOKEN` in repository secrets

**POSTGRES_CONNECTION** (for cron job)
1. Use your production database connection string
2. Format: `Host=your-db-host;Database=aktiekoll;Username=user;Password=pass`
3. Add as `POSTGRES_CONNECTION` in repository secrets

### 4.3 Verify Secrets Work

Manually trigger workflows:
1. Go to "Actions" tab
2. Select "Renovate" workflow → "Run workflow"
3. Select "Fetch Trades" workflow → "Run workflow"
4. Check logs for success

---

## Step 5: Address Critical Production Issues (2-4 hours)

**IMPORTANT:** Review `PRODUCTION_ISSUES.md` for details on each issue.

### 5.1 Add Retry Policies for External APIs (HIGH PRIORITY)

**What:** OpenFIGI API and CSV fetching have no retry logic
**Impact:** Scheduled data fetching will fail on transient network issues
**Time:** ~1-2 hours

**Steps:**

1. Add Polly package:
```bash
cd src
dotnet add AktieKoll package Microsoft.Extensions.Http.Polly
```

2. Update `src/Directory.Packages.props`:
```xml
<PackageVersion Include="Microsoft.Extensions.Http.Polly" Version="8.0.0" />
```

3. Update `src/AktieKoll/Program.cs` - Add retry policies:

```csharp
using Polly;
using Polly.Extensions.Http;

// Add helper methods at the top of the file
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // 5xx and 408
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) // 429
        .WaitAndRetryAsync(3,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                Console.WriteLine($"Retry {retryCount} after {timespan.TotalSeconds}s");
            });
}

static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
{
    return Policy.TimeoutAsync<HttpResponseMessage>(30); // 30 seconds
}

// Update HttpClient registrations (find these lines and modify)
builder.Services.AddHttpClient<CsvFetchService>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetTimeoutPolicy());

builder.Services.AddHttpClient<IOpenFigiService, OpenFigiService>(client =>
    client.BaseAddress = new Uri("https://api.openfigi.com/v3/"))
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetTimeoutPolicy());
```

4. Test retry behavior:
```bash
# Rebuild and run
docker-compose up -d --build

# Monitor logs for retry attempts
docker-compose logs -f api | grep -i retry
```

**See `PRODUCTION_ISSUES.md` Issue #1 for alternative implementations.**

### 5.2 Update Production Logging Configuration (10 minutes)

**What:** Production logging is too restrictive (Warning level)
**Impact:** Won't see important application events in production
**Time:** ~10 minutes

**Steps:**

1. Edit `src/AktieKoll/appsettings.Production.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "PostgresConnection": ""
  },
  "FrontendUrl": "",
  "ApiKey": ""
}
```

2. Commit the change:
```bash
git add src/AktieKoll/appsettings.Production.json
git commit -m "Update production logging to Information level"
git push
```

### 5.3 Fix appsettings.json .gitignore Inconsistency (5 minutes)

**What:** `appsettings.json` is in .gitignore but also committed
**Time:** ~5 minutes

**Recommended: Remove from .gitignore (keep as template)**

1. Edit `.gitignore` and remove this line:
```
appsettings.json
```

2. Keep these lines:
```
appsettings.Development.json
appsettings.Local.json
```

3. Commit:
```bash
git add .gitignore
git commit -m "Remove appsettings.json from .gitignore (keep as template)"
git push
```

---

## Step 6: Update Documentation (30 minutes)

### 6.1 Update README with Your Information

Edit `README.md` and replace:
- `yourusername` with your GitHub username
- `your-frontend-domain.com` with your actual domain
- Add any project-specific setup instructions

### 6.2 Create Your Own .env File

Based on `.env.example`, document your actual production values (store securely, not in git):

```bash
# Production environment (STORE SECURELY - NOT IN GIT)
POSTGRES_PASSWORD=actual-production-password
API_KEY=actual-production-api-key
FRONTEND_URL=https://yourdomain.com
```

### 6.3 Document Your Deployment Platform

Add a section to `DOCKER.md` or create `DEPLOYMENT.md` with:
- Where you're deploying (AWS, Azure, DigitalOcean, etc.)
- Server specifications
- Domain configuration
- SSL certificate setup
- Monitoring setup

---

## Step 7: Production Deployment (1-2 hours)

Follow these steps carefully for production deployment.

### 7.1 Pre-Deployment Checklist

Go through `DEPLOYMENT_CHECKLIST.md` and check off all items.

**Critical items:**
- [ ] All CI/CD workflows passing
- [ ] Docker images build successfully
- [ ] All tests passing
- [ ] Environment variables documented
- [ ] Production .env file created (not in git)
- [ ] Database backup strategy planned
- [ ] Rollback plan documented

### 7.2 Server Setup

On your production server:

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Install Docker Compose
sudo apt install docker-compose-plugin -y

# Verify installation
docker --version
docker compose version
```

### 7.3 Deploy Application

```bash
# Clone repository
git clone https://github.com/yourusername/AktieKoll.git
cd AktieKoll

# Create production .env file
nano .env
# Add production values (POSTGRES_PASSWORD, API_KEY, FRONTEND_URL)

# Start services
docker-compose up -d

# Run migrations
./scripts/migrate.sh production

# Verify health
curl http://localhost:5000/health/ready
```

### 7.4 Configure Reverse Proxy (HTTPS)

See `DOCKER.md` for detailed Nginx or Caddy setup.

**Quick Caddy setup:**
```bash
sudo apt install caddy

# Edit Caddyfile
sudo nano /etc/caddy/Caddyfile
```

Add:
```
api.yourdomain.com {
    reverse_proxy localhost:5000
}
```

```bash
sudo systemctl reload caddy
```

### 7.5 Verify Production Deployment

```bash
# Test health checks
curl https://api.yourdomain.com/health
curl https://api.yourdomain.com/health/ready

# Test API (should fail without key)
curl https://api.yourdomain.com/api/InsiderTrades

# Test with API key (should succeed)
curl -H "X-API-Key: your-production-key" \
  https://api.yourdomain.com/api/InsiderTrades

# Check SSL
curl -I https://api.yourdomain.com/health

# Test from frontend
# Configure frontend with:
# - API_URL=https://api.yourdomain.com
# - API_KEY=your-production-key
```

### 7.6 Set Up Monitoring

**Basic monitoring with cron:**
```bash
# Create health check script
cat > /usr/local/bin/check-api-health.sh <<'EOF'
#!/bin/bash
if ! curl -f https://api.yourdomain.com/health/ready &>/dev/null; then
    echo "API health check failed at $(date)" | \
      mail -s "AktieKoll API Down" admin@yourdomain.com
fi
EOF

chmod +x /usr/local/bin/check-api-health.sh

# Add to crontab (every 5 minutes)
(crontab -l 2>/dev/null; echo "*/5 * * * * /usr/local/bin/check-api-health.sh") | crontab -
```

**Better: Use uptime monitoring service**
- UptimeRobot (free tier)
- Pingdom
- StatusCake
- Monitor: `https://api.yourdomain.com/health/ready`

---

## Step 8: Configure Frontend (30 minutes)

Your frontend needs to be configured to work with the secured backend.

### 8.1 Update Frontend Environment Variables

In your frontend `.env` file:
```env
NEXT_PUBLIC_API_URL=https://api.yourdomain.com
API_KEY=your-production-api-key  # Keep secret, don't expose to browser
```

### 8.2 Update API Calls

All API calls must include the `X-API-Key` header:

**Example (Next.js/React):**
```javascript
// Create an API client (lib/api.js)
const API_URL = process.env.NEXT_PUBLIC_API_URL;
const API_KEY = process.env.API_KEY; // Server-side only

export async function fetchInsiderTrades() {
  const response = await fetch(`${API_URL}/api/InsiderTrades`, {
    headers: {
      'X-API-Key': API_KEY,
      'Content-Type': 'application/json'
    }
  });

  if (!response.ok) {
    throw new Error(`API error: ${response.status}`);
  }

  return response.json();
}
```

**IMPORTANT:**
- Never expose API key in browser JavaScript
- Use server-side API routes (Next.js API routes, server components)
- Or use environment variables that aren't prefixed with `NEXT_PUBLIC_`

### 8.3 Handle Rate Limiting

Add retry logic for 429 responses:

```javascript
async function fetchWithRetry(url, options, maxRetries = 3) {
  for (let i = 0; i < maxRetries; i++) {
    const response = await fetch(url, options);

    if (response.status === 429) {
      const retryAfter = response.headers.get('Retry-After') || 60;
      console.log(`Rate limited, waiting ${retryAfter}s...`);
      await new Promise(resolve => setTimeout(resolve, retryAfter * 1000));
      continue;
    }

    return response;
  }

  throw new Error('Max retries exceeded');
}
```

### 8.4 Update CORS Domain

Make sure your frontend domain is in the backend `FrontendUrl` environment variable:

```bash
# In backend .env
FRONTEND_URL=https://your-frontend-domain.com
```

Restart backend if needed:
```bash
docker-compose restart api
```

---

## Step 9: Post-Deployment Verification (30 minutes)

### 9.1 Verify All Endpoints

Create a test script (`test-api.sh`):
```bash
#!/bin/bash
API_URL="https://api.yourdomain.com"
API_KEY="your-production-key"

echo "Testing health endpoints..."
curl -f $API_URL/health || echo "❌ /health failed"
curl -f $API_URL/health/ready || echo "❌ /health/ready failed"

echo "Testing API endpoints..."
curl -f -H "X-API-Key: $API_KEY" $API_URL/api/InsiderTrades || echo "❌ API failed"

echo "Testing rate limiting..."
for i in {1..105}; do
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" $API_URL/health)
  if [ $STATUS -eq 429 ]; then
    echo "✓ Rate limiting works (request $i returned 429)"
    break
  fi
done

echo "Testing security headers..."
curl -I $API_URL/health | grep -E "(X-Content-Type-Options|X-Frame-Options|X-XSS-Protection)"

echo "✅ All tests complete"
```

Run it:
```bash
chmod +x test-api.sh
./test-api.sh
```

### 9.2 Check Logs

```bash
# Application logs
docker-compose logs --tail=100 api

# Check for errors
docker-compose logs api | grep -i error

# Check for warnings
docker-compose logs api | grep -i warning

# Check database logs
docker-compose logs postgres | grep -i error
```

### 9.3 Monitor Resource Usage

```bash
# Container stats
docker stats

# Disk usage
docker system df

# Database size
docker-compose exec postgres psql -U aktiekoll_user aktiekoll \
  -c "SELECT pg_size_pretty(pg_database_size('aktiekoll'));"
```

### 9.4 Test Data Fetching

Manually trigger the cron job:
```bash
# Run FetchTrades manually
docker-compose run --rm fetch-trades

# Or trigger GitHub Action manually
# Go to Actions → "Fetch Trades" → Run workflow
```

Verify data in database:
```bash
docker-compose exec postgres psql -U aktiekoll_user aktiekoll \
  -c "SELECT COUNT(*) FROM \"InsiderTrades\";"
```

---

## Step 10: Ongoing Maintenance

### Daily Tasks
- [ ] Check application logs for errors
- [ ] Monitor health check status
- [ ] Verify scheduled data fetching is working

### Weekly Tasks
- [ ] Review and merge Renovate PRs (dependency updates)
- [ ] Check disk usage
- [ ] Review rate limiting metrics
- [ ] Check API response times

### Monthly Tasks
- [ ] Review and rotate API keys (if needed)
- [ ] Test backup restoration procedure
- [ ] Review security logs
- [ ] Update documentation

### Automated Tasks (Already Configured)
- ✅ Data fetching every 6 hours (GitHub Actions cron)
- ✅ Dependency updates (Renovate daily)
- ✅ CI/CD on every push (GitHub Actions)

---

## Troubleshooting

### Issue: CI/CD Workflows Failing

**Check:**
1. GitHub Actions logs
2. Package restore issues
3. Test failures

**Solution:**
```bash
# Run locally first
cd src
dotnet restore AktieKoll.slnx
dotnet build AktieKoll.slnx --configuration Release
dotnet test AktieKoll.slnx --configuration Release
```

### Issue: Docker Containers Not Starting

**Check:**
```bash
docker-compose logs api
docker-compose logs postgres
```

**Common causes:**
- Port conflicts (5000, 5432)
- Missing environment variables
- Database not healthy

**Solution:**
```bash
# Stop all containers
docker-compose down

# Remove volumes (WARNING: deletes data)
docker-compose down -v

# Start fresh
docker-compose up -d
```

### Issue: Health Checks Failing

**Check:**
```bash
docker-compose exec api curl http://localhost:8080/health
docker-compose exec api curl http://localhost:8080/health/ready
```

**Common causes:**
- Database connection issue
- Application not started
- curl not installed (should be fixed now)

### Issue: Rate Limiting Not Working

**Check CORS configuration:**
```bash
# Should include rate limit headers
curl -I http://localhost:5000/health
```

**Verify rate limiter is registered** in Program.cs

### Issue: API Key Authentication Not Working

**Check:**
1. API key set in environment variables
2. Middleware registered in Program.cs
3. Header name is exactly `X-API-Key`

**Test:**
```bash
# Should fail (401)
curl -v http://localhost:5000/api/InsiderTrades

# Should succeed (200)
curl -v -H "X-API-Key: your-key" http://localhost:5000/api/InsiderTrades
```

---

## Reference Links

### Documentation
- Main README: [README.md](README.md)
- Security Guide: [SECURITY.md](SECURITY.md)
- Docker Guide: [DOCKER.md](DOCKER.md)
- Migration Strategy: [DATABASE_MIGRATIONS.md](DATABASE_MIGRATIONS.md)
- Deployment Checklist: [DEPLOYMENT_CHECKLIST.md](DEPLOYMENT_CHECKLIST.md)
- Production Issues: [PRODUCTION_ISSUES.md](PRODUCTION_ISSUES.md)

### Scripts
- Migration: `./scripts/migrate.sh`
- Backup: `./scripts/backup-database.sh`
- Restore: `./scripts/restore-database.sh`
- Check Status: `./scripts/check-migrations.sh`

### External Resources
- [.NET Docker Images](https://hub.docker.com/_/microsoft-dotnet-aspnet)
- [PostgreSQL Docker](https://hub.docker.com/_/postgres)
- [Polly Documentation](https://github.com/App-vNext/Polly)
- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)

---

## Summary

After completing this guide, you will have:

✅ Consolidated all deployment changes into one branch
✅ Security features implemented (API key, rate limiting, CORS, headers)
✅ Health checks configured
✅ Docker containerization complete
✅ Database migration strategy in place
✅ Production deployment completed
✅ Frontend integrated with backend
✅ Monitoring and maintenance procedures established

**Estimated Total Time:** 6-10 hours (depending on experience level)

**Priority Order:**
1. Steps 1-3: Review, merge, test locally (1-2 hours) - **DO FIRST**
2. Step 5: Address critical issues (2-4 hours) - **DO BEFORE PRODUCTION**
3. Steps 4, 6-10: Production deployment and setup (3-4 hours)

---

**Questions or Issues?**
- Review the specific documentation files for details
- Check troubleshooting sections
- Review `PRODUCTION_ISSUES.md` for known issues
- Check GitHub Actions logs for CI/CD issues

**Last Updated:** 2026-01-16
