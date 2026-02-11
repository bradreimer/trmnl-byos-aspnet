# TRMNL BYOS Server (ASP.NET Core)

A lightweight, self‑contained .NET server that implements the TRMNL BYOS firmware API and the image ingestion API used by TRMNL HA (Home Assistant). This project is designed to run cleanly on Synology, Docker, Linux, or any container host.

## API Reference

This implementation is based on the TRMNL BYOS API documentation at:

[https://github.com/usetrmnl/byos_hanami/blob/main/doc/api.adoc](https://github.com/usetrmnl/byos_hanami/blob/main/doc/api.adoc)

The firmware interacts with three core endpoints:

1. GET `/api/setup`
2. GET `/api/display`
3. POST `/api/logs`

The BYOD server also supports:

4. POST `/api/screens/{id}/image`
5. GET `/screens/{id}.jpg`

All request/response bodies are strongly typed using C# type-safe objects (DTOs).

## Features

- Fully type‑safe minimal API (ASP.NET Core)
- Implements all firmware endpoints from the TRMNL BYOS spec
- Supports BYOD image upload and retrieval
- Self‑contained Linux‑x64 Docker image
- Synology‑friendly `docker-compose.yml`
- In‑memory screen registry with disk‑backed image storage
- Clean, extendable architecture

## Running with Docker

```bash
docker compose up -d --build
```

Server will be available at:

[http://localhost:2300](http://localhost:2300)

Images are stored in:

```plain
./data/
```

## Project Structure

```plain
TrmnlByos/
  Program.cs
  Models/
    SetupModels.cs
    LogModels.cs
    DisplayModels.cs
    ScreenInfo.cs
Dockerfile
docker-compose.yml
README.md
AGENTS.md
.gitignore
```
