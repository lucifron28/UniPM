# Web build and API performance results

## Method

The React package was installed from its lockfile using Node 22.23.5/npm 10.9.9, then its existing unit suite and production build were run. The project scripts are test:run and build; build includes TypeScript typechecking.

For endpoint measurements, a fresh task-specific SQL Server database was migrated and seeded with the repository's synthetic and demo development data. A temporary GSD development account authenticated to the local API. The measurement client performed five warm-ups per endpoint followed by 30 sequential measured GET requests per endpoint, with page=1 and pageSize=25. Requests were not concurrent. Each measurement began immediately before fetch and stopped after the complete HTTP response body was read. Login was a setup step and is excluded from endpoint timings. No response body or access token was retained.

Median is the middle value (or average of the two middle values for this even sample count). p95 uses nearest-rank indexing: sorted sample at ceil(0.95 × n). Maximum is the largest measured value. All results are milliseconds.

## API results

| Endpoint | Route | n | Status | Median | p95 | Maximum | Records in final page / total |
|---|---|---:|---|---:|---:|---:|---:|
| Asset registry | GET /api/v1/assets/registry?page=1&pageSize=25 | 30 | 30 × 200 | 11.477 ms | 25.715 ms | 40.062 ms | 25 / 45 |
| Preventive-maintenance schedules | GET /api/v1/schedules/registry?page=1&pageSize=25 | 30 | 30 × 200 | 14.257 ms | 20.265 ms | 30.365 ms | 25 / 139 |
| Inspection/form review | GET /api/v1/inspections/registry?page=1&pageSize=25 | 30 | 30 × 200 | 19.063 ms | 24.176 ms | 87.070 ms | 25 / 88 |

There were 90 measured requests and zero non-200 responses. Login returned HTTP 200. The counts above are endpoint result counts for this seeded run, not claims about total underlying SQL row counts.

## Environment and timing boundary

- Source commit: 53afe5708ca640b6fe7618589cbf3be1a1de6ae3
- API run start: 2026-11-12T15:56:56.278Z (23:56:56.278 Asia/Manila)
- Target: http://127.0.0.1:5254
- Backend: .NET SDK 10.0.400, ASP.NET Core Release build
- Database: local SQL Server 2019 Developer 15.0.2210.4, compatibility 150, dedicated database UniPM_Nonfunctional_2026_11_12
- Seed output: synthetic seeder reported 20 assets, 34 schedules, and 30 inspections; demo seeder reported 25 assets, 105 schedules, 77 inspections, 42 forms, and 29 acknowledgements
- API benchmark process: Node 24.15.0; Windows platform string win32 10.0.26200 x64; 16 logical CPUs and about 15.4 GiB of RAM
- Data: synthetic development fixtures only; all measured registry reads used the synthetic GSD account

These are local API round-trip timings including response-body transfer to the test client. They do not include React rendering, browser layout/paint, network transit, concurrent-user contention, cold database startup, or production infrastructure. The repeated requests warmed application and database paths. They are not SLA or capacity claims.

## React checks

- npm ci — completed under Node 22.23.5; npm printed 43 dependency advisory counts (30 moderate, 13 high). Advisory details were not triaged in this run.
- npm run test:run — 278 passed across 32 files. Vitest printed jsdom Window.scrollTo not-implemented notices; the tests completed with no failing assertions.
- npm run build — passed, including TypeScript project build and Vite production build.
- ASP.NET Core Release build — passed with zero warnings and zero errors.

No full page-rendering or browser performance measurement was executed.

## Artifacts and reproduction

- [Raw 90-sample CSV](web-performance/api-latency-samples.csv)
- [Benchmark metadata and summary JSON](web-performance/api-latency-summary.json)
- Ignored raw command logs are listed in the [evidence index](evidence-index.md).

Repeat the API measurement with a local SQL Server 2019 instance and .NET SDK 10:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\reference\testing\nonfunctional-2026-11\run-api-performance.ps1 -DatabaseName UniPM_Nonfunctional_2026_11_12 -ResetDatabase -SampleCount 30

The React checks use Node major version 22:

    Set-Location .\web
    npm ci
    npm run test:run
    npm run build
