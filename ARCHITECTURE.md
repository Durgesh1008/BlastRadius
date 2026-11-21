# BlastRadius Engine — Self-Healing Feature Flag & Blast-Radius Control Engine
## Industrial System Architecture Specification

---

## 1. Executive Summary & Problem Space

In modern cloud environments, **code deployment and feature activation are conflated**. Deploying a new software version exposes 100% of production traffic to newly committed logic simultaneously. When critical defects emerge (e.g., memory leaks, database connection pool exhaustion, or third-party payment gateway timeouts), remediation requires:
1. Detecting the degradation via external dashboards (5–15 minutes).
2. Triage and escalation to on-call engineering (5–10 minutes).
3. Initiating rollback PRs, CI/CD pipeline builds, container image pushes, and rolling restarts (15–30 minutes).

Total Mean Time to Recovery (**MTTR**) frequently exceeds **30 to 60 minutes**, causing catastrophic SLA violations, severe financial losses, and brand degradation.

**BlastRadius Engine** decouples deployment from release. It combines:
- **Zero-network-latency evaluation (< 0.5ms)** directly within host application memory via thread-safe lock-free data structures.
- **Deterministic canary rollouts** powered by MurmurHash3 consistent hashing, guaranteeing zero flicker across rollout expansions.
- **Autonomous distributed circuit-breaking** capable of detecting abnormal error rate spikes in real-time sliding windows and automatically isolating/disabling the defect within **50 milliseconds**—with zero human intervention and zero code redeployment.

---

## 2. Distributed Architecture & Component Topology

The system comprises three primary subsystems collaborating across a distributed state mesh:

```mermaid
flowchart TB
    subgraph ControlPlane["Control Plane (BlastRadius.Api)"]
        direction TB
        API["REST & Management APIs\n(ASP.NET Core Web API)"]
        EF["Entity Framework Core 9\n(Neon PostgreSQL)"]
        CBWorker["Automated Circuit Breaker Worker\n(Sliding-Window Telemetry Poller)"]
        DB[("PostgreSQL (Neon)\nFlags, Policies, Audit Logs")]
    end

    subgraph MessagingLayer["Distributed Real-Time Sync & Telemetry"]
        RedisPubSub["Redis Pub/Sub\nChannel: 'flag-updates'"]
        RedisMetrics[("Redis (Upstash TLS)\nSliding-Window Telemetry Hash Buckets")]
    end

    subgraph HostApplications["Application Fleet (Consumers of BlastRadius.Sdk)"]
        subgraph AppNode1["Host App Instance A (e-commerce / Checkout)"]
            SDK1["BlastRadius.Sdk"]
            RAM1[("In-Memory Store\nConcurrentDictionary")]
            EvalEngine1["Rule Evaluator\n(MurmurHash3)"]
            BatchQueue1["Telemetry Queue\n(System.Threading.Channels)"]
        end

        subgraph AppNode2["Host App Instance B"]
            SDK2["BlastRadius.Sdk"]
            RAM2[("In-Memory Store\nConcurrentDictionary")]
            EvalEngine2["Rule Evaluator\n(MurmurHash3)"]
            BatchQueue2["Telemetry Queue\n(System.Threading.Channels)"]
        end
    end

    subgraph ClientUIs["Observability & Control"]
        AngularApp["Angular 19 Control Plane UI\n(Rollout Sliders, Audit Feed, Live Charts)"]
    end

    %% Interactions
    AngularApp -->|HTTPS / REST| API
    API -->|Read / Write with Optimistic Concurrency| EF
    EF --> DB
    API -->|Publish Mutation Event| RedisPubSub
    CBWorker -->|Poll Sliding Error Rates| RedisMetrics
    CBWorker -->|Trigger Automated Kill Switch| API

    RedisPubSub -.->|Instant Invalidation Event| SDK1
    RedisPubSub -.->|Instant Invalidation Event| SDK2

    SDK1 -.->|Bootstrap Pre-Warm HTTP Snapshot| API
    SDK2 -.->|Bootstrap Pre-Warm HTTP Snapshot| API

    BatchQueue1 -->|Async Flushed Batched Counts| RedisMetrics
    BatchQueue2 -->|Async Flushed Batched Counts| RedisMetrics

    SDK1 --- RAM1
    RAM1 --- EvalEngine1
    SDK2 --- RAM2
    RAM2 --- EvalEngine2
```

---

## 3. End-to-End Architectural Workflows

### 3.1. Sub-Millisecond In-Memory Evaluation Flow

Every evaluation in the host application must satisfy strict non-blocking guarantees. Network round-trips are eliminated from the critical path:

```mermaid
sequenceDiagram
    autonumber
    actor Caller as Checkout Controller
    participant SDK as BlastRadiusClient
    participant Store as InMemoryFlagStore (RAM)
    participant Engine as RuleEvaluator
    participant Hash as MurmurHash3
    participant Queue as Channel<TelemetryBatchItem>

    Caller->>SDK: IsEnabled("new-upi-checkout", context, defaultValue: false)
    activate SDK
    SDK->>Store: GetFlag("new-upi-checkout")
    Store-->>SDK: FeatureFlagConfig (from ConcurrentDictionary)
    
    alt Flag not found in cache
        SDK-->>Caller: defaultValue (false) [Reason: DefaultFallback]
    else Flag is disabled (IsEnabled == false)
        SDK-->>Caller: false [Reason: Disabled / Killed]
    else Flag is enabled
        SDK->>Engine: Evaluate(flag, context)
        activate Engine
        
        loop Target Rules by Ascending Priority (1..N)
            Engine->>Engine: Match attribute (e.g., plan == 'Enterprise' or city == 'Bangalore')
            alt Rule matched
                Engine-->>SDK: Result(rule.IsEnabled) [Reason: TargetingRuleMatch]
            end
        end
        
        alt No targeting rule matched (Evaluate Canary Rollout)
            Engine->>Hash: Hash32("new-upi-checkout:user_9482")
            Hash-->>Engine: 32-bit Integer Hash
            Engine->>Engine: bucket = abs(hash) % 100
            alt bucket < flag.RolloutPercentage
                Engine-->>SDK: Result(true) [Reason: PercentageRolloutMatch]
            else
                Engine-->>SDK: Result(false) [Reason: PercentageRolloutMiss]
            end
        end
        deactivate Engine

        SDK-)Queue: TryWrite(flagKey, isError: false) [Non-blocking Channel]
        SDK-->>Caller: EvaluationResult.Value
    end
    deactivate SDK
```

---

### 3.2. Automated 50ms Circuit Breaker Kill Switch Sequence

When faulty logic causes runtime exceptions in the host application, the automated blast-radius circuit breaker detects the anomaly and isolates the feature across all nodes:

```mermaid
sequenceDiagram
    autonumber
    actor User as End Users
    participant App as DemoApp (Host App)
    participant Telemetry as Redis Sliding-Window Telemetry
    participant Worker as CircuitBreakerWorker (Control Plane)
    participant Postgres as Neon PostgreSQL
    participant PubSub as Redis Pub/Sub ('flag-updates')
    participant SdkFleet as BlastRadius.Sdk (Fleet Instances)

    User->>App: POST /api/checkout (15% error spike)
    App->>App: Record exception in catch block
    App-)Telemetry: Flush async error counter to Redis bucket
    
    Note over Worker,Telemetry: Evaluates telemetry every 5 seconds (Sliding Window: 60s)
    Worker->>Telemetry: Query total & error counts for 'new-upi-checkout'
    Telemetry-->>Worker: total = 1000, errors = 150 (ErrorRate = 15.0%)
    
    Note over Worker: Policy Threshold: MaxErrorRate = 2.0%, MinRequests = 50\nBREACH DETECTED!
    
    Worker->>Postgres: UPDATE FeatureFlags SET IsEnabled = false, KillReason = 'Circuit Breaker Error Spike (15.0%)', Version = Version + 1
    Worker->>Postgres: INSERT INTO AuditLogs (FlagKey, Action, TriggeredBy, Details)
    Worker->>PubSub: PUBLISH 'flag-updates' { FlagKey: 'new-upi-checkout', Action: 'CircuitBreakerKilled', Version: 14 }
    
    par Instant Distribution across All Fleet Nodes (< 50ms)
        PubSub-->>SdkFleet: Receive Kill Signal
        SdkFleet->>SdkFleet: In-Memory ConcurrentDictionary["new-upi-checkout"].IsEnabled = false
    end
    
    User->>App: Next User Request
    App->>App: Sdk.IsEnabled returns FALSE (< 0.1ms)
    App->>App: Executes safe fallback path (Legacy Checkout)
    App-->>User: 200 OK (System Self-Healed!)
```

---

## 4. Key Architectural Decisions & Industry Justifications

| Architectural Component | Engineering Decision | Technical Justification |
| :--- | :--- | :--- |
| **Local Cache Storage** | `ConcurrentDictionary<string, FeatureFlagConfig>` | Lock-free reads via memory barriers; eliminates cross-thread contention on multi-core hosts; sub-microsecond retrieval. |
| **Consistent Hashing** | 32-bit x86 `MurmurHash3` | High avalanche characteristics, zero memory allocation, uniform distribution across `[0..99]`. Guarantees non-flicker canaries across scale-ups. |
| **Telemetry Ingestion** | Bounded `System.Threading.Channels` | Evaluation hot path never awaits Redis I/O. Drops or buffers telemetry safely if Redis network partitions occur. |
| **Distributed Notification** | Redis Pub/Sub (`flag-updates`) | Sub-10ms broadcast fanout across hundreds of container nodes. |
| **Cache Resilience** | HTTP Pre-warm + Periodic Polling | Guards against dropped Pub/Sub messages during network partitions or process restarts. Never purges cache on Redis disconnect. |
| **Telemetry Sliding Window** | Redis Time-Bucketed Hashes (`minute_epoch`) | Constant-time $O(1)$ writes and aggregated multi-bucket reads without memory explosion or sorted set overhead. |

---

## 5. Redis Telemetry Key Layout

Telemetry data is organized into rolling 60-second buckets:
```text
blastradius:telemetry:{environment}:{flagKey}:{minuteTimestamp}
  ├── total_evaluations: integer (INCRBY)
  ├── success_count:     integer (INCRBY)
  └── error_count:       integer (INCRBY)
```
Each key is set with an explicit `EXPIRE 300` (5 minutes TTL), eliminating dead metric storage.
