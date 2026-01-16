# Deployment Readiness Checklist

Use this checklist to ensure your AktieKoll backend is fully ready for production deployment.

## 1. Code Quality & CI/CD

### 1.1 CI Pipeline Status
- [ ] All GitHub Actions workflows pass (build, test)
- [ ] No failing tests in CI
- [ ] Build succeeds in Release configuration
- [ ] No compiler warnings in Release build
- [ ] Code compiles successfully

**How to verify:**
```bash
# Run locally to check for issues
cd src
dotnet restore AktieKoll.slnx
dotnet build AktieKoll.slnx --configuration Release
dotnet test AktieKoll.slnx --configuration Release
```

**If CI fails:**
- Check GitHub Actions logs: `https://github.com/yourusername/AktieKoll/actions`
- Fix any test failures
- Fix any build errors
- Check for missing NuGet packages

### 1.2 Dependencies
- [ ] All NuGet packages are up to date
- [ ] No security vulnerabilities in dependencies
- [ ] Renovate bot is configured and working
- [ ] AspNetCore.HealthChecks.NpgSql package added (version 9.0.2+)

**How to verify:**
```bash
# Check for outdated packages
cd src
dotnet list package --outdated

# Check for vulnerable packages
dotnet list package --vulnerable
```

## 2. Security Configuration

### 2.1 API Key Authentication
- [ ] API key authentication middleware implemented (`src/AktieKoll/Middleware/ApiKeyAuthenticationMiddleware.cs`)
- [ ] Health check endpoints exempt from authentication
- [ ] API key configuration in appsettings
- [ ] Production API key generated (use: `openssl rand -base64 32`)
- [ ] API key stored securely (not in code)

**How to verify:**
```bash
# Test without API key (should fail)
curl -X GET http://localhost:5000/api/InsiderTrades

# Test with API key (should succeed)
curl -X GET http://localhost:5000/api/InsiderTrades -H "X-API-Key: your-key"

# Test health check (should work without key)
curl http://localhost:5000/health
```

### 2.2 Rate Limiting
- [ ] Rate limiting configured in Program.cs
- [ ] Set to 100 requests/minute per IP (or custom limit)
- [ ] Returns 429 Too Many Requests when exceeded
- [ ] Rate limit headers exposed in CORS

**How to verify:**
```bash
# Send multiple rapid requests to test rate limit
for i in {1..105}; do
  curl -w "\n%{http_code}\n" http://localhost:5000/health 2>/dev/null | tail -1
done
# Requests 101+ should return 429
```

### 2.3 CORS Configuration
- [ ] CORS configured for frontend domain
- [ ] FrontendUrl environment variable set
- [ ] CORS policy allows necessary headers
- [ ] CORS policy exposes rate limit headers
- [ ] Development uses localhost:3000
- [ ] Production uses actual frontend domain

**Configuration to check in Program.cs:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
          .WithOrigins(frontendUrl)
          .AllowAnyMethod()
          .AllowAnyHeader()
          .WithExposedHeaders("X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset");
    });
});
```

### 2.4 Security Headers
- [ ] X-Content-Type-Options: nosniff
- [ ] X-Frame-Options: DENY
- [ ] X-XSS-Protection: 1; mode=block
- [ ] Referrer-Policy: strict-origin-when-cross-origin

**How to verify:**
```bash
curl -I http://localhost:5000/health
# Check response headers
```

### 2.5 HTTPS Configuration
- [ ] UseHttpsRedirection() enabled
- [ ] UseHsts() enabled in production
- [ ] SSL/TLS certificate ready for production
- [ ] Reverse proxy configured (Nginx/Caddy)

## 3. Health Checks

### 3.1 Health Check Endpoints
- [ ] `/health` endpoint implemented (basic check)
- [ ] `/health/ready` endpoint implemented (with DB check)
- [ ] Health checks exempt from API key authentication
- [ ] PostgreSQL health check configured

**How to verify:**
```bash
# Basic health check
curl http://localhost:5000/health

# Database connectivity check
curl http://localhost:5000/health/ready

# Should return JSON with status
```

### 3.2 Docker Health Checks
- [ ] Dockerfile includes HEALTHCHECK instruction
- [ ] Health check uses curl to test endpoint
- [ ] Health check interval set appropriately (30s)

## 4. Docker Configuration

### 4.1 Dockerfiles
- [ ] `src/AktieKoll/Dockerfile` exists
- [ ] `src/FetchTrades/Dockerfile` exists
- [ ] Multi-stage builds for optimized images
- [ ] Non-root user configured
- [ ] Correct ports exposed (8080)

**How to verify:**
```bash
# Build API image
cd src
docker build -f AktieKoll/Dockerfile -t aktiekoll-api:test .

# Check image size (should be reasonable)
docker images aktiekoll-api:test
```

### 4.2 Docker Compose
- [ ] `docker-compose.yml` exists in root
- [ ] PostgreSQL service configured
- [ ] API service configured
- [ ] Services use health checks
- [ ] Persistent volumes configured
- [ ] Environment variables mapped
- [ ] Network isolation configured

**How to verify:**
```bash
# Validate docker-compose file
docker-compose config

# Start services
docker-compose up -d

# Check service status
docker-compose ps

# Check logs
docker-compose logs api
docker-compose logs postgres
```

### 4.3 Docker Ignore
- [ ] `.dockerignore` file exists
- [ ] Excludes build artifacts (bin/, obj/)
- [ ] Excludes .git directory
- [ ] Excludes .env files
- [ ] Excludes IDE files

## 5. Configuration Files

### 5.1 Application Settings
- [ ] `appsettings.json` exists (template)
- [ ] `appsettings.Development.json` exists
- [ ] `appsettings.Production.json` exists
- [ ] Connection string placeholder present
- [ ] ApiKey placeholder present
- [ ] FrontendUrl placeholder present

**Check files:**
- `src/AktieKoll/appsettings.json`
- `src/AktieKoll/appsettings.Development.json`
- `src/AktieKoll/appsettings.Production.json`

### 5.2 Environment Variables
- [ ] `.env.example` file exists and documented
- [ ] All required variables documented
- [ ] Example values provided
- [ ] `.env` in .gitignore
- [ ] No secrets committed to git

**Required variables:**
```env
# For Docker
POSTGRES_PASSWORD=
API_KEY=
FRONTEND_URL=

# For manual deployment
ConnectionStrings__PostgresConnection=
ApiKey=
FrontendUrl=
ASPNETCORE_ENVIRONMENT=
```

### 5.3 Git Ignore
- [ ] `.env` files ignored
- [ ] `appsettings.json` ignored (if contains secrets)
- [ ] Build outputs ignored (bin/, obj/)
- [ ] No sensitive data in repository

## 6. Database

### 6.1 Migrations
- [ ] All migrations created
- [ ] Migrations tested locally
- [ ] Migration strategy documented
- [ ] Migrations can run in Docker container

**How to verify:**
```bash
# List migrations
cd src/AktieKoll
dotnet ef migrations list

# Test applying migrations
dotnet ef database update

# Or in Docker
docker-compose exec api dotnet ef database update
```

### 6.2 Database Configuration
- [ ] PostgreSQL connection string format correct
- [ ] Database credentials secured
- [ ] Connection pooling configured (if needed)
- [ ] Database backup strategy planned

### 6.3 Initial Data
- [ ] Seed data strategy decided (if needed)
- [ ] FetchTrades tested and working
- [ ] Scheduled data fetching configured

## 7. Documentation

### 7.1 README
- [ ] README.md updated with Docker instructions
- [ ] Quick start guide present
- [ ] Prerequisites listed
- [ ] Security configuration documented
- [ ] API endpoints documented
- [ ] Environment variables documented

### 7.2 Security Documentation
- [ ] SECURITY.md exists
- [ ] API key generation instructions
- [ ] CORS configuration explained
- [ ] Rate limiting documented
- [ ] Health checks explained
- [ ] Security best practices included

### 7.3 Docker Documentation
- [ ] DOCKER.md exists
- [ ] Docker setup instructions
- [ ] Production deployment guide
- [ ] Reverse proxy setup (Nginx/Caddy)
- [ ] Troubleshooting section
- [ ] Backup/restore procedures

## 8. Testing

### 8.1 Local Testing
- [ ] Application runs locally without errors
- [ ] All endpoints respond correctly
- [ ] Database operations work
- [ ] Tests pass locally

### 8.2 Docker Testing
- [ ] Application runs in Docker
- [ ] Can connect to PostgreSQL container
- [ ] Health checks pass
- [ ] Logs show no errors
- [ ] Migrations run successfully in container

**How to verify:**
```bash
# Start with Docker
docker-compose up -d

# Run migrations
docker-compose exec api dotnet ef database update

# Test health
curl http://localhost:5000/health/ready

# Test API endpoint (with key)
curl -H "X-API-Key: dev-api-key-change-in-production" \
  http://localhost:5000/api/InsiderTrades

# Check logs
docker-compose logs -f api
```

### 8.3 Security Testing
- [ ] API key required for protected endpoints
- [ ] Health checks work without API key
- [ ] Rate limiting triggers correctly
- [ ] CORS blocks unauthorized origins
- [ ] Security headers present

## 9. Production Readiness

### 9.1 Environment Setup
- [ ] Production server/platform chosen
- [ ] Docker and Docker Compose installed
- [ ] Domain name configured
- [ ] DNS pointing to server
- [ ] Firewall configured

### 9.2 Secrets Management
- [ ] Production API key generated
- [ ] Database password generated
- [ ] Secrets stored securely (not in git)
- [ ] Environment variables configured on server

### 9.3 SSL/TLS
- [ ] SSL certificate obtained (Let's Encrypt)
- [ ] Reverse proxy configured (Nginx or Caddy)
- [ ] HTTPS working
- [ ] HTTP redirects to HTTPS
- [ ] SSL labs test passed (A+ rating)

**Test SSL:**
```bash
# After deployment
curl https://api.yourdomain.com/health
```

### 9.4 Monitoring
- [ ] Health check monitoring setup
- [ ] Log aggregation configured (optional)
- [ ] Error alerting configured (optional)
- [ ] Uptime monitoring configured (optional)

### 9.5 Backup Strategy
- [ ] Database backup plan documented
- [ ] Backup scripts created
- [ ] Backup restoration tested
- [ ] Backup schedule configured

**Example backup:**
```bash
docker-compose exec postgres pg_dump -U aktiekoll_user aktiekoll > backup.sql
```

## 10. Frontend Integration

### 10.1 API Communication
- [ ] Frontend has API URL configured
- [ ] Frontend has API key configured
- [ ] Frontend sends X-API-Key header
- [ ] Frontend handles rate limiting (429 errors)
- [ ] Frontend handles authentication errors (401)

**Frontend example:**
```javascript
const API_URL = 'https://api.yourdomain.com';
const API_KEY = 'your-api-key';

fetch(`${API_URL}/api/InsiderTrades`, {
  headers: {
    'X-API-Key': API_KEY,
    'Content-Type': 'application/json'
  }
})
```

### 10.2 CORS
- [ ] Frontend domain added to FrontendUrl
- [ ] CORS working in production
- [ ] Preflight requests handled
- [ ] No CORS errors in browser console

## 11. CI/CD Workflows

### 11.1 Build Pipeline
- [ ] `.github/workflows/ci.yml` exists
- [ ] Triggers on push to main/develop
- [ ] Builds Release configuration
- [ ] Runs all tests
- [ ] Reports failures

### 11.2 Data Fetching
- [ ] `.github/workflows/cron-fetch.yml` exists
- [ ] Scheduled correctly (every 6 hours)
- [ ] Database connection configured via secrets
- [ ] Runs successfully

### 11.3 Dependency Updates
- [ ] `.github/workflows/renovate.yml` exists
- [ ] Runs daily
- [ ] Auto-merges dev dependencies (optional)

## 12. Final Verification

### 12.1 Pre-Deployment Checklist
- [ ] All CI checks pass
- [ ] No failing tests
- [ ] Security features tested
- [ ] Docker images build successfully
- [ ] Documentation complete
- [ ] Secrets configured
- [ ] Backup plan ready

### 12.2 Deployment Steps
1. [ ] Clone repository on production server
2. [ ] Create `.env` file with production values
3. [ ] Run `docker-compose up -d`
4. [ ] Run database migrations
5. [ ] Test health endpoints
6. [ ] Configure reverse proxy
7. [ ] Test API endpoints with API key
8. [ ] Configure frontend with API URL and key
9. [ ] Test end-to-end from frontend
10. [ ] Set up monitoring

### 12.3 Post-Deployment Verification
- [ ] Health checks return 200 OK
- [ ] API endpoints respond correctly
- [ ] Database queries work
- [ ] Frontend can communicate with backend
- [ ] HTTPS working
- [ ] Rate limiting working
- [ ] Logs show no errors
- [ ] No security warnings

**Verification commands:**
```bash
# Health checks
curl https://api.yourdomain.com/health
curl https://api.yourdomain.com/health/ready

# Test API (should fail without key)
curl https://api.yourdomain.com/api/InsiderTrades

# Test API (should succeed with key)
curl -H "X-API-Key: your-production-key" \
  https://api.yourdomain.com/api/InsiderTrades

# Check headers
curl -I https://api.yourdomain.com/health

# SSL test
openssl s_client -connect api.yourdomain.com:443 -servername api.yourdomain.com
```

## 13. Troubleshooting Common Issues

### CI Build Failures

**Issue: "Package 'AspNetCore.HealthChecks.NpgSql' not found"**
```bash
# Solution: Add to Directory.Packages.props
<PackageVersion Include="AspNetCore.HealthChecks.NpgSql" Version="9.0.2" />
```

**Issue: "Build failed with errors"**
```bash
# Check locally first
cd src
dotnet clean
dotnet restore AktieKoll.slnx
dotnet build AktieKoll.slnx --configuration Release

# Check for missing usings or syntax errors
```

**Issue: "Tests failing"**
```bash
# Run tests with verbose output
cd src
dotnet test AktieKoll.slnx --configuration Release --verbosity detailed

# Check test logs for specific failures
```

### Docker Issues

**Issue: "Container won't start"**
```bash
# Check logs
docker-compose logs api

# Common causes:
# - Missing environment variables
# - Database not ready
# - Port conflict
```

**Issue: "Can't connect to database"**
```bash
# Check postgres is healthy
docker-compose ps

# Check connection string
docker-compose exec api env | grep ConnectionStrings

# Test database directly
docker-compose exec postgres psql -U aktiekoll_user -d aktiekoll
```

**Issue: "Health check failing"**
```bash
# Check inside container
docker-compose exec api curl http://localhost:8080/health

# Check if app is running
docker-compose exec api ps aux

# Check listening ports
docker-compose exec api netstat -tlnp
```

## Quick Commands Reference

```bash
# Local Development
cd src
dotnet restore AktieKoll.slnx
dotnet build AktieKoll.slnx --configuration Release
dotnet test AktieKoll.slnx --configuration Release
cd AktieKoll
dotnet run

# Docker Development
docker-compose up -d
docker-compose exec api dotnet ef database update
docker-compose logs -f api
docker-compose down

# Production Deployment
git clone https://github.com/yourusername/AktieKoll.git
cd AktieKoll
cp .env.example .env
nano .env  # Edit with production values
docker-compose up -d
docker-compose exec api dotnet ef database update
curl http://localhost:5000/health/ready

# Generate Secrets
openssl rand -base64 32  # API key
openssl rand -base64 16  # Database password

# Database Backup
docker-compose exec postgres pg_dump -U aktiekoll_user aktiekoll > backup.sql

# View Logs
docker-compose logs -f api
docker-compose logs -f postgres
```

## Success Criteria

Your deployment is ready when:

✅ All items in this checklist are completed
✅ CI/CD pipeline passes all checks
✅ Application runs successfully in Docker
✅ Health checks return 200 OK
✅ API endpoints respond correctly with authentication
✅ Database migrations run successfully
✅ Frontend can communicate with backend
✅ Security features are tested and working
✅ Documentation is complete
✅ Production environment is configured

---

**Once all items are checked, you're ready for production deployment! 🚀**
