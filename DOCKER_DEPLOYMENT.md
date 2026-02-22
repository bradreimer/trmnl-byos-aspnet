# Docker Deployment Guide

## Pulling and Running the Container Image

### Prerequisites

- Docker CLI installed on your machine
- Access to the GitHub Container Registry (public repositories are accessible without authentication)

### Authentication (for private repositories)

If the repository is private, authenticate with GitHub Container Registry:

```bash
echo $GITHUB_PAT | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin
```

Replace `$GITHUB_PAT` with your GitHub Personal Access Token (with `read:packages` scope).

### Pull the Latest Image

```bash
# Pull the latest image from the main branch
docker pull ghcr.io/$(echo $GITHUB_REPOSITORY | tr '[:upper:]' '[:lower:]'):latest

# Or pull a specific version by SHA
docker pull ghcr.io/$(echo $GITHUB_REPOSITORY | tr '[:upper:]' '[:lower:]'):main-sha123456
```

**Note:** Replace `$GITHUB_REPOSITORY` with your actual repository path (e.g., `username/trmnl-byos-aspnet`).

### Run the Container

#### Basic Run

```bash
docker run -d \
  --name trmnl-byos \
  -p 3000:3000 \
  -v $(pwd)/data:/data \
  ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:latest
```

#### Run with Custom Configuration

```bash
docker run -d \
  --name trmnl-byos \
  -p 3000:3000 \
  -v $(pwd)/data:/data \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://0.0.0.0:3000 \
  ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:latest
```

### Verify the Container is Running

```bash
docker ps
```

### View Logs

```bash
docker logs trmnl-byos
```

### Stop and Remove the Container

```bash
docker stop trmnl-byos
docker rm trmnl-byos
```

## Available Image Tags

The CI/CD pipeline automatically creates multiple tags:

- `latest` - Latest build from the main branch
- `main` - Latest build from the main branch
- `main-<sha>` - Specific commit from main branch
- `pr-<number>` - Pull request builds
- Version tags (if using semantic versioning)

## Using with Docker Compose

Create or update your `docker-compose.yml`:

```yaml
version: '3.8'

services:
  trmnl-byos:
    image: ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:latest
    ports:
      - "3000:3000"
    volumes:
      - ./data:/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    restart: unless-stopped
```

Then run:

```bash
docker-compose pull
docker-compose up -d
```

## Image Variants

### Specific Commit SHA

Pull a specific commit build:

```bash
docker pull ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:main-abc1234
```

### Pull Request Testing

Test a pull request before merging:

```bash
docker pull ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:pr-42
```

## CI/CD Pipeline Overview

The GitHub Actions workflow performs the following:

1. **Code Quality** - Runs formatters and code analysis
2. **Testing** - Executes all unit and integration tests with code coverage
3. **Docker Build** - Builds multi-arch container images
4. **Artifact Publishing** - Pushes images to GitHub Container Registry

All builds are cached to improve build times on subsequent runs.

## Troubleshooting

### Permission Denied

If you get permission errors when pulling images:

```bash
# Ensure you're logged in
docker login ghcr.io

# Check your authentication status
cat ~/.docker/config.json
```

### Image Not Found

Ensure the repository and tag exist:

1. Visit `https://github.com/YOUR_USERNAME/trmnl-byos-aspnet/pkgs/container/trmnl-byos-aspnet`
2. Verify the tag you're trying to pull is listed

### Container Won't Start

Check logs for errors:

```bash
docker logs trmnl-byos --tail 100
```

## Local Development

To build the image locally instead of pulling from the registry:

```bash
# Build from Dockerfile
docker build -t trmnl-byos-aspnet:local .

# Run your local build
docker run -d \
  --name trmnl-byos \
  -p 3000:3000 \
  -v $(pwd)/data:/data \
  trmnl-byos-aspnet:local
```
