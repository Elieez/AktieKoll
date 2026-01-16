# Docker Deployment Guide

This guide covers deploying AktieKoll using Docker and Docker Compose.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Configuration](#configuration)
- [Production Deployment](#production-deployment)
- [Troubleshooting](#troubleshooting)
- [Advanced Usage](#advanced-usage)

## Prerequisites

- **Docker**: 20.10 or higher ([Install Docker](https://docs.docker.com/get-docker/))
- **Docker Compose**: 2.0 or higher ([Install Docker Compose](https://docs.docker.com/compose/install/))

Verify installation:
```bash
docker --version
docker-compose --version
```

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/AktieKoll.git
cd AktieKoll
```

### 2. Create Environment File

```bash
cp .env.example .env
```

Edit `.env` and set your values:
```env
POSTGRES_PASSWORD=your-secure-password
API_KEY=your-secure-api-key
FRONTEND_URL=http://localhost:3000
```

Generate a secure API key:
```bash
openssl rand -base64 32
```

### 3. Start Services

```bash
docker-compose up -d
```

### 4. Run Database Migrations

```bash
docker-compose exec api dotnet ef database update
```

### 5. Verify Deployment

```bash
# Check health
curl http://localhost:5000/health/ready

# Check logs
docker-compose logs -f api
```

The API is now running at `http://localhost:5000`

## Architecture

The docker-compose setup includes:

```
┌─────────────────┐
│   Frontend      │ (Not included - separate deployment)
│  (Port 3000)    │
└────────┬────────┘
         │ HTTP + API Key
         ▼
┌─────────────────┐
│   API Service   │ ← Health checks
│  (Port 5000)    │ ← Rate limiting
└────────┬────────┘ ← CORS
         │
         ▼
┌─────────────────┐
│   PostgreSQL    │
│  (Port 5432)    │
└─────────────────┘
```

### Services

1. **postgres**: PostgreSQL 17 database
   - Port: 5432
   - Persistent volume for data
   - Health checks enabled

2. **api**: AktieKoll ASP.NET Core API
   - Port: 5000 (mapped to internal 8080)
   - Depends on healthy postgres
   - Health checks at `/health` and `/health/ready`
   - Runs as non-root user for security

3. **fetch-trades** (optional): Scheduled data fetcher
   - Runs every 6 hours
   - Can be enabled in docker-compose.yml

## Configuration

### Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `POSTGRES_PASSWORD` | Yes | `changeme123` | Database password |
| `API_KEY` | Yes | None | API authentication key |
| `FRONTEND_URL` | Yes | `http://localhost:3000` | CORS allowed origin |

### Docker Compose Configuration

The `docker-compose.yml` file is configured for both development and production use. Key features:

- **Persistent volumes**: Database data survives container restarts
- **Health checks**: Services wait for dependencies to be healthy
- **Restart policies**: Containers restart automatically on failure
- **Network isolation**: Services communicate on a private network

## Production Deployment

### 1. Server Setup

Requirements:
- Ubuntu 20.04+ or similar Linux distribution
- Docker and Docker Compose installed
- At least 2GB RAM, 10GB disk space
- Domain name pointed to your server

### 2. Clone and Configure

```bash
# On your production server
git clone https://github.com/yourusername/AktieKoll.git
cd AktieKoll

# Create production environment file
nano .env
```

Production `.env`:
```env
POSTGRES_PASSWORD=VerySecurePassword123!
API_KEY=generated-via-openssl-rand-base64-32
FRONTEND_URL=https://yourdomain.com
```

### 3. Start Services

```bash
docker-compose up -d
```

### 4. Run Migrations

```bash
docker-compose exec api dotnet ef database update
```

### 5. Set Up Reverse Proxy (HTTPS)

#### Option A: Nginx

Install Nginx:
```bash
sudo apt update
sudo apt install nginx certbot python3-certbot-nginx
```

Create Nginx configuration (`/etc/nginx/sites-available/aktiekoll`):
```nginx
server {
    listen 80;
    server_name api.yourdomain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

Enable and get SSL certificate:
```bash
sudo ln -s /etc/nginx/sites-available/aktiekoll /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
sudo certbot --nginx -d api.yourdomain.com
```

#### Option B: Caddy (Automatic HTTPS)

Install Caddy:
```bash
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update
sudo apt install caddy
```

Create Caddyfile (`/etc/caddy/Caddyfile`):
```
api.yourdomain.com {
    reverse_proxy localhost:5000
}
```

Reload Caddy:
```bash
sudo systemctl reload caddy
```

### 6. Enable Automatic Startup

Docker Compose services are configured with `restart: unless-stopped`, so they'll automatically start on server reboot.

### 7. Monitoring

Set up monitoring for the health endpoints:

```bash
# Create a simple health check script
cat > /usr/local/bin/check-aktiekoll.sh <<'EOF'
#!/bin/bash
if ! curl -f http://localhost:5000/health/ready &>/dev/null; then
    echo "API health check failed!" | mail -s "AktieKoll API Down" admin@yourdomain.com
fi
EOF

chmod +x /usr/local/bin/check-aktiekoll.sh

# Add to crontab (check every 5 minutes)
echo "*/5 * * * * /usr/local/bin/check-aktiekoll.sh" | crontab -
```

## Troubleshooting

### Container Won't Start

**Check logs:**
```bash
docker-compose logs api
docker-compose logs postgres
```

**Common issues:**
- Port 5000 already in use: Change port in docker-compose.yml
- Database won't start: Check disk space with `df -h`
- Permission errors: Ensure Docker has proper permissions

### Database Connection Failed

```bash
# Check if postgres is healthy
docker-compose ps

# Check postgres logs
docker-compose logs postgres

# Test database connection
docker-compose exec postgres psql -U aktiekoll_user -d aktiekoll -c "SELECT 1;"
```

### API Returns 500 Errors

```bash
# Check API logs
docker-compose logs -f api

# Common causes:
# - Database not migrated: Run migrations
# - Missing environment variables: Check .env file
# - Connection string incorrect: Verify postgres service name
```

### Migration Fails

```bash
# Try running migrations with verbose output
docker-compose exec api dotnet ef database update --verbose

# If database is locked, restart postgres
docker-compose restart postgres
sleep 5
docker-compose exec api dotnet ef database update
```

### Can't Access from Outside

**Firewall issues:**
```bash
# Check if port is open
sudo ufw status
sudo ufw allow 5000/tcp

# Or if using firewalld
sudo firewall-cmd --add-port=5000/tcp --permanent
sudo firewall-cmd --reload
```

### Health Check Failing

```bash
# Test health endpoint directly
docker-compose exec api curl http://localhost:8080/health
docker-compose exec api curl http://localhost:8080/health/ready

# Check if app is listening
docker-compose exec api netstat -tlnp
```

## Advanced Usage

### Building Custom Images

Build and tag images manually:

```bash
# Build API image
cd src
docker build -f AktieKoll/Dockerfile -t aktiekoll-api:1.0.0 .

# Build FetchTrades image
docker build -f FetchTrades/Dockerfile -t aktiekoll-fetch:1.0.0 .

# Tag for registry
docker tag aktiekoll-api:1.0.0 yourusername/aktiekoll-api:1.0.0
docker tag aktiekoll-api:1.0.0 yourusername/aktiekoll-api:latest

# Push to Docker Hub
docker push yourusername/aktiekoll-api:1.0.0
docker push yourusername/aktiekoll-api:latest
```

### Using External Database

To use an external PostgreSQL database instead of the Docker container:

1. Comment out the `postgres` service in `docker-compose.yml`
2. Update API environment to point to external database:
   ```yaml
   environment:
     ConnectionStrings__PostgresConnection: "Host=external-db.com;Database=aktiekoll;Username=user;Password=pass"
   ```

### Running FetchTrades

Enable the scheduled FetchTrades service:

1. Uncomment the `fetch-trades` service in `docker-compose.yml`
2. Restart services:
   ```bash
   docker-compose up -d
   ```

Or run manually:
```bash
docker-compose run --rm fetch-trades
```

### Scaling the API

Run multiple API instances behind a load balancer:

```bash
# Scale to 3 instances
docker-compose up -d --scale api=3

# Use Nginx or HAProxy to load balance between them
```

### Viewing Resource Usage

```bash
# View container stats
docker stats

# View disk usage
docker system df

# Clean up unused resources
docker system prune -a
```

### Database Backup

```bash
# Backup database
docker-compose exec postgres pg_dump -U aktiekoll_user aktiekoll > backup.sql

# Restore database
cat backup.sql | docker-compose exec -T postgres psql -U aktiekoll_user aktiekoll
```

### Updating the Application

```bash
# Pull latest code
git pull origin main

# Rebuild and restart
docker-compose up -d --build

# Run any new migrations
docker-compose exec api dotnet ef database update
```

## Security Best Practices

1. **Always use strong passwords** for `POSTGRES_PASSWORD` and `API_KEY`
2. **Never commit `.env` files** to version control
3. **Use HTTPS** in production (via reverse proxy)
4. **Regularly update** Docker images and dependencies
5. **Monitor logs** for suspicious activity
6. **Backup database** regularly
7. **Limit network exposure** - only expose necessary ports
8. **Use secrets management** for production (Docker secrets, Kubernetes secrets, etc.)

## Support

For issues or questions:
- Check the main [README.md](README.md)
- Review [SECURITY.md](SECURITY.md) for security configuration
- Open an issue on GitHub

## Additional Resources

- [Docker Documentation](https://docs.docker.com/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [PostgreSQL Docker Hub](https://hub.docker.com/_/postgres)
- [ASP.NET Core Docker Images](https://hub.docker.com/_/microsoft-dotnet-aspnet)
