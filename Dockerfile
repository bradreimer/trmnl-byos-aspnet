# --- Build stage -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Build arguments for multi-platform support
ARG TARGETARCH
WORKDIR /src

# Copy the project file from the subfolder
COPY TrmnlByos/TrmnlByos.csproj TrmnlByos/
RUN dotnet restore TrmnlByos/TrmnlByos.csproj

# Copy the full source tree
COPY . .

# Publish the app with architecture-specific runtime identifier
RUN if [ "$TARGETARCH" = "arm64" ]; then \
        dotnet publish TrmnlByos/TrmnlByos.csproj \
        -c Release \
        -o /app \
        /p:PublishSingleFile=true \
        /p:SelfContained=true \
        /p:RuntimeIdentifier=linux-arm64; \
    else \
        dotnet publish TrmnlByos/TrmnlByos.csproj \
        -c Release \
        -o /app \
        /p:PublishSingleFile=true \
        /p:SelfContained=true \
        /p:RuntimeIdentifier=linux-x64; \
    fi

# --- Runtime stage -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app

VOLUME ["/data"]

COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://0.0.0.0:3000

EXPOSE 3000

ENTRYPOINT ["./TrmnlByos"]
