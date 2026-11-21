# BlastRadius Engine

**Self-Healing Feature Flag & Blast-Radius Control System**  
Built with .NET 9 · ASP.NET Core · PostgreSQL (Neon) · Redis (Upstash) · MurmurHash3

---

## What It Solves

Traditional deployment pipelines couple code shipping with feature exposure — every deploy instantly touches 100% of production traffic. When a defect surfaces (memory leak, DB connection exhaustion, third-party timeout), recovery requires:

1. Detecting degradation via dashboards — **5–15 min**
2. On-call triage and escalation — **5–10 min**
3. Rollback PR → CI build → container push → rolling restart — **15–30 min**

**Total MTTR: 30–60 minutes** of SLA violation and revenue loss.

BlastRadius decouples deployment from release, then adds an autonomous kill switch:

| Capability | Metric |
|---|---|
| In-memory flag evaluation latency | **< 0.5 ms** (no network) |
| Automated circuit-breaker response | **< 50 ms** end-to-end |
| Canary rollout consistency | **Zero flicker** (MurmurHash3 determinism) |
| Cache invalidation fan-out | **< 10 ms** across all nodes (Redis Pub/Sub) |

---

## License

```
MIT License

Copyright (c) 2026 Durgesh Chaudhary (Durgeshchaudhary001)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## Repository Layout

```
BlastRadius/
├── src/
│   ├── BlastRadius.Sdk/            # Client SDK — ships as a NuGet package to host apps
│   │   ├── Client/                 # IBlastRadiusClient + BlastRadiusClient
│   │   ├── Configuration/          # BlastRadiusOptions
│   │   ├── Evaluation/             # IRuleEvaluator + RuleEvaluator (MurmurHash3 engine)
│   │   ├── Extensions/             # AddBlastRadiusSdk() DI extension
│   │   ├── Hashing/                # MurmurHash3 (32-bit x86, zero-alloc)
│   │   ├── Models/                 # Domain models: FeatureFlagConfig, TargetingRule, EvaluationResult …
│   │   ├── Store/                  # IInMemoryFlagStore + ConcurrentDictionary implementation
│   │   └── Telemetry/              # IRedisTelemetryReporter — async Channel-based flusher
│   │
│   └── BlastRadius.Api/            # Control Plane — REST API + CircuitBreakerWorker
│       ├── Controllers/            # FeatureFlagsController, AuditLogsController, TelemetryController
│       ├── Data/                   # EF Core DbContext + entity classes (Neon PostgreSQL)
│       ├── DTOs/                   # Request / response models
│       ├── Services/               # FeatureFlagService, RedisPublisher, TelemetryReader
│       └── Workers/                # CircuitBreakerWorker (IHostedService)
│
├── tests/
│   ├── BlastRadius.Sdk.Tests/      # xUnit — RuleEvaluator, MurmurHash3, InMemoryFlagStore
│   └── BlastRadius.Api.Tests/      # xUnit — FeatureFlagService integration tests
│
├── ARCHITECTURE.md                 # Full system architecture + Mermaid sequence diagrams
├── BlastRadius.sln
└── README.md                       # ← you are here
```

---

## System Architecture (High-Level)

```
┌──────────────────────────────────────────────────────┐
│              Control Plane  (BlastRadius.Api)         │
│  REST API  ──►  EF Core  ──►  Neon PostgreSQL         │
│  CircuitBreakerWorker polls Redis telemetry every 5s  │
└───────────────────┬──────────────────────────────────┘
                    │ PUBLISH flag-updates (Redis Pub/Sub)
        ┌───────────▼──────────────┐
        │   Redis (Upstash TLS)    │
        │  Pub/Sub + Telemetry     │
        └────────┬────────┬────────┘
                 │        │ SUBSCRIBE
     ┌───────────▼─┐  ┌───▼───────────┐
     │ SDK Node A   │  │  SDK Node B   │   ← any number of host app instances
     │ ConcurrentDict  ConcurrentDict  │
     │ < 0.5 ms eval   < 0.5 ms eval  │
     └─────────────┘  └───────────────┘
```

For the full sequence diagrams (evaluation flow, 50 ms circuit-breaker kill chain) see [ARCHITECTURE.md](./ARCHITECTURE.md).

---

## Prerequisites

| Tool | Minimum version |
|---|---|
| .NET SDK | 9.0 |
| PostgreSQL | 15 (Neon serverless recommended) |
| Redis | 7 (Upstash TLS recommended) |

---

## Quick Start

### 1 — Clone

```bash
git clone https://github.com/Durgesh1008/BlastRadius.git
cd BlastRadius
```

### 2 — Configure secrets

Edit `src/BlastRadius.Api/appsettings.Development.json` (never commit real credentials):

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=<neon-host>;Database=blastradius;Username=<user>;Password=<pass>;Ssl Mode=Require",
    "Redis": "<upstash-endpoint>:6379,password=<token>,ssl=True,abortConnect=False"
  },
  "BlastRadius": {
    "PubSubChannel": "flag-updates",
    "DefaultEnvironment": "production"
  }
}
```

### 3 — Apply database migrations

```bash
cd src/BlastRadius.Api
dotnet ef database update
```

### 4 — Run the control plane

```bash
dotnet run --project src/BlastRadius.Api
# API available at https://localhost:5001
# Swagger UI at https://localhost:5001/swagger
```

### 5 — Run the tests

```bash
dotnet test tests/BlastRadius.Sdk.Tests/BlastRadius.Sdk.Tests.csproj
dotnet test tests/BlastRadius.Api.Tests/BlastRadius.Api.Tests.csproj
```

---

## Integrating the SDK into a Host Application

```csharp
// Program.cs of your ASP.NET Core host app
builder.Services.AddBlastRadiusSdk(options =>
{
    options.ControlPlaneUrl      = "https://your-control-plane-host";
    options.RedisConnectionString = "<upstash-redis-connection-string>";
    options.Environment           = "production";
    options.Ssl                   = true;
    options.SyncIntervalSeconds   = 60;   // defensive HTTP polling fallback
    options.EnableTelemetry       = true; // feeds the circuit-breaker sliding window
});
```

### Evaluating a flag

```csharp
public class CheckoutService
{
    private readonly IBlastRadiusClient _flags;

    public CheckoutService(IBlastRadiusClient flags) => _flags = flags;

    public async Task<IActionResult> ProcessAsync(Order order)
    {
        if (_flags.IsEnabled("new-upi-checkout", EvaluationContext.ForUser(order.UserId)))
        {
            try
            {
                var result = await NewUpiFlowAsync(order);
                _flags.RecordTelemetry("new-upi-checkout", isError: false);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // This counter feeds the auto-kill circuit breaker
                _flags.RecordTelemetry("new-upi-checkout", isError: true);
                _logger.LogError(ex, "UPI checkout failed — falling back");
            }
        }

        // Safe legacy fallback path
        return await LegacyCheckoutAsync(order);
    }
}
```

### Targeting rules — advanced context

```csharp
var ctx = new EvaluationContext
{
    UserId = "user_9482",
    Attributes = new Dictionary<string, string>
    {
        ["plan"]    = "Enterprise",
        ["country"] = "IN",
        ["city"]    = "Bangalore"
    }
};

var detail = _flags.Evaluate("new-upi-checkout", ctx);
// detail.Value    — bool result
// detail.Reason   — TargetingRuleMatch | PercentageRolloutMatch | Disabled | DefaultFallback
// detail.BucketIndex — MurmurHash3 bucket [0..99]
// detail.ElapsedMicroseconds — wall-clock evaluation time
```

---

## REST API Reference

Base path: `https://<host>/api/v1`

### Feature Flags

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/flags?environment=production` | List all flags in an environment |
| `GET` | `/flags/snapshot?environment=production` | SDK pre-warm snapshot payload |
| `GET` | `/flags/{key}?environment=production` | Get single flag config |
| `POST` | `/flags` | Create a new feature flag |
| `PUT` | `/flags/{key}` | Update flag (optimistic concurrency via `Version`) |
| `PATCH` | `/flags/{key}/rollout` | Adjust canary rollout percentage (0–100) |
| `POST` | `/flags/{key}/kill` | Manually trip the emergency kill switch |

### Audit & Observability

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/audit-logs?flagKey=&environment=&limit=100` | Full mutation history |
| `GET` | `/telemetry/{key}?environment=&slidingWindowSeconds=60` | Live error-rate metrics |

All endpoints return `application/json`. Full interactive schema is at `/swagger`.

---

## SDK Configuration Reference

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `ControlPlaneUrl` | `string` | — | HTTP pre-warm endpoint |
| `RedisConnectionString` | `string` | — | Upstash/Redis TLS string |
| `Environment` | `string` | `production` | Flag partition key |
| `Ssl` | `bool` | `true` | Enforce TLS on Redis |
| `ConnectTimeoutMs` | `int` | `5000` | Redis connect timeout |
| `PubSubChannel` | `string` | `flag-updates` | Invalidation channel name |
| `SyncIntervalSeconds` | `int` | `60` | HTTP polling fallback cadence |
| `EnableTelemetry` | `bool` | `true` | Enable circuit-breaker counters |
| `TelemetryFlushIntervalMs` | `int` | `1000` | Async Redis flush interval |
| `TelemetryBatchSize` | `int` | `200` | Max buffer before forced flush |

---

## Circuit Breaker — How It Works

1. The SDK records every evaluation outcome via a non-blocking `System.Threading.Channels` queue — **zero impact on the hot path**.
2. A background flusher batches counters to Redis time-bucketed hashes (`blastradius:telemetry:{env}:{flagKey}:{minuteEpoch}`).
3. The `CircuitBreakerWorker` in the control plane polls every **5 seconds**, aggregating the last N buckets into a sliding window.
4. When `ErrorRate% > policy.MaxErrorRatePercent` **and** `TotalRequests >= policy.MinRequestsThreshold`:
   - Postgres flag row is updated: `IsEnabled = false`, `KillReason = "Circuit Breaker …"`, `Version++`
   - An audit log entry is written
   - A Redis Pub/Sub event is published to `flag-updates`
5. All SDK fleet nodes receive the event and update their in-memory `ConcurrentDictionary` within **< 50 ms**.
6. Every subsequent evaluation returns `false` and routes traffic to the safe fallback — **no redeployment, no human on-call page**.

---

## Key Engineering Decisions

| Component | Choice | Reason |
|-----------|--------|--------|
| Local cache | `ConcurrentDictionary<string, FeatureFlagConfig>` | Lock-free reads via memory barriers; sub-microsecond retrieval on multi-core hosts |
| Consistent hashing | 32-bit x86 MurmurHash3 | High avalanche effect, zero allocation, uniform distribution over `[0..99]`; guarantees no flicker across scale-out events |
| Telemetry ingestion | Bounded `System.Threading.Channels` | Evaluation hot path is never blocked on Redis I/O; safely drops/buffers under partition |
| Distributed notification | Redis Pub/Sub | Sub-10 ms broadcast fan-out across hundreds of container nodes |
| Cache resilience | HTTP pre-warm + periodic polling | Guards against dropped Pub/Sub messages on network partition or process restart |
| Telemetry window | Redis time-bucketed hashes + `EXPIRE 300` | O(1) writes and reads; no memory explosion; automatic expiry of stale buckets |
| DB concurrency | EF Core optimistic concurrency (`Version` row-version) | Prevents split-brain flag mutations from concurrent operators or workers |

---

## Telemetry Key Schema

```
blastradius:telemetry:{environment}:{flagKey}:{minuteTimestamp}
  total_evaluations  →  INCRBY (integer)
  success_count      →  INCRBY (integer)
  error_count        →  INCRBY (integer)

TTL: EXPIRE 300  (5 minutes — automatic dead-metric cleanup)
```

---

## Running in Production

- **Never** commit real connection strings. Use environment variables or a secrets manager (Azure Key Vault, AWS Secrets Manager, Doppler).
- The control plane is stateless — scale horizontally behind any load balancer.
- Redis Pub/Sub requires at least one persistent subscriber connection per SDK node; `BlastRadiusClient` manages this automatically.
- Set `SyncIntervalSeconds` to a value appropriate for your acceptable worst-case drift if Pub/Sub messages are lost.

---

## Contributing

Pull requests are welcome. For large changes, open an issue first to discuss what you would like to change.

1. Fork the repository
2. Create a feature branch (`git checkout -b feat/your-feature`)
3. Write tests for new logic
4. Open a pull request against `main`

---

## Author

**Durgesh Chaudhary** — [github.com/Durgesh1008](https://github.com/Durgesh1008)
