#!/bin/bash

# Docker Compose Helper Script for TRMNL BYOS ASP.NET
# Usage: ./compose.sh [up|down|restart|logs|status]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Display usage information
usage() {
    echo "Usage: $0 {up|down|restart|logs|status}"
    echo ""
    echo "Commands:"
    echo "  up       - Start the containers"
    echo "  down     - Stop the containers"
    echo "  restart  - Restart the containers"
    echo "  logs     - View container logs"
    echo "  status   - Show container status"
    exit 1
}

# Start containers
docker_up() {
    echo -e "${YELLOW}Starting TRMNL BYOS containers...${NC}"
    cd "$SCRIPT_DIR"
    docker compose up -d
    echo -e "${GREEN}✓ Containers started${NC}"
    echo "Service available at http://localhost:2300"
}

# Stop containers
docker_down() {
    echo -e "${YELLOW}Stopping TRMNL BYOS containers...${NC}"
    cd "$SCRIPT_DIR"
    docker compose down
    echo -e "${GREEN}✓ Containers stopped${NC}"
}

# Restart containers
docker_restart() {
    echo -e "${YELLOW}Restarting TRMNL BYOS containers...${NC}"
    cd "$SCRIPT_DIR"
    docker compose restart
    echo -e "${GREEN}✓ Containers restarted${NC}"
}

# View logs
docker_logs() {
    cd "$SCRIPT_DIR"
    docker compose logs -f
}

# Show status
docker_status() {
    cd "$SCRIPT_DIR"
    docker compose ps
}

# Main script logic
if [[ $# -eq 0 ]]; then
    usage
fi

case "$1" in
    up)
        docker_up
        ;;
    down)
        docker_down
        ;;
    restart)
        docker_restart
        ;;
    logs)
        docker_logs
        ;;
    status)
        docker_status
        ;;
    *)
        echo -e "${RED}Error: Unknown command '$1'${NC}"
        usage
        ;;
esac
