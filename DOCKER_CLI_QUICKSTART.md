# Quick Start: Docker CLI Commands

## Pull the Latest Image

```bash
# Replace YOUR_USERNAME with your GitHub username
docker pull ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:latest
```

## Run the Container

```bash
docker run -d \
  --name trmnl-byos \
  -p 3000:3000 \
  -v $(pwd)/data:/data \
  ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:latest
```

## Common Commands

```bash
# Check if container is running
docker ps

# View container logs
docker logs trmnl-byos

# Follow logs in real-time
docker logs -f trmnl-byos

# Stop the container
docker stop trmnl-byos

# Start the container again
docker start trmnl-byos

# Remove the container
docker rm trmnl-byos

# Remove the image
docker rmi ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:latest
```

## Pull Specific Versions

```bash
# Pull by branch name
docker pull ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:main

# Pull by commit SHA (first 7 characters)
docker pull ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:main-abc1234

# Pull pull request build
docker pull ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:pr-42
```

## Test the API

```bash
# Check if server is running
curl http://localhost:3000/api/setup \
  -H "ID: test-device" \
  -H "MODEL: test-model" \
  -H "FIRMWARE: 1.0.0" \
  -H "REFRESH_RATE: 60"
```

## Docker Compose (Alternative)

```bash
# Create docker-compose.yml with the image reference
# Then run:
docker-compose pull
docker-compose up -d
docker-compose logs -f
docker-compose down
```

---

For detailed documentation, see [DOCKER_DEPLOYMENT.md](DOCKER_DEPLOYMENT.md)
