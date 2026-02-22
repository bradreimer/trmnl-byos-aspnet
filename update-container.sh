#!/bin/bash
#
# Container Auto-Update Script (What's Up Docker - wut integration)
# Monitors and updates TRMNL BYOS container with latest image
#

CONTAINER_NAME="trmnl-byos"
IMAGE="ghcr.io/bradreimer/trmnl-byos-aspnet:latest"
PORT="2300"
DATA_DIR="${HOME}/trmnl-data"
UPDATE_INTERVAL="${1:-3600}"  # 1 hour default

echo "Container Update Monitor Started"
echo "Container: $CONTAINER_NAME"
echo "Image: $IMAGE"
echo "Check interval: $((UPDATE_INTERVAL / 60)) minutes"
echo ""

while true; do
    sleep "$UPDATE_INTERVAL"
    
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Checking for updates..."
    
    # Get current container image digest
    if docker ps -a | grep -q "$CONTAINER_NAME"; then
        CURRENT_DIGEST=$(docker inspect "$CONTAINER_NAME" --format='{{index .Image}}' 2>/dev/null || echo "")
    else
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Container not found, skipping update check"
        continue
    fi
    
    # Pull latest image
    if docker pull "$IMAGE" > /dev/null 2>&1; then
        LATEST_DIGEST=$(docker inspect "$IMAGE" --format='{{index .RepoDigests 0}}' 2>/dev/null || echo "")
        
        if [ -n "$CURRENT_DIGEST" ] && [ -n "$LATEST_DIGEST" ] && [ "$CURRENT_DIGEST" != "$LATEST_DIGEST" ]; then
            echo "[$(date '+%Y-%m-%d %H:%M:%S')] Update available! Updating container..."
            
            # Stop and remove old container
            docker stop "$CONTAINER_NAME" 2>/dev/null || true
            docker rm "$CONTAINER_NAME" 2>/dev/null || true
            
            # Start new container with latest image
            docker run -d \
              --name "$CONTAINER_NAME" \
              --restart unless-stopped \
              -p "$PORT:3000" \
              -v "$DATA_DIR:/data" \
              -e ASPNETCORE_ENVIRONMENT=Production \
              "$IMAGE"
            
            echo "[$(date '+%Y-%m-%d %H:%M:%S')] Container updated successfully"
        else
            echo "[$(date '+%Y-%m-%d %H:%M:%S')] No updates available"
        fi
    else
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Failed to pull image, will try again next interval"
    fi
done
