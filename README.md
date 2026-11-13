# Smart-X — IoT Mesh Telemetry Gateway

**PROG7312 | Programming 3B: Advanced Application Development**

Student: Perez Seth Roy  
Student Number: ST10448618

<a href="https://youtu.be/u8mSVr7E8Mk"><img src="https://cdn.simpleicons.org/youtube/FF0000" height="16" alt="YouTube" /></a> [Final POE Demo](https://youtu.be/u8mSVr7E8Mk)

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Features](#2-features)
3. [System Architecture](#3-system-architecture)
4. [Technology Stack](#4-technology-stack)
5. [Getting Started](#5-getting-started)
6. [Using the Application](#6-using-the-application)
7. [API Reference](#7-api-reference)
8. [Real-Time Communication](#8-real-time-communication)
9. [Data Structures and Implementation](#9-data-structures-and-implementation)
10. [Part 3 Implementation Report](#10-part-3-implementation-report)
11. [Performance and Scaling](#11-performance-and-scaling)
12. [Troubleshooting](#12-troubleshooting)
13. [Project Structure](#13-project-structure)

---

## 1. Project Overview

Smart-X is an IoT mesh telemetry gateway and monitoring application. It simulates the management of distributed sensor networks, such as hydroponic farming, smart-grid installations and utility monitoring.

The application receives telemetry from simulated devices, validates and stores readings, detects anomalies, manages device state and gives operators a real-time dashboard. Part 3 adds the network itself. The ESP32 mesh is modelled as a weighted graph, and the system finds the lowest-power routing tree. It reroutes around failed nodes and answers historical queries quickly using a balanced tree.

### Project scope

| Component | Description |
|---|---|
| Part 1 | Telemetry ingestion, validation, storage, anomaly detection and supporting programming concepts. |
| Part 2 | Real-time processing, device management, command queues, fault tracking, command history and predictive suggestions. |
| Part 3 | Self-balancing AVL tree for historical telemetry queries, mesh graph, BFS, DFS, Prim's minimum spanning tree, what-if outage analysis, benchmarks, automated tests and CI. |

The system uses simulated devices and telemetry rather than physical hardware.

## 2. Features

### 2.1 Part 1 — Telemetry Ingestion and Validation

- **Sensor management:** Register, edit and delete devices using MAC addresses, locations and categories.
- **Generic telemetry processing:** `TelemetryPacket<T>` supports floating-point, integer and Boolean readings.
- **Operator overloading:** `PowerReading` overloads arithmetic, comparison and equality operators.
- **Batch processing:** Telemetry batches are stored in jagged arrays and converted into collections.
- **Recursive deployment validation:** Nested facility paths of any depth are validated.
- **Encrypted file storage:** Uploaded files are protected with AES encryption.
- **Custom ring buffer:** A fixed-capacity rolling window feeds anomaly detection.
- **Anomaly detection:** Readings are classified as Normal, Warning, Critical or Disconnected.
- **Anomaly logs:** View, filter and acknowledge recorded anomalies.
- **Webhook integrations:** Configure notification endpoints and test connectivity.
- **System health monitoring:** Inspect application health, database connectivity and device statistics.

### 2.2 Part 2 — Real-Time Processing and Device Management

- **Command Stream:** Queues, priority alerts, commands, faults, history and device registry.
- **FIFO queue** (`Queue<T>`) for standard telemetry.
- **Priority queue** (`PriorityQueue<TElement, TPriority>`) for Warning and Critical telemetry.
- **Command undo** using `Stack<T>`, restoring the device's previous actuator state.
- **Live device registry** with efficient lookup by MAC address.
- **Historical timeline** using `SortedDictionary<TKey, TValue>`.
- **Fault tracking** using `HashSet<T>`.
- **Predictive Action Engine** with recency decay, repeat-offender detection and trend forecasting.
- **Automated Insights** for disconnected devices, abnormal readings, inactive sensors and backlogs.
- **Shared client state:** one SignalR connection and one store shared across pages.

### 2.3 Part 3 — Topology, Graphs and Historical Queries

- **Network Topology page** with an interactive mesh map: 12 sensors and 1 gateway.
- **Mesh graph** as a weighted, undirected adjacency list.
- **Breadth-first search** for the fewest-hops route from every node to the gateway.
- **Depth-first search** to find subnets, meaning islands cut off from the gateway.
- **Prim's algorithm** with a custom min-heap, producing the minimum spanning tree (the most power-efficient routing tree).
- **Route tracing** comparing the MST route against the fewest-hops route for any node.
- **What-if mode:** simulate outages without touching devices or the database, and see which nodes reroute and which are stranded.
- **Live mesh health:** a background monitor pushes changes over SignalR.
- **AVL-tree telemetry history:** time-range queries per sensor in O(log n + k).
- **Live benchmark:** AVL tree against a linear scan, with an unbalanced BST height for comparison.
- **136 automated unit tests** and a GitHub Actions pipeline with a Docker smoke test.

## 3. System Architecture

| Component | Responsibility |
|---|---|
| SmartX.Api | REST endpoints, telemetry processing, device state, mesh analysis and history. |
| SmartX.Client | Blazor WebAssembly user interface. |
| PostgreSQL | Durable records: devices, telemetry, anomalies, integrations and file metadata. |
| SignalR | Pushes telemetry, queue, fault and mesh changes to clients. |
| Background services | Simulation, queue processing, mesh health monitoring and history warm-up. |
| SmartX.Tests | xUnit test project for collections, graph algorithms and services. |

### Data flow

1. Simulated devices generate telemetry.
2. The API validates readings, stores them and scores them for anomalies.
3. Readings enter the FIFO or priority queue and are processed by an event-driven background service.
4. Each reading is also added to that sensor's AVL tree for historical queries.
5. `MeshHealthMonitor` checks node health every 2 seconds and pushes `MeshHealthChanged` when it changes.
6. `MeshAnalysisService` rebuilds the BFS, DFS and MST results from the current mesh state.
7. The Blazor client updates the dashboard, topology map and history panel.

The API and client keep separate models so they can be built and deployed independently. Part 2 and Part 3 structures are held in memory and rebuilt on API restart (history takes about 12–14 s to warm up).

## 4. Technology Stack

| Layer | Technology |
|---|---|
| Backend | C#, .NET 10, ASP.NET Core Minimal APIs |
| Frontend | Blazor WebAssembly and Razor components |
| Database | PostgreSQL 16 |
| Data access | Entity Framework Core with Npgsql |
| Real-time | SignalR |
| API documentation | Swagger / OpenAPI |
| Logging | Serilog |
| File security | AES encryption |
| Testing | xUnit 2.9.3 |
| CI | GitHub Actions, Dependabot |
| Deployment | Docker, Docker Compose, Nginx |
| Visualisation | SVG chart and mesh map, plain CSS design system |

## 5. Getting Started

### Prerequisites

- Docker Desktop, including Docker Compose.
- Git.

A local .NET SDK is **not** needed to run the application. It is only needed to run the unit tests outside Docker (.NET 10 SDK).

### Clone and run

```bash
git clone https://github.com/ST10448618/PROG7312---POE.git
cd PROG7312---POE
docker compose up --build
```

Wait for the database, API and client containers to start. After the API starts, give it roughly 15 seconds to warm up the history trees before using the Topology history panel.

### Application URLs

| Service | URL |
|---|---|
| Client application | http://localhost:8081 |
| Topology page | http://localhost:8081/topology |
| API documentation | http://localhost:5000/swagger |
| API health check | http://localhost:5000/health |
| PostgreSQL | localhost:5432 |

Development database: database `smartx`, user `smartx`, password `smartx_dev`. These are for local development only.

### Initialisation

On the first successful start:

1. PostgreSQL starts and passes its health check.
2. The API applies EF Core migrations.
3. `MockTelemetrySeeder` seeds the demonstration sensors.
4. `MeshTopologySeeder` creates the 12 mesh sensors and the gateway (this runs after the first seeder).
5. `TelemetryHistoryBuilder` seeds **96,768 historical readings** (12 sensors × 8,064 readings, 7 days at 75-second intervals) and loads them into per-sensor AVL trees.
6. Background services start generating live telemetry and processing queues.
7. `PatternHistorySeeder` loads demonstration operator behaviour for the predictive engine.

### Stopping and resetting

```bash
docker compose down            # keep the database
docker compose down -v         # delete the database volume
```

**Important:** `down -v` deletes persisted data. For a fully clean rebuild use:

```bash
docker compose down -v && docker compose up --build
```

## 6. Using the Application

| Page | Route | Functionality |
|---|---|---|
| Home | `/` | Live statistics and a sensor readings chart. |
| Telemetry Overview | `/dashboard` | Device statistics, alerts and an anomaly heat map. |
| Command Stream | `/command-stream` | Queues, commands, undo, faults, timeline, registry and predictive suggestions. |
| **Topology** | `/topology` | Mesh map, MST overlay, route tracing, what-if outages, per-sensor history and the performance benchmark. |
| Anomaly Logs | `/anomalies` | Search and filter scored readings, and acknowledge anomalies. |
| Data Ingestion | `/ingestion` | Manage devices, upload encrypted files and trigger test telemetry. |
| Integrations | `/integrations` | Register and test webhooks. |
| System Health | `/health` | Uptime, database connectivity and incident history. |

### Using the Topology page (Part 3)

1. Open **Topology**. The four cards show reachable sensors, nodes down, MST total cost and the power saved against using every link.
2. Click any node on the map. The side panel shows its category, location, last value and link count. The route to the gateway is traced, and the MST route is compared with the fewest-hops route.
3. Toggle **Show MST** to highlight the minimum spanning tree edges.
4. Turn on **What-if mode**, select a node and choose **Simulate outage**. The map shows which nodes reroute and which become stranded. Use **Reset scenario** to clear it. This never changes devices or the database.
5. Pick a sensor and a time window in the history panel to run an AVL range query.
6. Press **Run benchmark** to compare the AVL tree with a linear scan.
7. Use the **Command Stream** page to disconnect a real node. The topology updates itself within about 2 seconds.

**Reference results for the seeded mesh (13 nodes, 23 links):**

| Scenario | Result |
|---|---|
| Healthy mesh | MST has 12 edges, total cost **29.6**. |
| Node 05 down | MST cost **29.3**, 11 edges, 11 nodes reroute. Node 03's route changes from `03 → 05 → GW` (cost 4.4) to `03 → GW` (cost 3.0). |
| Nodes 03, 05 and 07 down | 9 nodes are stranded as one island with no route to the gateway. |
| Fewest hops (BFS) | 03, 05 and 07 are 1 hop away. 01, 04, 06 and 08 are 2. 02, 09 and 0A are 3. 0B and 0C are 4. |
| Farthest MST route | Node 0B to the gateway: 6 hops, cost 14.0. |

## 7. API Reference

Swagger is the definitive reference: http://localhost:5000/swagger

| Area | Operations |
|---|---|
| Sensors | Retrieve, register, update and delete devices; summary and registry. |
| Files | Upload, list and download decrypted files. |
| Telemetry | Submit typed readings, disconnect devices, retrieve recent readings. |
| Command Stream | Queues, command history, timeline and faults; issue and undo commands. |
| Insights | Predictive suggestions and automated insights. |
| Anomalies | Filtered anomaly records and acknowledgement. |
| Integrations | Register, list, test and remove webhooks. |
| System | Health summaries and incidents. |
| **Mesh (Part 3)** | Topology, whole-mesh analysis and route tracing, each with an optional what-if. |
| **History (Part 3)** | Range queries, summaries and the benchmark. |

### Part 3 endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/mesh/topology` | Nodes, links and current status. |
| GET | `/api/mesh/analysis?assumeDown=<mac>&assumeDown=<mac>` | BFS hops, DFS subnets, MST, savings, reroutes and stranded nodes. `assumeDown` is optional and simulated. |
| GET | `/api/mesh/route/{mac}?assumeDown=` | MST route against fewest-hops route for one node. |
| GET | `/api/history/{sensorId}?from=&to=&skip=&take=&order=` | AVL range query, ascending or descending, with paging. |
| GET | `/api/history/{sensorId}/summary` | Count, min, max, mean and time span for one sensor. |
| GET | `/api/history/benchmark?sizes=1000,10000&queries=` | AVL tree against linear scan at the given sizes. |

### Other common examples

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/sensors/summary` | Sensor statistics. |
| GET | `/api/sensors/registry` | Live device registry. |
| GET | `/api/commands/queues` | Queue depths. |
| POST | `/api/commands/issue` | Issue a device command. |
| POST | `/api/commands/undo` | Undo the most recent command. |
| GET | `/api/insights` | Suggestions and insights. |

## 8. Real-Time Communication

The hub is at `/hubs/telemetry`. The client opens **one** shared connection, owned by `TelemetryStore`, and reuses it across pages.

| Event | Purpose |
|---|---|
| `AnomalyUpdate` | Updates anomaly visualisations. |
| `TelemetryIngested` | Announces newly ingested telemetry. |
| `DeviceRegistryUpdated` | Refreshes live registry information. |
| `QueueDepthChanged` | Updates queue counters. |
| `FaultSetChanged` | Updates active faults. |
| `InsightsUpdated` | Refreshes suggestions and insights. |
| `CommandIssued` / `CommandUndone` | Announce command activity. |
| `ItemProcessed` | A queued item has been processed. |
| **`MeshHealthChanged`** | The mesh's set of failed nodes changed, so the Topology page re-analyses. |

## 9. Data Structures and Implementation

| Concept | Structure or component | Responsibility |
|---|---|---|
| Standard processing | `Queue<T>` | FIFO processing for standard telemetry. |
| Priority processing | `PriorityQueue<T, (Rank, Sequence)>` | Priority events first, in arrival order within a rank. |
| Command undo | `Stack<T>` | LIFO history with actuator-state snapshots. |
| Device registry | `ConcurrentDictionary` | Thread-safe lookup by MAC address. |
| Historical timeline | `SortedDictionary` | Timestamp-ordered buckets. |
| Active faults | `HashSet<T>` | No duplicate fault entries. |
| Rolling anomaly window | `RingBuffer<T>` | Bounded window for z-score scoring. |
| **Historical queries** | **`AvlTree<TKey,TValue>`** | **Self-balancing tree, one per sensor, keyed by timestamp.** |
| **Priority for Prim** | **`MinHeap<T>`** | **Array-backed binary heap with a custom comparer.** |
| **Mesh network** | **`MeshGraph`** | **Undirected weighted adjacency list.** |
| **Routing algorithms** | **`MeshAlgorithms`** | **BFS, DFS, Prim, route tracing and reroute analysis.** |

### Predictive Action Engine

1. Alerts are recorded in a rolling window.
2. Operator commands and searches are associated with the alerts active at the time.
3. Each observation is weighted by **recency decay** (half-life 4 hours).
4. A **repeat-offender** insight fires when a device alarms 3 or more times in 60 minutes, and escalates at 6.
5. A **trend forecast** (linear regression, at least 6 points and R² ≥ 0.6) projects the reading 30 minutes ahead.
6. A suggestion is shown only when its pattern has at least 2 observations.

This is an explainable, rule-driven engine, not a trained machine-learning model. Learned patterns are held in memory and are not persisted to PostgreSQL.

## 10. Part 3 Implementation Report

Source files are under `SmartX.Api/Domain/Collections/` and `SmartX.Api/Services/`.

### 10.1 AVL tree — `AvlTree.cs`

**Why it exists.** Every sensor has about 8,064 historical readings, and operators ask "what happened between 14:00 and 15:00?". A scan checks every reading. A plain binary search tree would be fast, but timestamps arrive in increasing order, and inserting sorted keys into an unbalanced BST produces a linked list with height n. An AVL tree rotates after every insert so its height stays about log₂ n.

**Rebalancing:**

```csharp
private static Node Rebalance(Node node)
{
    UpdateHeight(node);
    var balance = HeightOf(node.Left) - HeightOf(node.Right);

    if (balance > 1) // left-heavy
    {
        if (HeightOf(node.Left!.Left) < HeightOf(node.Left.Right))
            node.Left = RotateLeft(node.Left);   // left-right case
        return RotateRight(node);
    }
    if (balance < -1) // right-heavy
    {
        if (HeightOf(node.Right!.Right) < HeightOf(node.Right.Left))
            node.Right = RotateRight(node.Right); // right-left case
        return RotateLeft(node);
    }
    return node;
}
```

**Range query.** `Range(from, to)` walks down once to find the first key ≥ `from`, pushing each node onto a stack. It then yields entries in order until a key exceeds `to`. It uses an explicit `Stack<Node>` instead of recursion, so no call-stack depth limit applies and iteration is lazy (`yield return`), which suits paging with `skip` and `take`:

```csharp
var pending = new Stack<Node>();
for (var n = _root; n is not null;)
{
    if (n.Key.CompareTo(from) >= 0) { pending.Push(n); n = n.Left; }
    else n = n.Right;
}
while (pending.Count > 0)
{
    var node = pending.Pop();
    if (node.Key.CompareTo(to) > 0) yield break;   // everything after is later still
    yield return new(node.Key, node.Value);
    for (var c = node.Right; c is not null; c = c.Left) pending.Push(c);
}
```

**Other design choices**

- Equal keys go right, so two readings with the same timestamp stay in insertion order.
- `RangeDescending` is the mirror image, used for "newest first" pages.
- `TryRemoveMin` lets a history cap drop the oldest reading in O(log n).
- `IsValid(out problem)` checks the real height of every node, the balance factor, the stored heights and the ordering of the whole in-order sequence. The tests call it after thousands of inserts.
- `MinimumPossibleHeight` (⌈log₂(n+1)⌉) is the theoretical floor, and the benchmark shows it next to the real height.

| Operation | Time |
|---|---|
| Insert | O(log n) |
| Min / max | O(log n) |
| Remove min | O(log n) |
| Range query returning k results | O(log n + k) |
| Full in-order traversal | O(n) |
| Space | O(n) |

### 10.2 Min-heap — `MinHeap.cs`

A binary heap in a growable array (children of `i` at `2i+1` and `2i+2`), with a pluggable `IComparer<T>`. Sifting is **hole-based**: instead of swapping at each level, parents or children are shifted into the hole and the moving item is written once at the end.

```csharp
private void SiftUp(int index)
{
    var item = _items[index];
    while (index > 0)
    {
        var parent = (index - 1) / 2;
        if (_comparer.Compare(item, _items[parent]) >= 0) break;
        _items[index] = _items[parent];   // shift the parent down into the hole
        index = parent;
    }
    _items[index] = item;                 // single write
}
```

| Operation | Time |
|---|---|
| Push | O(log n) |
| Pop | O(log n) |
| Peek | O(1) |
| Growth | Doubles capacity, amortised O(1) |

### 10.3 Mesh graph — `MeshGraph.cs`

An undirected weighted graph stored as an **adjacency list** (`Dictionary<string, List<MeshNeighbor>>`), with case-insensitive ids. It rejects bad input when the graph is built: duplicate nodes, a second gateway, self-links, non-positive or NaN costs and duplicate links. The mesh is sparse (13 nodes, 23 links), so an adjacency list uses O(V + E) space instead of O(V²) for a matrix and iterates a node's neighbours directly.

### 10.4 Graph algorithms — `MeshAlgorithms.cs`

All algorithms take an `isDown` predicate so the same code serves the live mesh and what-if scenarios. The gateway is never treated as down.

**Breadth-first search, O(V + E).** It finds the fewest-hops distance from the gateway and keeps a parent pointer for each node, which `TraceRoute` follows back to build a path. Fewest hops means fewest radio transmissions, which suits latency.

**Depth-first search, O(V + E).** `FindSubnets` uses an explicit `Stack<string>`, not recursion. It groups the surviving nodes into connected components. A group that does not contain the gateway is an island, and every node in it is stranded.

**Prim's algorithm, O(E log V).** It grows a minimum spanning tree from the gateway. A `MinHeap<Frontier>` holds candidate edges ordered by cost. Instead of a decrease-key operation, the algorithm uses **lazy deletion**: stale heap entries are skipped when popped because their destination is already in the tree. Ties are broken by node ids so the same graph always yields the same tree.

```csharp
heap.Push(new Frontier(link.Cost, current, neighbour));
// ...
while (heap.TryPop(out var edge))
{
    if (reached.Contains(edge.To)) continue;   // lazy deletion
    reached.Add(edge.To);
    parent[edge.To] = edge.From;
    edges.Add(new MeshEdge(edge.From, edge.To, edge.Cost));
    total += edge.Cost;
    // push this node's links to nodes not yet reached
}
```

**Reroute analysis, O(V + E).** `RouteChanges` compares the healthy tree's parents with the current ones and reports each node's old path, new path, next hop, cost change and whether it is now stranded.

| Algorithm | Time | Space |
|---|---|---|
| BFS | O(V + E) | O(V) |
| DFS subnets | O(V + E) | O(V) |
| Prim (binary heap) | O(E log V) | O(V + E) |
| Route trace | O(path length) | O(path length) |

### 10.5 Services

| Service | Role |
|---|---|
| `MeshService` | Builds the live snapshot. A node is down when the fault tracker or registry says so. |
| `MeshAnalysisService` | `Analyze(assumeDown)` returns reachable, stranded, subnets, MST, savings and reroutes. `Route(id, assumeDown)` compares the MST route with the fewest-hops route. |
| `MeshHealthMonitor` | Checks every 2 seconds. It pushes `MeshHealthChanged` only when the set of failed nodes changes. |
| `TelemetryHistoryService` | One `AvlTree<DateTime, Reading>` per sensor, guarded by a lock. Supports query and summarise, with a cap of 100,000 points per sensor. |
| `HistoryBenchmark` | Times the AVL tree against a linear scan and reports tree heights. |

## 11. Performance and Scaling

The benchmark runs inside the API on identical data and identical time windows, and it checks that both methods return the same result.

```
GET http://localhost:5000/api/history/benchmark?sizes=1000,10000,50000
```

### Why the trees behave this way

| Readings (n) | Linear scan visits | AVL height (at most about 1.44 log₂ n) | Minimum possible |
|---|---|---|---|
| 1,000 | 1,000 | ≈ 10–12 | 10 |
| 10,000 | 10,000 | ≈ 14–16 | 14 |
| 100,000 | 100,000 | ≈ 17–20 | 17 |

A plain BST receiving time-ordered inserts has height **n** (it degenerates to a list). The benchmark reports this height for sizes up to 5,000 so you can see the contrast.

### Example measured results

> **Replace this table with your own run.** Click *Run benchmark* on the Topology page and copy the numbers.

| Readings | Speed-up (linear ÷ AVL) | Same result |
|---|---|---|
| 1,000 | about 3.9× | ✔ |
| 10,000 | about 4.1× | ✔ |

### Reading the numbers

- A linear scan is O(n). An AVL range query is O(log n + k), where k is the number of results.
- For a fixed window size, **the gap widens as n grows**, because the scan's cost grows with n while the tree's grows with log n and k.
- With very wide windows (k close to n) both methods have to touch most of the data, so the speed-up shrinks. The tree wins most clearly on narrow windows, which are the common operator query.
- The measured speed-up at these sizes is modest because 10,000 points is small. The logarithmic advantage grows with the dataset.
- Memory is O(n) for both. The tree adds two child references and a height field per entry.

### Mesh scaling

The mesh analysis is cheap to run: BFS and DFS are O(V + E) and Prim is O(E log V). With 13 nodes and 23 links it is effectively instant, which is why the monitor can re-run it every 2 seconds on a change. Because the graph is stored as an adjacency list, adding sensors increases cost in proportion to V + E, not V².

## 12. Troubleshooting

| Problem | Likely cause | Fix |
|---|---|---|
| Topology says "Loading the mesh…" for a while after first start | History warm-up takes about 12–14 s. | Wait, then refresh. |
| API cannot connect to the database | Postgres is not healthy yet. | Wait for the health check, or `docker compose down -v && docker compose up --build`. |
| Page not found on refresh (e.g. `/topology`) | nginx SPA fallback not applied. | Rebuild the client container. |
| Migration or column errors after pulling new changes | Old database volume. | `docker compose down -v`, then `up --build`. |
| Docker error "cannot connect to the Docker daemon" or a named-pipe error | Docker Desktop is not running. | Start it and retry. |
| `MSB3277` EF Core version-conflict warnings | Transitive package versions differ (10.0.4 and 10.0.12). | Harmless; the build succeeds. |
| A new service fails with "unable to resolve" | It is not registered in `Program.cs`. | Register it in `Program.cs`. Order matters for hosted services (see the comments there). |
| Spike does not raise a Critical alert | The rolling baseline adapted. | Try another device or allow it to recover. |
| `ci-local.ps1` is blocked | PowerShell execution policy. | `Set-ExecutionPolicy -Scope Process Bypass`, then re-run. |

## 13. Project Structure

```text
PROG7312---POE/
├── .github/
│   ├── workflows/ci.yml
│   └── dependabot.yml
├── scripts/ci-local.ps1
├── docker-compose.yml
├── README.md
├── FEEDBACK_UPDATES.md
├── SmartX.slnx
├── SmartX.Api/
│   ├── Program.cs
│   ├── Domain/
│   │   ├── Collections/     AvlTree, MinHeap, MeshGraph, MeshAlgorithms, RingBuffer
│   │   ├── Entities/
│   │   ├── Models/          MeshAnalysisModels and others
│   │   └── ValueObjects/
│   ├── Infrastructure/      Data, FileStorage, Seeding
│   ├── Services/            MeshService, MeshAnalysisService, MeshHealthMonitor,
│   │                        TelemetryHistoryService, HistoryBenchmark,
│   │                        CommandStreamService, PredictiveActionEngine, ...
│   ├── Endpoints/           MeshEndpoints, HistoryEndpoints, ...
│   ├── Hubs/
│   ├── Middleware/
│   ├── Migrations/
│   └── Dockerfile
├── SmartX.Client/
│   ├── Pages/               Topology.razor and others
│   ├── Components/          MeshMap, RouteView, TelemetryHistoryPanel, BenchmarkCard
│   ├── Services/            TelemetryStore (+ Topology partial)
│   ├── wwwroot/css/
│   ├── nginx.conf
│   └── Dockerfile
└── SmartX.Tests/
    ├── Collections/
    ├── Mesh/
    ├── Services/
    └── Support/
```

**Smart-X | PROG7312 — Programming 3B**

Student: Perez Seth Roy  
Student Number: ST10448618
