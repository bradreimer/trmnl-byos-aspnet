# AI Agent Integration Notes

This file describes the API surface of the TRMNL BYOD server so that an AI agent
(or code generation assistant) can safely extend or modify the project.

## Reference Specification

All firmware endpoints are derived from:

https://github.com/usetrmnl/byos_hanami/blob/main/doc/api.adoc

This document defines:

- Firmware → Server communication
- Required request headers
- Expected JSON response shapes
- Logging format
- Display polling behavior

The implementation in this project mirrors the examples in the spec.

---

## Implemented Endpoints

### 1. `GET /api/setup`

- Reads firmware headers:
  - `ID`
  - `MODEL`
  - `FIRMWARE`
  - `REFRESH_RATE`
- Returns a `SetupResponse` DTO.

### 2. `POST /api/logs`

- Accepts a `LogEntry` DTO.
- Returns `204 No Content`.

### 3. `GET /api/display`

- Reads `ID` header.
- Returns a `DisplayResponse` DTO.

### 4. `POST /api/screens/{id}/image`

- Accepts raw image bytes.
- Stores to `/data/{id}.jpg` or `.png`.
- Returns `{ id, path }`.

### 5. `GET /screens/{id}.jpg`

- Returns the stored image.

---

## Type-Safe DTOs

All request/response bodies are defined under:

```plain
Models/
SetupModels.cs
LogModels.cs
DisplayModels.cs
ScreenInfo.cs
```

Code

These should be used for:

- Validation
- Serialization
- Future schema evolution

---

## Internal State

The server maintains:

- An in-memory dictionary of `ScreenInfo`
- Disk-backed image storage under `/data`

This design is intentionally simple for BYOD deployments.

---

## Extension Guidelines

When extending:

- Add new DTOs under `Models/`
- Add new endpoints in `Program.cs`
- Maintain compatibility with the TRMNL firmware spec
- Avoid breaking existing JSON shapes
- Keep `/data` as the canonical image store

---

## Notes for Agents

- Do not remove or rename existing endpoints.
- Do not change JSON field names without versioning.
- When adding new features, prefer additive changes.
- Always reference the TRMNL BYOS API spec for firmware behavior.
