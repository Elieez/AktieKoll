# Security Configuration Guide

This document outlines the security features implemented in the AktieKoll API and how to configure them.

## Security Features

### 1. API Key Authentication

All API endpoints (except `/health` and `/health/ready`) require authentication via API key.

**How it works:**
- Frontend must include `X-API-Key` header in all requests
- Requests without a valid API key receive a 401 Unauthorized response
- Health check endpoints are exempt to allow monitoring

**Configuration:**

```bash
# Development (appsettings.Development.json)
"ApiKey": "dev-api-key-change-in-production"

# Production (use environment variable)
export ApiKey="your-secure-random-api-key-here"
```

**Frontend Integration:**

```javascript
// Example fetch request from frontend
fetch('https://your-api.com/api/InsiderTrades', {
  headers: {
    'X-API-Key': 'your-api-key-here',
    'Content-Type': 'application/json'
  }
})
```

### 2. Rate Limiting

Protects the API from abuse and DoS attacks.

**Current Configuration:**
- **Global Limit:** 100 requests per minute per IP address
- **Response:** 429 Too Many Requests when limit exceeded
- **Window:** Fixed 1-minute rolling window
- **Auto-replenishment:** Enabled

**Customization:**

To adjust rate limits, modify `Program.cs`:

```csharp
PermitLimit = 100,              // Change request limit
Window = TimeSpan.FromMinutes(1) // Change time window
```

### 3. CORS (Cross-Origin Resource Sharing)

Restricts API access to your frontend domain only.

**Configuration:**

```bash
# Development
"FrontendUrl": "http://localhost:3000"

# Production
export FrontendUrl="https://your-frontend-domain.com"
```

**Important:** Only requests from the configured frontend URL will be accepted.

### 4. HTTPS Enforcement

- `UseHttpsRedirection()` redirects all HTTP requests to HTTPS
- `UseHsts()` enabled in production (HTTP Strict Transport Security)

### 5. Security Headers

The following security headers are automatically added to all responses:

- `X-Content-Type-Options: nosniff` - Prevents MIME type sniffing
- `X-Frame-Options: DENY` - Prevents clickjacking attacks
- `X-XSS-Protection: 1; mode=block` - Enables XSS filtering
- `Referrer-Policy: strict-origin-when-cross-origin` - Controls referrer information

### 6. Health Check Endpoints

Two endpoints for monitoring:

- `/health` - Basic health check
- `/health/ready` - Includes database connectivity check

**Response Format:**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567"
}
```

## Environment Variables

### Required for Production

| Variable | Description | Example |
|----------|-------------|---------|
| `ApiKey` | API key for frontend authentication | `a1b2c3d4-e5f6-7890-abcd-ef1234567890` |
| `FrontendUrl` | Your frontend domain URL | `https://aktiekoll.com` |
| `ConnectionStrings__PostgresConnection` | PostgreSQL connection string | `Host=db;Database=aktiekoll;Username=user;Password=pass` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |

### Setting Environment Variables

**Linux/macOS:**
```bash
export ApiKey="your-secret-key"
export FrontendUrl="https://your-domain.com"
export ConnectionStrings__PostgresConnection="Host=localhost;Database=aktiekoll;..."
```

**Docker Compose:**
```yaml
environment:
  - ApiKey=${API_KEY}
  - FrontendUrl=${FRONTEND_URL}
  - ConnectionStrings__PostgresConnection=${DATABASE_URL}
```

**GitHub Actions:**
```yaml
env:
  ApiKey: ${{ secrets.API_KEY }}
  FrontendUrl: ${{ secrets.FRONTEND_URL }}
```

## Generating Secure API Keys

Use a cryptographically secure random generator:

**Linux/macOS:**
```bash
openssl rand -base64 32
```

**PowerShell:**
```powershell
[System.Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

**Online (use with caution):**
- https://www.uuidgenerator.net/
- Generate a UUID v4

## Security Best Practices

1. **Never commit API keys** - Use environment variables or secrets management
2. **Rotate API keys regularly** - Especially after team member changes
3. **Use HTTPS in production** - Never deploy without TLS/SSL certificates
4. **Monitor health endpoints** - Set up alerts for database failures
5. **Review rate limits** - Adjust based on your frontend's actual usage patterns
6. **Keep dependencies updated** - Renovate bot is configured for automatic updates
7. **Use strong database credentials** - Never use default passwords

## Testing Security Features

### Test API Key Authentication

```bash
# Should fail (401 Unauthorized)
curl -X GET https://your-api.com/api/InsiderTrades

# Should succeed
curl -X GET https://your-api.com/api/InsiderTrades \
  -H "X-API-Key: your-api-key"
```

### Test Rate Limiting

```bash
# Send 101 requests rapidly
for i in {1..101}; do
  curl -X GET https://your-api.com/api/InsiderTrades \
    -H "X-API-Key: your-api-key"
done
# Request 101 should return 429 Too Many Requests
```

### Test Health Checks

```bash
# Basic health check (no auth required)
curl -X GET https://your-api.com/health

# Readiness check with database (no auth required)
curl -X GET https://your-api.com/health/ready
```

## Troubleshooting

### "API key is missing"
- Ensure frontend sends `X-API-Key` header
- Check header name (case-sensitive)

### "Invalid API key"
- Verify API key matches server configuration
- Check for whitespace or encoding issues

### "429 Too Many Requests"
- Rate limit exceeded
- Wait 60 seconds or adjust rate limits
- Check if multiple users share the same IP

### Health check fails
- Verify database is running and accessible
- Check connection string configuration
- Review database firewall rules

## Migration from Development to Production

1. Generate a secure API key (see above)
2. Set environment variables on production server
3. Update frontend to use production API URL and key
4. Test health endpoints before going live
5. Monitor rate limit metrics
6. Set up SSL/TLS certificate
7. Configure DNS and firewall rules

## Support

For security issues or questions, please review the main README.md or create an issue in the repository.
