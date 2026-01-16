# Production Readiness Issues

This document tracks remaining production issues that need to be addressed before or shortly after deployment.

## Status Legend
- 🔴 **Critical** - Must fix before production
- 🟡 **Important** - Should fix soon after deployment
- 🟢 **Nice-to-have** - Improves reliability but not blocking

---

## 🔴 Critical Issues

### 1. No Retry Policies for External APIs

**Status:** 🔴 Critical
**Priority:** High
**Affected Files:**
- `src/AktieKoll/Services/OpenFigiService.cs`
- `src/AktieKoll/Services/CsvFetchService.cs`
- `src/FetchTrades/Program.cs`
- `src/AktieKoll/Program.cs` (HttpClient configuration)

**Problem:**
The application makes HTTP calls to external services without retry logic or circuit breakers:

1. **OpenFIGI API** (`https://api.openfigi.com/v3/`)
   - Used to resolve stock symbols from ISIN codes
   - No retry on rate limits (HTTP 429)
   - No retry on transient network failures
   - Silent failure (returns null, losing symbol data)

2. **CSV Fetch Service** (`marknadssok.fi.se`)
   - Fetches insider trading data from Swedish Financial Authority
   - No retry on network failures
   - Immediate failure causes entire scheduled job to fail

**Impact:**
- GitHub Actions cron job (runs every 6 hours) will fail on transient network issues
- Missing stock symbol data when OpenFIGI has temporary problems
- Data gaps in the database when scheduled fetches fail
- No resilience against rate limiting

**Current Error Handling:**
```csharp
// OpenFigiService.cs - Silent failure
catch (Exception ex)
{
    logger.LogError(ex, "Failed to resolve ticker for {isin}", isin);
    return null;  // ⚠️ Symbol data is lost
}

// CsvFetchService.cs - Immediate failure
catch (Exception ex)
{
    logger.LogError(ex, "Failed to fetch insider trades");
    throw;  // ⚠️ No retry, entire job fails
}
```

**Recommended Solution:**

Install Polly for resilience policies:

```bash
cd src
dotnet add AktieKoll package Microsoft.Extensions.Http.Polly
dotnet add FetchTrades package Microsoft.Extensions.Http.Polly
```

Add to `Directory.Packages.props`:
```xml
<PackageVersion Include="Microsoft.Extensions.Http.Polly" Version="8.0.0" />
```

**Implementation Example:**

Update `Program.cs` to add retry policies:

```csharp
using Polly;
using Polly.Extensions.Http;

// Add retry policy helper
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // Handles 5xx and 408
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) // Handle 429
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                var logger = context.GetLogger();
                logger.LogWarning("Retry {RetryCount} after {Delay}s due to {StatusCode}",
                    retryCount, timespan.TotalSeconds, outcome.Result?.StatusCode);
            });
}

// Add timeout policy
static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
{
    return Policy.TimeoutAsync<HttpResponseMessage>(30); // 30 second timeout
}

// Configure HttpClients with policies
builder.Services.AddHttpClient<CsvFetchService>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetTimeoutPolicy());

builder.Services.AddHttpClient<IOpenFigiService, OpenFigiService>(client =>
    client.BaseAddress = new Uri("https://api.openfigi.com/v3/"))
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetTimeoutPolicy());
```

**Alternative: Manual Retry Logic**

If you don't want to use Polly, implement manual retries in the services:

```csharp
// Example for CsvFetchService
private async Task<string> FetchWithRetryAsync(string url, int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex) when (attempt < maxRetries)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
            logger.LogWarning(ex, "Attempt {Attempt} failed, retrying in {Delay}s", attempt, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
    throw new HttpRequestException($"Failed after {maxRetries} attempts");
}
```

**Testing the Fix:**

```bash
# Test retry behavior by simulating network issues
# Temporarily add failure injection to test retries

# Monitor logs to see retry attempts
docker-compose logs -f api | grep -i retry
```

---

### 2. HttpClient Timeout Not Configured

**Status:** 🔴 Critical
**Priority:** High
**Affected Files:**
- `src/AktieKoll/Program.cs` (lines 78-80)

**Problem:**
HttpClient uses default timeout of 100 seconds. If an external service hangs or is very slow, requests can hang for over a minute.

**Impact:**
- Hung requests tie up threads
- API becomes unresponsive during external service issues
- Poor user experience (very slow responses)

**Current Configuration:**
```csharp
builder.Services.AddHttpClient<CsvFetchService>();
builder.Services.AddHttpClient<IOpenFigiService, OpenFigiService>(client =>
    client.BaseAddress = new Uri("https://api.openfigi.com/v3/"));
// ⚠️ No timeout configured
```

**Recommended Solution:**

**Option 1: Using Polly (recommended if implementing retry policies)**

Already covered in Issue #1 above.

**Option 2: Configure timeout directly**

```csharp
builder.Services.AddHttpClient<CsvFetchService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30); // 30 second timeout
});

builder.Services.AddHttpClient<IOpenFigiService, OpenFigiService>(client =>
{
    client.BaseAddress = new Uri("https://api.openfigi.com/v3/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

**Option 3: Global timeout in HttpClientFactory**

```csharp
builder.Services.ConfigureAll<HttpClientFactoryOptions>(options =>
{
    options.HandlerLifetime = TimeSpan.FromMinutes(5);
});

// Then configure individual timeouts per client
```

**Recommended Timeout Values:**
- **API calls:** 10-30 seconds
- **CSV downloads:** 30-60 seconds (larger files)
- **Symbol resolution:** 10-15 seconds

---

## 🟡 Important Issues

### 3. GitHub Actions Secrets Not Documented

**Status:** 🟡 Important
**Priority:** Medium
**Affected Files:**
- `.github/workflows/renovate.yml`
- `.github/workflows/cron-fetch.yml`

**Problem:**
Two GitHub Actions workflows require secrets that aren't documented anywhere:

1. **RENOVATE_TOKEN** - Required for Renovate dependency updates
2. **POSTGRES_CONNECTION** - Required for scheduled data fetching

**Impact:**
- Renovate won't run without the token
- Cron job won't fetch insider trades without database connection

**Solution:**

**1. Document in README.md**

Add to the CI/CD section:

```markdown
### Required GitHub Secrets

Configure these secrets in your repository settings:

| Secret Name | Purpose | How to Generate |
|-------------|---------|-----------------|
| `RENOVATE_TOKEN` | Automated dependency updates | Create GitHub Personal Access Token with `repo` scope |
| `POSTGRES_CONNECTION` | Database connection for scheduled data fetching | Format: `Host=<host>;Database=aktiekoll;Username=<user>;Password=<password>` |

**Setting up secrets:**
1. Go to repository Settings > Secrets and variables > Actions
2. Click "New repository secret"
3. Add each secret with appropriate values
```

**2. Update DEPLOYMENT_CHECKLIST.md**

Add section under "11. CI/CD Workflows":

```markdown
### 11.4 GitHub Actions Secrets
- [ ] RENOVATE_TOKEN configured (for dependency updates)
- [ ] POSTGRES_CONNECTION configured (for scheduled data fetching)
- [ ] Secrets tested by manually triggering workflows
```

**3. Create Secret Setup Instructions**

```bash
# Generate Renovate token:
# 1. Go to https://github.com/settings/tokens
# 2. Generate new token (classic)
# 3. Select 'repo' scope
# 4. Copy token and add to repository secrets as RENOVATE_TOKEN

# Configure database connection:
# Use production database connection string
# Format: Host=your-db-host;Database=aktiekoll;Username=user;Password=pass
# Add to repository secrets as POSTGRES_CONNECTION
```

---

### 4. Configuration Inconsistency (appsettings.json)

**Status:** 🟡 Important
**Priority:** Low
**Affected Files:**
- `.gitignore` (line 407)
- `src/AktieKoll/appsettings.json`

**Problem:**
`appsettings.json` is listed in `.gitignore` but is also committed to the repository. This creates confusion about whether it should be tracked.

**Current State:**
```gitignore
# .gitignore line 407
appsettings.json
```

But the file exists in the repository with default configuration.

**Impact:**
- Minor confusion for developers
- Risk of accidentally committing secrets if developer adds them to appsettings.json

**Recommended Solution:**

**Option 1: Keep as template (Recommended)**

Remove from `.gitignore` since it's a template without secrets:

```bash
# Edit .gitignore and remove this line:
appsettings.json
```

**Option 2: Don't track, require manual creation**

Remove from repository and keep in `.gitignore`:

```bash
git rm src/AktieKoll/appsettings.json
# Keep in .gitignore
```

**Current Best Practice:**
- Keep `appsettings.json` as a template in the repo (no secrets)
- Use `appsettings.Development.json` and `appsettings.Production.json` for environment-specific overrides
- Use environment variables for secrets

---

### 5. Production Logging Configuration

**Status:** 🟡 Important
**Priority:** Medium
**Affected Files:**
- `src/AktieKoll/appsettings.Production.json`

**Problem:**
Production logging is set to `Warning` level, which means you won't see important informational logs.

**Current Configuration:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",  // ⚠️ Too restrictive
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Error"  // ⚠️ Too restrictive
    }
  }
}
```

**Impact:**
- Missing important application events in logs
- Harder to troubleshoot issues in production
- No visibility into normal application operations

**Recommended Solution:**

Update `appsettings.Production.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",              // See application events
      "Microsoft.AspNetCore": "Warning",      // Reduce ASP.NET noise
      "Microsoft.EntityFrameworkCore": "Warning"  // See query warnings
    }
  }
}
```

**What This Gives You:**
- **Information** - See important application events (data fetched, symbols resolved, etc.)
- **Warning** - See potential issues (retries, degraded performance)
- **Error** - See failures

**Log Levels Explained:**
- **Trace/Debug** - Very verbose, development only
- **Information** - Normal application events (✓ Use in production)
- **Warning** - Potential issues
- **Error** - Failures
- **Critical** - Catastrophic failures

---

## 🟢 Nice-to-Have Improvements

### 6. Structured Logging

**Status:** 🟢 Nice-to-have
**Priority:** Low

**Problem:**
Currently using basic console logging. No structured logging means harder log analysis.

**Recommendation:**
Consider adding Serilog for structured logging:

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
```

**Benefits:**
- Structured JSON logs
- Easy filtering and searching
- Integration with log aggregation services (Seq, ELK, Splunk)
- Better debugging in production

---

### 7. Database Connection Pooling Configuration

**Status:** 🟢 Nice-to-have
**Priority:** Low

**Problem:**
No explicit connection pooling configuration. Npgsql uses default pooling, but explicit configuration is better for production.

**Current:**
```
Host=localhost;Database=aktiekoll;Username=user;Password=pass
```

**Recommended:**
```
Host=localhost;Database=aktiekoll;Username=user;Password=pass;Min Pool Size=5;Max Pool Size=100;Connection Lifetime=300
```

**Benefits:**
- Prevents connection exhaustion
- Better performance under load
- Predictable resource usage

---

### 8. API Key Comparison Security

**Status:** 🟢 Nice-to-have
**Priority:** Low
**Affected Files:**
- `src/AktieKoll/Middleware/ApiKeyAuthenticationMiddleware.cs` (line 40)

**Problem:**
API key comparison uses `Equals()` which is vulnerable to timing attacks.

**Current Code:**
```csharp
if (!apiKey.Equals(extractedApiKey))  // ⚠️ Timing attack vulnerable
{
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    await context.Response.WriteAsync("Invalid API key");
    return;
}
```

**Recommended Fix:**

Use constant-time comparison:

```csharp
using System.Security.Cryptography;

// Replace the comparison with:
if (!CryptographicOperations.FixedTimeEquals(
    System.Text.Encoding.UTF8.GetBytes(apiKey),
    System.Text.Encoding.UTF8.GetBytes(extractedApiKey)))
{
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    await context.Response.WriteAsync("Invalid API key");
    return;
}
```

**Why This Matters:**
Timing attacks allow attackers to guess API keys by measuring response times. Constant-time comparison prevents this.

---

## Implementation Checklist

Use this checklist to track progress on addressing these issues:

### Critical Issues (Before Production)
- [ ] Add retry policies for external APIs (Issue #1)
- [ ] Configure HttpClient timeouts (Issue #2)

### Important Issues (Soon After Production)
- [ ] Document GitHub Actions secrets (Issue #3)
- [ ] Fix appsettings.json .gitignore inconsistency (Issue #4)
- [ ] Update production logging configuration (Issue #5)

### Nice-to-Have (When Time Permits)
- [ ] Add structured logging with Serilog (Issue #6)
- [ ] Configure database connection pooling (Issue #7)
- [ ] Use constant-time API key comparison (Issue #8)

---

## Testing Recommendations

After implementing fixes:

### Test Retry Policies
```bash
# Simulate network failures
# Monitor logs for retry attempts
docker-compose logs -f api | grep -i retry

# Test with actual external services
./scripts/migrate.sh docker
docker-compose exec api curl http://localhost:8080/api/InsiderTrades
```

### Test Timeouts
```bash
# Use a network tool to simulate slow responses
# Verify requests timeout after configured duration
```

### Test Health Checks
```bash
# Verify curl works in container
docker-compose exec api curl http://localhost:8080/health

# Check health from outside
curl http://localhost:5000/health/ready
```

---

## Support

For questions or issues with these recommendations:
1. Review the main documentation (README.md, SECURITY.md, DOCKER.md)
2. Check Polly documentation: https://github.com/App-vNext/Polly
3. Open an issue on GitHub

---

**Last Updated:** 2025-01-16
**Priority Order:** Fix Critical (#1, #2) → Important (#3-5) → Nice-to-have (#6-8)
