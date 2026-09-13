# Smart-X — IoT Mesh Ecosystem 

**Student Number:** ST10448618
**Module:** PROG7312 — Programming 3B
**YouTube Demo:** https://youtu.be/VZPxDienen0

## Overview

Smart-X is a simulated Internet of Things (IoT) gateway for a distributed
sensor network (hydroponic farms, smart grid installations, utility
trackers). Thousands of ESP32-style sensors publish multi-typed telemetry
(floats for moisture, integers for power draw, booleans for valve states)
to a central gateway, which validates, stores, and analyses that data for
anomalies in real time.

Part 1 covers: sensor registration and file/log attachment, generic
telemetry ingestion, a live anomaly-scoring engine visualised as a heat
map, and the supporting API/database/infrastructure layer.

## Architecture

- **Backend:** ASP.NET Core Minimal API (.NET 10), layered into
  Domain / Infrastructure / Services / Endpoints. EF Core + PostgreSQL
  for persistence.
- **Frontend:** Blazor WebAssembly — an entirely independent project from
  the API (no shared assembly), so either side can be redeployed without
  rebuilding the other.
- **Real-time:** SignalR pushes live anomaly scores and telemetry events
  to the client as they happen.
- **Containerisation:** Docker Compose orchestrates Postgres, the API,
  and the client together, with health checks gating startup order.

## Languages & technologies

| Layer | Technology |
|---|---|
| Backend language | C# (.NET 10) |
| Frontend language | C# (Razor components, Blazor WebAssembly) |
| Database | PostgreSQL 16 |
| ORM | Entity Framework Core |
| Real-time transport | SignalR |
| Styling | Plain CSS (no build-step dependency) |
| Containerisation | Docker & Docker Compose |
| API documentation | Swagger / OpenAPI |
| Logging | Serilog |

## Prerequisites

You need the following installed before running this project:

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (includes Docker Compose)
- Git

You do **not** need .NET, Node, or PostgreSQL installed locally — everything
runs inside containers. If you want to run the API or client outside Docker
for development, you'll additionally need:
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A local PostgreSQL 16 instance (or the `db` container from Compose)

## Setup — running the whole app

1. Clone the repository:
```bash
   git clone <repository-url>
   cd SmartX
```
2. Start everything:
```bash
   docker compose up --build
```
   This builds and starts three containers: `db` (Postgres), `api`
   (ASP.NET Core), and `client` (Blazor WASM served via nginx). The API
   automatically applies EF Core migrations and seeds three demo sensors
   on first boot.
3. Once all three containers report healthy, open:
   - **Client app:** http://localhost:8081
   - **API Swagger docs:** http://localhost:5000/swagger
   - **API health check:** http://localhost:5000/health

## Resetting the database

If you regenerate EF Core migrations or want a completely clean state:
```bash
docker compose down -v
docker compose up --build
```
The `-v` flag removes the Postgres data volume — without it, an old
schema can conflict with newly generated migrations.

## How to interact with the app

| Page | What it does |
|---|---|
| **Home** | Live KPI dashboard — sensor counts, alerts, uptime, recent activity |
| **Overview** | Real-time anomaly heat map; click any cell for sensor detail |
| **Anomaly Logs** | Searchable/filterable history of every scored reading |
| **Data Ingestion** | Register sensors, attach encrypted files, trigger telemetry, power-aggregation calculator |
| **Integration** | Register and test real webhook endpoints for critical-alert notifications |
| **System Health** | Live uptime, DB connectivity, and incident history |

Telemetry also flows automatically in the background — an
`AutoTelemetrySimulator` service continuously feeds realistic readings
(mostly Normal, with occasional Warning/Critical/Disconnected events) to
every registered sensor every few seconds, so the system demonstrates
live operation without requiring manual interaction. 

## Key design decisions

- **`RingBuffer<T>`** — a hand-built fixed-capacity circular buffer
  backing the anomaly engine's rolling score window (custom collection,
  not a wrapped `List<T>`).
- **`TelemetryPacket<T>`** — generic wrapper letting float/int/bool
  telemetry flow through one ingestion pipeline with no boxing/unboxing.
- **`PowerReading`** — overloads `+ - > < >= <= == !=`, plus
  `IComparable`/`IEquatable`, for direct aggregation of meter readings.
- **Encrypted file storage** — sensor log/config uploads are AES-encrypted
  at rest, with size and extension validation.
- **Recursive JSON deployment parser** — validates arbitrarily nested
  facility/zone/sub-zone hierarchies, not a fixed-depth structure.
- **Anomaly Heat Map** — implements the "Semantic Visual Encoding"
  strategy from the Part 1 research: a rolling z-score mapped to
  Normal/Warning/Critical/Disconnected, pushed live via SignalR.

## Coming Soon 

- Command Stream & History (queues, priority alerts, undo) — Part 2
- Network Topology & Mesh Routing (trees, graphs, MST) — final PoE
