# Automatic Container Updates with Watchtower

Watchtower automatically monitors your containers, pulls new images when available, and restarts containers with the latest version.

## Quick Setup

### Run Watchtower Alongside Your Container

```bash
# First, run your TRMNL container
docker run -d \
  --name trmnl-byos \
  --restart unless-stopped \
  -p 3000:3000 \
  -v ~/trmnl-data:/data \
  ghcr.io/bradreimer/trmnl-byos-aspnet:latest

# Then, run Watchtower to monitor it
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower \
  trmnl-byos \
  --interval 3600 \
  --cleanup
```

**What this does:**
- Checks for updates every hour (`3600` seconds)
- Only monitors the `trmnl-byos` container
- Automatically pulls new images
- Restarts the container with the new image
- Removes old images after updating (`--cleanup`)

## Configuration Options

### Check Every 5 Minutes

```bash
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower \
  trmnl-byos \
  --interval 300 \
  --cleanup
```

### Monitor All Containers

```bash
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower \
  --interval 3600 \
  --cleanup
```

### Run Once and Exit (Useful for Cron)

```bash
docker run --rm \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower \
  trmnl-byos \
  --run-once \
  --cleanup
```

### Update on a Schedule (Cron Expression)

```bash
# Check daily at 2 AM
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower \
  trmnl-byos \
  --schedule "0 0 2 * * *" \
  --cleanup
```

### Enable Notifications (Optional)

#### Slack Notifications

```bash
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e WATCHTOWER_NOTIFICATIONS=slack \
  -e WATCHTOWER_NOTIFICATION_SLACK_HOOK_URL=https://hooks.slack.com/services/YOUR/WEBHOOK/URL \
  containrrr/watchtower \
  trmnl-byos \
  --interval 3600 \
  --cleanup
```

#### Email Notifications

```bash
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e WATCHTOWER_NOTIFICATIONS=email \
  -e WATCHTOWER_NOTIFICATION_EMAIL_FROM=watchtower@example.com \
  -e WATCHTOWER_NOTIFICATION_EMAIL_TO=you@example.com \
  -e WATCHTOWER_NOTIFICATION_EMAIL_SERVER=smtp.gmail.com \
  -e WATCHTOWER_NOTIFICATION_EMAIL_SERVER_PORT=587 \
  -e WATCHTOWER_NOTIFICATION_EMAIL_SERVER_USER=your-email@gmail.com \
  -e WATCHTOWER_NOTIFICATION_EMAIL_SERVER_PASSWORD=your-app-password \
  containrrr/watchtower \
  trmnl-byos \
  --interval 3600 \
  --cleanup
```

## Docker Compose Setup

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

  watchtower:
    image: containrrr/watchtower
    container_name: watchtower
    restart: unless-stopped
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    command: trmnl-byos --interval 3600 --cleanup
```

Start both services:

```bash
docker-compose up -d
```

## Monitoring Watchtower

### View Watchtower Logs

```bash
docker logs watchtower

# Follow logs in real-time
docker logs -f watchtower
```

### Check When Last Update Happened

```bash
docker logs watchtower | grep "Updated"
```

## Managing Watchtower

### Stop Watchtower

```bash
docker stop watchtower
```

### Restart Watchtower

```bash
docker restart watchtower
```

### Remove Watchtower

```bash
docker stop watchtower
docker rm watchtower
```

### Update Watchtower Itself

```bash
docker stop watchtower
docker rm watchtower
docker pull containrrr/watchtower

# Run again with your preferred settings
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower \
  trmnl-byos \
  --interval 3600 \
  --cleanup
```

## Advanced Options

### Only Update on Specific Labels

Add a label to your TRMNL container:

```bash
docker run -d \
  --name trmnl-byos \
  --restart unless-stopped \
  --label com.centurylinklabs.watchtower.enable=true \
  -p 3000:3000 \
  -v ~/trmnl-data:/data \
  ghcr.io/bradreimer/trmnl-byos-aspnet:latest
```

Then configure Watchtower to only watch labeled containers:

```bash
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower \
  --label-enable \
  --interval 3600 \
  --cleanup
```

### Include Pre/Post Update Scripts

```bash
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ~/.watchtower-lifecycle:/scripts \
  containrrr/watchtower \
  trmnl-byos \
  --interval 3600 \
  --cleanup
```

Create lifecycle hooks in `~/.watchtower-lifecycle/`:
- `pre-update` - Runs before container update
- `post-update` - Runs after container update

### Delay Container Restart

Give your application time to gracefully shut down:

```bash
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower \
  trmnl-byos \
  --interval 3600 \
  --cleanup \
  --stop-timeout 30s
```

## Alternative: Manual Cron Job

If you prefer not to run Watchtower continuously:

```bash
# Edit crontab
crontab -e

# Add this line to check for updates daily at 3 AM
0 3 * * * docker run --rm -v /var/run/docker.sock:/var/run/docker.sock containrrr/watchtower trmnl-byos --run-once --cleanup >> /var/log/watchtower.log 2>&1
```

## Security Considerations

### Use Read-Only Docker Socket (if supported)

```bash
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock:ro \
  containrrr/watchtower \
  trmnl-byos \
  --interval 3600 \
  --cleanup
```

**Note:** Watchtower needs write access to manage containers, so this may not work in all scenarios.

### Authenticate with Private Registries

If using a private registry:

```bash
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ~/.docker/config.json:/config.json \
  -e DOCKER_CONFIG=/config.json \
  containrrr/watchtower \
  trmnl-byos \
  --interval 3600 \
  --cleanup
```

## Recommended Configuration

For most users, this is the recommended setup:

```bash
# Run Watchtower to check for updates every 6 hours
docker run -d \
  --name watchtower \
  --restart unless-stopped \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower \
  trmnl-byos \
  --interval 21600 \
  --cleanup \
  --include-restarting \
  --revive-stopped=false
```

**This configuration:**
- ✓ Checks every 6 hours (21600 seconds)
- ✓ Cleans up old images to save disk space
- ✓ Updates containers even if they're restarting
- ✓ Won't restart stopped containers (respects manual stops)

## Testing the Setup

1. Make a change to your code and push to GitHub
2. Wait for CI/CD to build and push the new image (~5 minutes)
3. Wait for Watchtower's next check interval
4. Watch the logs: `docker logs -f watchtower`
5. Verify the update: `docker logs trmnl-byos | head -20`

## Troubleshooting

### Watchtower Not Updating

```bash
# Check Watchtower logs for errors
docker logs watchtower

# Manually trigger an update
docker run --rm \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower \
  trmnl-byos \
  --run-once
```

### Permission Denied

```bash
# Ensure Docker socket is accessible
ls -l /var/run/docker.sock

# Your user should be in the docker group
groups
```

### Updates Not Detected

```bash
# Check if the image actually changed
docker pull ghcr.io/bradreimer/trmnl-byos-aspnet:latest

# Compare image IDs
docker images | grep trmnl-byos
```

## Further Reading

- [Watchtower Documentation](https://containrrr.dev/watchtower/)
- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
