# Local performance evidence

Measured 24 October 2026 on application commit `c6eed5a`. These are bounded local read bursts, not a production capacity claim or a sustained-user study. No paid providers, uploads, mutations or payments were exercised by the timed workload.

## Environment and workload

Windows 11 (build 26200), 8 logical processors, 15.7 GiB RAM; Node 24.20.0 / Next.js production Webpack build; .NET SDK 10.0.402 / .NET 10 API; PostgreSQL 18.6; k6 2.2.0. The browser BFF, API, database and load generator shared one host. The API used Development, explicitly labelled demo extraction and Simulated payment modes. A loopback-only guard and demo-mode check prevent this script from targeting the public site.

The fictional database contained 23 reports, 9 cases and 7 vet requests across 7 farms before this run. The selected seeded farmer's first farm had **22 reports, 8 cases and a recorded flock headcount of 780**. This is a tiny dataset. No real farmer records were used.

Each virtual user makes two iterations of three sequential authenticated reads: dashboard, first report page and first case page. Each read is followed by 150 ms think time. Login occurs outside the measured read latency. All virtual users share one isolated demo account; this does not measure different-account behavior. The 60/150/300 read budgets keep the combined run below the 600-per-user/minute admission budget. Redirects are disabled and requests time out after 15 seconds.

| Concurrent virtual users | Successful reads | p50 read latency | p95 read latency | Max | Read counter/s* | Unexpected failures | 429 throttles |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 10 | 60 | 132.6 ms | 925.9 ms | 932.4 ms | 14.39 | 0% | 0% |
| 25 | 150 | 77.9 ms | 344.7 ms | 429.5 ms | 73.82 | 0% | 0% |
| 50 | 300 | 109.7 ms | 305.6 ms | 340.2 ms | 139.80 | 0% | 0% |

*k6's read counter rate divides the count by the entire run duration, including setup and think time (4.17 / 2.03 / 2.15 seconds respectively). It is not a sustained throughput guarantee. The first scenario includes colder application/database paths; the lower later p95 does not prove that more users make the system faster. All 510 authenticated-read checks passed; all three runs exited 0.

Raw aggregate evidence: [10 users](results/load-10.json), [25 users](results/load-25.json), [50 users](results/load-50.json). These files contain aggregate metrics, not credentials or response bodies. An after-run memory snapshot was 228.0 MiB API and 103.1 MiB web; peak CPU/memory was not sampled, so no resource bottleneck is claimed from those values.

## Reproduce safely

Start the isolated demo using [DEMO_GUIDE.md](DEMO_GUIDE.md). Install official k6, set `FARMHAND_LOAD_PASSWORD` privately to the generated local seed password, then from the repository root run:

```powershell
$env:FARMHAND_LOAD_VUS = '10' # repeat separately for 25 and 50
$env:FARMHAND_LOAD_RESULT = 'load-10.json'
k6 run scripts/load-readonly.js
```

The default target is `http://127.0.0.1:3000`; only explicit loopback HTTP hosts/ports are accepted. Respect rate limits between repeated runs. Keep the private password out of source, shell history and output. Do not run load against the public deployment. A separate local API restart isolated this benchmark's process-local limiter counters from subsequent browser tests.

## Query improvements and limits

Farm summary counts now execute PostgreSQL aggregates in a batch instead of loading every animal/case and repeating four queries per farm. Legacy case paging is database-side with stable ordering and overflow-safe offsets. Animal details fetch only the newest 30 observations. Financial totals are grouped in SQL. Dashboard active requests are filtered before display limits; pending treatment reminders are joined directly to the farm, and verified payments no longer generate a false payment prompt. Assigned vet case search no longer truncates at 100 requests. Database regressions cover these correctness boundaries.

No baseline benchmark or representative query plan was collected; no percentage speedup is claimed. Large animal lists, all open cases/active requests and recent-observation aggregation can still grow. Existing indexes were retained; broad speculative indexes were not added without workload evidence. Long AI/speech calls remain synchronous. Vet notification dispatch now has a five-second total budget, but a durable notification queue is future work.

Next-stage verification needs larger synthetic farms, sustained diverse-account load, slow-provider and mixed-write tests, resource/query-plan sampling, managed database backup drills and an explicit latency/error SLO. Multiple web replicas need shared refresh coordination, distributed rate limiting and a private shared upload store. See [ARCHITECTURE.md](ARCHITECTURE.md).
