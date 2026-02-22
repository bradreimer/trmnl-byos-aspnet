# Running the TRMNL BYOD Container

## Quick Start

### Pull and Run the Latest Image

```bash
# Pull the latest image
docker pull ghcr.io/bradreimer/trmnl-byos-aspnet:latest

# Run the container
docker run -d \
  --name trmnl-byos \
  --restart unless-stopped \
  -p 3000:3000 \
  -v ~/trmnl-data:/data \
  ghcr.io/bradreimer/trmnl-byos-aspnet:latest
```

### Verify It's Running

```bash
# Check container status
docker ps | grep trmnl-byos

# View logs
docker logs trmnl-byos

# Follow logs in real-time
docker logs -f trmnl-byos

# Test the API
curl http://localhost:3000
```

## Configuration Options

### Port Mapping

```bash
# Use a different host port (e.g., 8080)
docker run -d \
  --name trmnl-byos \
  -p 8080:3000 \
  -v ~/trmnl-data:/data \
  ghcr.io/bradreimer/trmnl-byos-aspnet:latest
```

### Data Volume

The `/data` volume stores uploaded images:

```bash
# Use a specific directory
-v /path/to/your/data:/data

# Use a Docker volume (managed by Docker)
docker volume create trmnl-data
-v trmnl-data:/data
```

### Environment Variables

```bash
docker run -d \
  --name trmnl-byos \
  -p 3000:3000 \
  -v ~/trmnl-data:/data \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://0.0.0.0:3000 \
  ghcr.io/bradreimer/trmnl-byos-aspnet:latest
```

## Managing the Container

### Stop the Container

```bash
docker stop trmnl-byos
```

### Start the Container

```bash
docker start trmnl-byos
```

### Restart the Container

```bash
docker restart trmnl-byos
```

### Remove the Container

```bash
docker stop trmnl-byos
docker rm trmnl-byos
```

### Update to Latest Image

```bash
# Stop and remove old container
docker stop trmnl-byos
docker rm trmnl-byos

# Pull latest image
docker pull ghcr.io/bradreimer/trmnl-byos-aspnet:latest

# Run new container
docker run -d \
  --name trmnl-byos \
  --restart unless-stopped \
  -p 3000:3000 \
  -v ~/trmnl-data:/data \
  ghcr.io/bradreimer/trmnl-byos-aspnet:latest
```

## Using Docker Compose

Create `docker-compose.yml`:

```yaml
version: '3.8'

services:
  trmnl-byos:
    image: ghcr.io/bradreimer/trmnl-byos-aspnet:latest
    container_name: trmnl-byos
    restart: unless-stopped
    ports:
      - "3000:3000"
    volumes:
      - ./data:/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
```

Then run:

```bash
# Start
docker-compose up -d

# View logs
docker-compose logs -f

# Stop
docker-compose down

# Update and restart
docker-compose pull
docker-compose up -d
```

## Health Check

The server provides a health check endpoint:

```bash
curl http://localhost:3000
# Response: {"status":"ok","service":"trmnl-byod-dotnet"}
```

## Troubleshooting

### Container Won't Start

```bash
# Check logs for errors
docker logs trmnl-byos

# Check if port is already in use
lsof -i :3000
# or
netstat -an | grep 3000
```

### Permission Issues with Data Volume

```bash
# Ensure the data directory is writable
chmod 755 ~/trmnl-data

# Check container user
docker exec trmnl-byos whoami
```

### View Container Details

```bash
# Inspect container
docker inspect trmnl-byos

# Check resource usage
docker stats trmnl-byos
```

## Automatic Updates

See [AUTO_UPDATE.md](AUTO_UPDATE.md) for automatic update configuration using Watchtower.
