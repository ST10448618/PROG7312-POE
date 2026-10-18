# Smart-X — IoT Mesh Telemetry Gateway

**PROG7312 | Programming 3B: Advanced Application Development**

Student: Perez Seth Roy   
Student Number: ST10448618

Part 2 Demonstration: [Insert YouTube Link]

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Features](#2-features)
3. [System Architecture](#3-system-architecture)
4. [Technology Stack](#4-technology-stack)
5. [Getting Started](#5-getting-started)
6. [Using the Application](#6-using-the-application)
7. [Part 2 Testing Guide](#7-part-2-testing-guide)
8. [Part 1 Verification](#8-part-1-verification)
9. [API Reference](#9-api-reference)
10. [Real-Time Communication](#10-real-time-communication)
11. [Data Structures and Implementation](#11-data-structures-and-implementation)
12. [Project Structure](#12-project-structure)

---

## 1. Project Overview

Smart-X is an IoT mesh telemetry gateway and monitoring application designed to simulate the management of distributed sensor networks, such as those used in hydroponic farming, smart-grid installations and utility monitoring.

The application receives telemetry from simulated devices, validates and stores incoming readings, detects anomalies, manages device states and provides operators with a real-time monitoring dashboard.

The project demonstrates the practical application of object-oriented programming, generic types, custom collections, algorithmic processing, database persistence, REST APIs and real-time communication.

### Project scope

| Component | Description |
|---|---|
| Part 1 | Telemetry ingestion, validation, storage, anomaly detection and supporting programming concepts. |
| Part 2 | Real-time processing, device management, command queues, fault tracking, command history and predictive action suggestions. |
| Final PoE | Planned network topology, graph traversal and mesh-routing functionality. |

The system uses simulated devices and telemetry rather than physical IoT hardware.

## 2. Features

### 2.1 Part 1 — Telemetry Ingestion and Validation

- **Sensor management:** Register, edit and delete devices using their MAC addresses, deployment locations and categories.
- **Generic telemetry processing:** Use `TelemetryPacket<T>` to support different data types, including floating-point, integer and Boolean readings.
- **Operator overloading:** The `PowerReading` class overloads arithmetic, comparison and equality operators.
- **Batch processing:** Store telemetry batches using jagged arrays and convert them into collections for processing.
- **Recursive deployment validation:** Validate nested facility paths of arbitrary depth.
- **Encrypted file storage:** Encrypt uploaded configuration files, photographs and logs using AES encryption.
- **Custom ring buffer:** Maintain a fixed-capacity rolling window for anomaly detection.
- **Anomaly detection:** Classify readings as Normal, Warning, Critical or Disconnected using a rolling statistical baseline.
- **Anomaly logs:** View, filter and acknowledge recorded anomalies.
- **Webhook integrations:** Configure notification endpoints and test connectivity.
- **System health monitoring:** Inspect application health, database connectivity and device statistics.

### 2.2 Part 2 — Real-Time Processing and Device Management

- **Command Stream:** Monitor processing queues, priority alerts, commands, faults, history and device registry information.
- **FIFO queue:** Process standard telemetry using `Queue<T>`.
- **Priority queue:** Prioritise Warning and Critical telemetry using `PriorityQueue<TElement, TPriority>`.
- **Command undo:** Store manual commands in `Stack<T>` and support last-in, first-out undo operations.
- **Live device registry:** Maintain device state for efficient lookup and updates by MAC address.
- **Historical timeline:** Organise telemetry by timestamp using `SortedDictionary<TKey, TValue>`.
- **Fault tracking:** Use `HashSet<T>` to prevent duplicate entries for disconnected devices.
- **Predictive Action Engine:** Learn associations between alerts and operator actions to generate ranked suggestions.
- **Automated Insights:** Identify disconnected devices, abnormal readings, inactive sensors and queue backlogs.
- **Search and filtering:** Filter the device registry by text, category and operational state.
- **Shared client state:** Preserve relevant interface state and maintain a shared SignalR connection when navigating between pages.
- **Live Home dashboard:** Display sensor statistics and a time-series chart with selectable sensor lines.
- **Telemetry Overview:** Present active alerts, device statistics and a sensor anomaly heat map.

## 3. System Architecture

Smart-X uses a client-server architecture consisting of an ASP.NET Core API, a Blazor WebAssembly client and a PostgreSQL database.

### Main components

| Component | Responsibility |
|---|---|
| SmartX.Api | Exposes REST endpoints, processes telemetry, manages device state and coordinates services. |
| SmartX.Client | Provides the user interface, dashboard, device management pages and real-time monitoring. |
| PostgreSQL | Stores durable records, including devices, telemetry logs, anomaly logs, integrations and file metadata. |
| SignalR | Pushes telemetry and state changes to connected clients. |
| Background services | Simulate telemetry and process queued events. |
| Anomaly engine | Scores readings against a rolling statistical baseline. |
| Predictive engine | Learns action patterns and produces suggestions and automated insights. |

### Data flow

1. Simulated devices generate telemetry.
2. The API receives and validates incoming readings.
3. Valid readings are stored and evaluated by the anomaly detection engine.
4. Readings are directed into the appropriate processing queue.
5. Background services process queued items and update device state.
6. The API publishes relevant changes through SignalR.
7. The Blazor client updates its dashboard, alerts, charts and device registry.

The API and client are separate deployables. Each maintains its own models, allowing the two applications to be built and deployed independently.

PostgreSQL stores durable application records, while several Part 2 processing structures operate in memory and are rebuilt when the API restarts.

## 4. Technology Stack

| Layer | Technology |
|---|---|
| Backend | C#, .NET 10 and ASP.NET Core Minimal APIs |
| Frontend | Blazor WebAssembly and Razor components |
| Database | PostgreSQL 16 |
| Data access | Entity Framework Core with Npgsql |
| Real-time communication | SignalR |
| API documentation | Swagger / OpenAPI |
| Logging | Serilog |
| File security | AES encryption |
| Deployment | Docker, Docker Compose and Nginx |
| Visualisation | SVG-based telemetry chart and CSS dashboard components |

## 5. Getting Started

### Prerequisites

Install the following:

- Docker Desktop, including Docker Compose.
- Git.

A local .NET SDK, Node.js installation or PostgreSQL installation is not required when running the application through Docker Compose.

### Clone and run

Replace the repository URL with the actual repository URL before using these commands.

```bash
git clone <repository-url>
cd SmartX
docker compose up --build
```

Wait for the database, API and client containers to start.

### Application URLs

| Service | URL |
|---|---|
| Client application | http://localhost:8081 |
| API documentation | http://localhost:5000/swagger |
| API health check | http://localhost:5000/health |
| PostgreSQL | localhost:5432 |

The development database configuration is:

| Setting | Value |
|---|---|
| Database | `smartx` |
| Username | `smartx` |
| Password | `smartx_dev` |

These credentials are for local development only.

### Initialisation

On the first successful startup:

1. PostgreSQL starts and passes its health check.
2. The API applies Entity Framework Core migrations.
3. The application seeds demonstration sensors if the database is empty.
4. Background services begin generating simulated telemetry and processing queued items.
5. The pattern history seeder loads demonstration operator behaviour for the predictive engine.

### Demonstration sensors

| MAC address | Category | Deployment location |
|---|---|---|
| `AA:BB:CC:00:01:01` | Environmental | Facility A/Zone 1/Sub-Zone B/Rack 1 |
| `AA:BB:CC:00:01:02` | Power Consumption | Facility A/Zone 1/Sub-Zone C |
| `AA:BB:CC:00:01:03` | Actuator | Facility A/Zone 1/Sub-Zone B/Rack 1 |

### Stopping and resetting

Stop the containers while preserving the database:

```bash
docker compose down
```

Stop the containers and delete the database volume:

```bash
docker compose down -v
```

**Important:** The second command deletes persisted database data. Use it only when a clean reset is required, such as when testing database migrations against a fresh database.

## 6. Using the Application

| Page | Route | Functionality |
|---|---|---|
| Home | `/` | Live statistics, uptime, telemetry counts and a sensor readings chart. Sensor chips toggle individual chart lines. |
| Telemetry Overview | `/dashboard` | Device statistics, active critical alerts and an interactive anomaly heat map. |
| Command Stream | `/command-stream` | Queue depths, command issuing, undo history, device faults, historical timeline, searchable device registry and predictive suggestions. |
| Anomaly Logs | `/anomalies` | Search and filter scored readings by sensor and severity; acknowledge anomalies. |
| Data Ingestion | `/ingestion` | Manage devices, upload encrypted attachments, trigger test telemetry, calculate power aggregates and validate deployment paths. |
| Integrations | `/integrations` | Register webhook endpoints, test connectivity and manage integrations. |
| System Health | `/health` | Inspect system uptime, database connectivity, device counts and incident history. |
| Topology | Not implemented | Reserved for future mesh-routing functionality. |

### Home dashboard

The Home page provides a summary of the system's current state.

- The statistic cards refresh every 10 seconds.
- Sensor chips show or hide individual chart lines.
- Sensor colours remain consistent when other sensors are hidden.
- Focus 0–100 mode emphasises the normal operating range.
- Full range mode expands the chart to accommodate extreme readings.
- Warning and Critical markers highlight anomalous readings.
- Highest reading, lowest reading and total plotted readings are displayed beneath the chart.

### Telemetry Overview

The Overview page presents four statistic cards, an active alerts section and the anomaly heat map.

Select a heat-map cell or an alert's Inspect button to open the sensor inspector. The inspector provides:

- **Real-time:** Latest readings, anomaly state and recent history.
- **Network:** Device location, category, connection status and last-seen time.
- **Actions:** Contextual guidance based on the device's current state.

### Command Stream

The Command Stream page combines real-time processing and device management.

Use the device registry to inspect current values and states. Issue supported commands, undo recent commands, review the historical timeline and investigate active faults.

The predictive section provides ranked Suggested Actions and Automated Insights when the relevant conditions are met.

## 7. Part 2 Testing Guide

This section provides repeatable demonstrations of the primary Part 2 requirements.

Before testing, start the application and open the client and API documentation.

### 7.1 Normal telemetry

1. Navigate to Data Ingestion.
2. Select a device and trigger a Normal reading.
3. Open Command Stream.
4. Inspect the standard queue, registry and latest device value.

**Expected result:** The reading is processed as normal telemetry, and the relevant device and queue state is updated.

### 7.2 Critical and Warning telemetry

1. Open Data Ingestion.
2. Select a device.
3. Trigger a Spike or Drop.
4. Inspect the anomaly logs, queue counters and device state.
5. Return to the Home page to inspect the chart.

**Expected result:** The reading is scored, an anomaly is recorded when the detection threshold is met, and the appropriate processing queue receives the event.

The rolling baseline adapts to repeated readings. If a spike does not produce a Critical alert, try another device or allow the baseline to recover before repeating the test.

### 7.3 Priority queue and FIFO queue

1. Generate several abnormal readings.
2. Generate multiple normal readings.
3. Open Command Stream and monitor the queue counters.
4. Query `/api/commands/queues` to inspect the current queue depths.

**Expected result:** Warning and Critical events are assigned priority processing, while normal events use the standard FIFO queue. The background processor drains pending items.

Queue depths can change quickly because the background processor runs continuously. A short burst of events is more effective than sending one reading at a time.

### 7.4 Dictionary-based device registry

1. Trigger a reading with a distinctive value for a registered sensor.
2. Open the live device registry.
3. Inspect the sensor's latest value, status and last-seen information.

**Expected result:** The registry reflects the latest device state through a lookup keyed by MAC address.

### 7.5 SortedDictionary historical timeline

1. Open Command Stream.
2. Navigate to Historical Timeline.
3. Refresh the timeline after generating several readings.
4. Compare the timestamps and ordering.

**Expected result:** Historical readings are presented in timestamp order, with the newest entries displayed first in the interface.

The in-memory timeline is configured to retain a bounded history of recent time buckets.

### 7.6 HashSet fault tracking

1. Select a sensor and trigger Disconnect.
2. Inspect Active Faults.
3. Trigger Disconnect again for the same sensor.
4. Send a new reading to the device.

**Expected result:** The device is represented once in the active fault set. A subsequent valid reading should clear the fault when the recovery logic runs.

### 7.7 Stack-based command undo

1. Open Command Stream.
2. Issue three different supported commands.
3. Review Command History.
4. Select Undo last command repeatedly.

**Expected result:** Undo operations follow last-in, first-out order, removing the most recent command from the in-memory command history according to the implementation.

Commands simulate device actions; they do not operate physical hardware.

### 7.8 Device registry search and filters

1. Open Command Stream.
2. Search for a known MAC address or part of a device identifier.
3. Apply the available category and operational-state filters.
4. Change the search query and repeat.

**Expected result:** The registry narrows to matching devices. Committed searches can contribute to the predictive engine's learned patterns.

### 7.9 Predictive Action Engine

The engine uses recent alerts and previously observed operator behaviour to produce suggestions. A matching pattern must meet the implementation's minimum observation threshold before it is suggested.

**Demonstration A — Seeded command pattern**

1. Generate a Spike on `AA:BB:CC:00:01:01`.
2. Open Suggested Actions.
3. Inspect the suggested action targeting `AA:BB:CC:00:01:03`.
4. Review its confidence and explanatory text.
5. Use Run if the suggestion is available.

**Demonstration B — Seeded search pattern**

1. Generate a Disconnect for `AA:BB:CC:00:01:02`.
2. Open Suggested Actions.
3. Look for a suggestion to filter Power Consumption devices by Faulted status.
4. Select Apply filter if available.

**Demonstration C — Learned operator behaviour**

1. Generate a Spike on `AA:BB:CC:00:01:03`.
2. Issue Shutdown Valve for `AA:BB:CC:00:01:01` on two separate occasions.
3. Generate the matching alert again if necessary.
4. Inspect the resulting suggestions.

**Expected result:** The engine can learn repeated associations between alerts and operator actions and rank qualifying suggestions. Exact results depend on the implementation's alert window, recorded history and current in-memory state.

### 7.10 Automated Insights

Generate or wait for one of the following conditions:

- A disconnected device.
- A reading outside the configured operating range.
- A device that has stopped reporting.
- A backlog in the priority queue.

Open Automated Insights and inspect the resulting explanations.

**Expected result:** The engine identifies applicable conditions and displays relevant operational guidance.

### 7.11 Home chart interactions

1. Open Home.
2. Generate normal and abnormal readings.
3. Click a sensor chip to hide its line.
4. Click it again to restore the line.
5. Switch between Focus 0–100 and Full range.
6. Hover over highlighted markers.
7. Navigate to another page and return to Home.

**Expected result:** Sensor lines can be toggled, chart modes change the visible range, markers expose reading details, and sensor visibility preferences remain in the shared client store during navigation.

### 7.12 Anomaly Logs search

1. Open Anomaly Logs.
2. Enter a sensor identifier in the search field.
3. Press Enter to apply the search.
4. Change the severity dropdown.
5. Confirm that the results update.

**Expected result:** The search and severity controls filter the displayed records using the page's filtering logic.

## 8. Part 1 Verification

The following checks can be used to verify the original ingestion and validation requirements alongside the Part 2 work.

| Requirement | Verification |
|---|---|
| Generics | Use Swagger to submit float, integer and Boolean telemetry through the appropriate endpoints. |
| Operator overloading | Use the Data Ingestion power aggregation calculator or the relevant power aggregation API endpoint. |
| Recursive validation | Validate a deeply nested facility path, then test a path that violates the deployment rules. |
| Encrypted uploads | Upload a file for a sensor, download it and compare the readable download with the encrypted stored file. |
| Ring buffer | Generate enough readings to exceed the configured rolling-window capacity and verify that anomaly scoring uses the recent window. |
| Anomaly detection | Generate normal and abnormal readings and inspect their recorded classifications. |
| Webhook integration | Configure an endpoint, test connectivity and verify the notification behaviour for critical alerts. |

For encrypted file verification, inspect the upload directory inside the API container using the actual container name and configured storage path. Avoid publishing encryption keys or sensitive file contents in the demonstration.

## 9. API Reference

The Swagger interface provides the definitive request and response schemas for the running version of the application.

Open: http://localhost:5000/swagger

The following endpoint groups describe the expected API surface. Confirm individual routes against the current implementation before relying on them in external scripts.

| Area | Operations |
|---|---|
| Sensors | Retrieve, register, update and delete devices; retrieve summary and registry information. |
| Files | Upload attachments, list stored files and download decrypted files. |
| Telemetry | Submit typed readings, disconnect devices, retrieve recent readings and access history. |
| Command Stream | Inspect queues, command history, timeline and faults; issue and undo commands. |
| Insights | Retrieve suggestions and insights; submit search activity. |
| Anomalies | Retrieve filtered anomaly records and acknowledge individual entries. |
| Integrations | Register, list, test and remove webhook integrations. |
| System | Retrieve health summaries, incidents and the API health status. |
| Demonstration utilities | Test power aggregation and deployment path validation. |

### Common endpoint examples

| Method | Example route | Purpose |
|---|---|---|
| GET | `/api/sensors/summary` | Retrieve sensor statistics. |
| GET | `/api/sensors/registry` | Retrieve the live device registry. |
| POST | `/api/telemetry/moisture` | Submit a moisture reading. |
| GET | `/api/commands/queues` | Inspect queue depths. |
| GET | `/api/commands/history` | Retrieve command history, if exposed by the current API. |
| GET | `/api/commands/timeline` | Retrieve the historical timeline, if exposed by the current API. |
| GET | `/api/commands/faults` | Inspect active faults. |
| POST | `/api/commands/issue` | Issue a device command. |
| POST | `/api/commands/undo` | Undo the most recent command. |
| GET | `/api/insights` | Retrieve predictive suggestions and automated insights. |
| GET | `/api/system/health-summary` | Retrieve system health statistics. |

## 10. Real-Time Communication

The application uses SignalR to distribute state changes to connected clients.

The hub is configured at:

`/hubs/telemetry`

The client maintains a shared telemetry store so that components can reuse received data and state while navigating between pages.

| Event | Purpose |
|---|---|
| `AnomalyUpdate` | Updates anomaly-related visualisations. |
| `TelemetryIngested` | Announces newly ingested telemetry. |
| `DeviceRegistryUpdated` | Refreshes live registry information. |
| `QueueDepthChanged` | Updates processing queue counters. |
| `FaultSetChanged` | Updates active fault information and navigation indicators. |
| `InsightsUpdated` | Refreshes suggestions and automated insights. |
| `CommandIssued` | Announces a newly issued command. |
| `CommandUndone` | Announces a command undo operation. |

Event names and payloads should be confirmed against the current hub and client subscription implementations.

## 11. Data Structures and Implementation

| Concept | Data structure or component | Responsibility |
|---|---|---|
| Standard processing | `Queue<T>` | FIFO processing for standard telemetry. |
| Priority processing | `PriorityQueue<TElement, TPriority>` | Processes higher-priority events before standard queue items. |
| Command undo | `Stack<T>` | Maintains last-in, first-out command history. |
| Device registry | `ConcurrentDictionary<TKey, TValue>` | Supports efficient, thread-safe device lookup and updates. |
| Historical timeline | `SortedDictionary<TKey, TValue>` | Maintains timestamp-ordered telemetry buckets. |
| Active faults | `HashSet<T>` | Prevents duplicate fault entries. |
| Predictive learning | Dictionaries and collections | Records alert-action associations and ranks qualifying suggestions. |
| Rolling anomaly window | `RingBuffer<T>` | Retains a bounded set of recent readings for statistical scoring. |
| Telemetry batches | Jagged arrays and `List<T>` | Stores and processes grouped telemetry. |
| Typed telemetry | `TelemetryPacket<T>` | Supports multiple telemetry value types through a generic model. |
| Power arithmetic | `PowerReading` | Implements overloaded arithmetic and comparison operators. |
| Client state | `TelemetryStore` | Shares live data, connection state and selected interface preferences. |

### Predictive Action Engine

The engine learns associations between alerts and operator actions.

1. Relevant alerts are recorded within a rolling time window.
2. Operator commands and committed searches are associated with the alerts active at the time.
3. Repeated associations contribute to the learned pattern history.
4. Candidate actions are ranked according to their observed confidence.
5. Suggestions meeting the configured minimum observation threshold are presented to the operator.

Automated Insights are separate rule-based evaluations of system conditions, such as disconnected devices, abnormal readings and processing backlogs.

These capabilities are intended to demonstrate an explainable, rule-driven predictive workflow rather than a trained machine-learning model.

## 12. Project Structure

```text
SmartX/
├── docker-compose.yml
├── README.md
├── SmartX.Api/
│   ├── Program.cs
│   ├── Domain/
│   │   ├── Collections/
│   │   ├── Entities/
│   │   ├── Models/
│   │   └── ValueObjects/
│   ├── Infrastructure/
│   │   ├── Data/
│   │   ├── FileStorage/
│   │   └── Seeding/
│   ├── Services/
│   ├── Endpoints/
│   ├── Hubs/
│   ├── Middleware/
│   ├── Dtos/
│   ├── Migrations/
│   └── Dockerfile
└── SmartX.Client/
    ├── Pages/
    ├── Components/
    ├── Layout/
    ├── Services/
    ├── Models/
    ├── wwwroot/
    │   └── css/
    ├── nginx.conf
    └── Dockerfile
```

The tree represents the main architectural areas. Some folder names may differ from the exact repository layout.

---

**Smart-X | PROG7312 — Programming 3B**

Student: Perez Seth Roy   
Student Number: ST10448618
