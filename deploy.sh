#!/bin/bash
#
# Quick deployment script for TRMNL BYOD Server with auto-updates
#

set -e

echo "==================================="
echo "TRMNL BYOD Server - Quick Deploy"
echo "==================================="
echo ""

# Configuration
CONTAINER_NAME="trmnl-byos"
WATCHTOWER_NAME="watchtower"
IMAGE="ghcr.io/bradreimer/trmnl-byos-aspnet:latest"
PORT="2300"
DATA_DIR="${HOME}/trmnl-data"
UPDATE_INTERVAL="3600"  # 1 hour

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Error: Docker is not installed"
    echo "   Install Docker from: https://docs.docker.com/get-docker/"
    exit 1
fi

# Check if Docker is running
if ! docker info &> /dev/null; then
    echo "❌ Error: Docker daemon is not running"
    echo "   Start Docker and try again"
    exit 1
fi

echo "✓ Docker is installed and running"
echo ""

# Create data directory
if [ ! -d "$DATA_DIR" ]; then
    mkdir -p "$DATA_DIR"
    echo "✓ Created data directory: $DATA_DIR"
else
    echo "✓ Data directory exists: $DATA_DIR"
fi
echo ""

# Stop and remove existing containers
echo "Checking for existing containers..."
if docker ps -a | grep -q "$CONTAINER_NAME"; then
    echo "  Stopping existing $CONTAINER_NAME container..."
    docker stop "$CONTAINER_NAME" 2>/dev/null || true
    docker rm "$CONTAINER_NAME" 2>/dev/null || true
    echo "  ✓ Removed old container"
fi

if docker ps -a | grep -q "$WATCHTOWER_NAME"; then
    echo "  Stopping existing $WATCHTOWER_NAME container..."
    docker stop "$WATCHTOWER_NAME" 2>/dev/null || true
    docker rm "$WATCHTOWER_NAME" 2>/dev/null || true
    echo "  ✓ Removed old watchtower"
fi
echo ""

# Pull the latest image
echo "Pulling latest TRMNL BYOD image..."
docker pull "$IMAGE"
echo "✓ Image pulled successfully"
echo ""

# Run the TRMNL container
echo "Starting TRMNL BYOD server..."
docker run -d \
  --name "$CONTAINER_NAME" \
  --restart unless-stopped \
  -p "$PORT:3000" \
  -v "$DATA_DIR:/data" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  "$IMAGE"

# Wait a moment for container to start
sleep 2

# Check if container is running
if docker ps | grep -q "$CONTAINER_NAME"; then
    echo "✓ TRMNL BYOD server is running"
else
    echo "❌ Error: Container failed to start"
    echo "   Check logs with: docker logs $CONTAINER_NAME"
    exit 1
fi
echo ""

# Run Watchtower for auto-updates
echo "Starting Watchtower for automatic updates..."
docker run -d \
  --name "$WATCHTOWER_NAME" \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower \
  "$CONTAINER_NAME" \
  --interval "$UPDATE_INTERVAL" \
  --cleanup

echo "✓ Watchtower is monitoring for updates"
echo ""

# Test the server
echo "Testing server health..."
sleep 2
if curl -s http://localhost:$PORT/ | grep -q "ok"; then
    echo "✓ Server is healthy and responding"
else
    echo "⚠ Warning: Server might not be fully started yet"
    echo "   Check status with: docker logs $CONTAINER_NAME"
fi
echo ""

# Summary
echo "==================================="
echo "✅ Deployment Complete!"
echo "==================================="
echo ""
echo "Server URL:    http://localhost:$PORT"
echo "Health Check:  curl http://localhost:$PORT"
echo "Data Directory: $DATA_DIR"
echo "Update Interval: Every $(($UPDATE_INTERVAL / 60)) minutes"
echo ""
echo "Useful Commands:"
echo "  View logs:       docker logs -f $CONTAINER_NAME"
echo "  Stop server:     docker stop $CONTAINER_NAME"
echo "  Start server:    docker start $CONTAINER_NAME"
echo "  Restart server:  docker restart $CONTAINER_NAME"
echo ""
echo "  Watchtower logs: docker logs -f $WATCHTOWER_NAME"
echo "  Force update:    docker restart $WATCHTOWER_NAME"
echo ""
echo "Next Steps:"
echo "  1. Configure your TRMNL device to point to this server"
echo "  2. Monitor logs: docker logs -f $CONTAINER_NAME"
echo "  3. Updates will happen automatically every hour"
echo ""
