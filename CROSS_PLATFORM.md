# Cross-Platform Build Guide

This project uses .NET 10 and supports building on Linux, macOS, and Windows.

## Supported Platforms

### Development & Building
- **Linux**: x64, ARM64
- **macOS**: x64 (Intel), ARM64 (Apple Silicon)
- **Windows**: x64, ARM64

### Docker Containers
- **linux/amd64** (x64)
- **linux/arm64** (ARM64/Apple Silicon)

## Building the Project

### Standard Build (Platform-Agnostic)

Build for your current platform:

```bash
# Debug configuration
dotnet build

# Release configuration
dotnet build --configuration Release
```

The project automatically detects your platform and builds accordingly.

### Publishing for Specific Platforms

When deploying, specify the target runtime identifier:

#### Linux x64
```bash
dotnet publish -c Release -r linux-x64 --self-contained true
```

#### Linux ARM64 (Raspberry Pi, ARM servers)
```bash
dotnet publish -c Release -r linux-arm64 --self-contained true
```

#### macOS x64 (Intel Macs)
```bash
dotnet publish -c Release -r osx-x64 --self-contained true
```

#### macOS ARM64 (Apple Silicon)
```bash
dotnet publish -c Release -r osx-arm64 --self-contained true
```

#### Windows x64
```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

#### Windows ARM64
```bash
dotnet publish -c Release -r win-arm64 --self-contained true
```

## Docker Multi-Platform Support

The project's Docker images are built for multiple architectures:

### Pull the Correct Architecture Automatically
```bash
docker pull ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:latest
```

Docker automatically selects the correct image for your architecture (amd64 or arm64).

### Verify Architecture
```bash
docker image inspect ghcr.io/YOUR_USERNAME/trmnl-byos-aspnet:latest | grep Architecture
```

### Build Multi-Platform Images Locally

```bash
# Create a builder instance
docker buildx create --use

# Build for both amd64 and arm64
docker buildx build --platform linux/amd64,linux/arm64 -t trmnl-byos-aspnet:multi .
```

## CI/CD Pipeline

The GitHub Actions workflow automatically:

1. Builds and tests on Ubuntu (linux-x64)
2. Creates Docker images for:
   - **linux/amd64** - Intel/AMD x64 processors
   - **linux/arm64** - ARM64 processors (AWS Graviton, Raspberry Pi 64-bit, Apple Silicon via Rosetta)

Both architectures are pushed to GitHub Container Registry with the same tag.

## Development Recommendations

### For Local Development
- Use standard `dotnet build` and `dotnet run` without specifying runtime
- The project will build for your native platform automatically

### For Testing Cross-Platform
- Use `dotnet publish -r <runtime>` to test specific platform builds
- Docker is recommended for testing Linux builds on macOS/Windows

### For Deployment
- **Docker**: Pull the multi-arch image (recommended)
- **Binary**: Publish with `--self-contained true` for the target runtime
- **Framework-dependent**: Omit `--self-contained` if .NET 10 is installed on target

## Runtime Identifiers (RID) Catalog

Common RIDs used in this project:

| Platform | Architecture | RID |
|----------|-------------|-----|
| Linux | x64 | `linux-x64` |
| Linux | ARM64 | `linux-arm64` |
| macOS | x64 | `osx-x64` |
| macOS | ARM64 | `osx-arm64` |
| Windows | x64 | `win-x64` |
| Windows | ARM64 | `win-arm64` |

For a complete list, see: https://learn.microsoft.com/en-us/dotnet/core/rid-catalog

## Troubleshooting

### Build Fails on Release Configuration

**Issue**: Old hardcoded RuntimeIdentifier prevented cross-platform builds

**Solution**: This has been fixed. The RuntimeIdentifier is now only specified during publish, not build.

### Docker Build Fails for ARM64

**Issue**: Docker BuildX not enabled or not enough memory

**Solutions**:
```bash
# Enable BuildX
docker buildx create --use

# Increase Docker memory (Docker Desktop settings)
# Recommended: 4GB+ for multi-platform builds
```

### Tests Fail on macOS

**Issue**: Tests reference platform-specific paths

**Solution**: Ensure all file paths use `Path.Combine()` and platform-agnostic separators.

## Performance Considerations

### Self-Contained vs Framework-Dependent

**Self-Contained** (default for Release):
- ✅ No .NET runtime required on target
- ✅ Specific .NET version guaranteed
- ❌ Larger binary size (~70MB)

**Framework-Dependent**:
- ✅ Smaller binary size (~1MB)
- ❌ Requires .NET 10 runtime on target
- ❌ Runtime version mismatch possible

### Architecture Performance

| Architecture | Use Case | Performance |
|-------------|----------|-------------|
| linux/amd64 | Cloud VMs, traditional servers | Excellent |
| linux/arm64 | AWS Graviton, cost-efficient cloud | Excellent |
| osx-arm64 | Apple Silicon development | Excellent |
| osx-x64 | Intel Mac development | Good |

## Additional Resources

- [.NET 10 Release Notes](https://github.com/dotnet/core/tree/main/release-notes/10.0)
- [Docker Multi-Platform Builds](https://docs.docker.com/build/building/multi-platform/)
- [ASP.NET Core Deployment](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/)
