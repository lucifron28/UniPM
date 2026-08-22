# Manuscript Platform Baseline

Use this repository-controlled wording when updating UniPM manuscript sections.
It reflects the accepted development platform decision and TEST-022 compatibility
evidence. The capstone was evaluated as a local prototype and does not claim an
IIS deployment or production readiness.

## Approved Platform Wording

> UniPM uses SQL Server 2019 as its minimum supported relational database
> platform. SQL Server Full-Text Search supports lexical retrieval. Embedding
> representations are stored as versioned serialized values associated with
> relational document and source metadata, while bounded semantic similarity
> calculations are performed by the ASP.NET Core backend. Docker was used as
> optional development tooling and is not required in the proposed deployment
> architecture.

## System Architecture

```text
React web application / Flutter mobile application
                    |
                  HTTPS
                    |
             ASP.NET Core API
               hosted on IIS
                    |
                 EF Core
                    |
Native Windows SQL Server 2019 + Full-Text Search
```

The web and mobile clients access the backend API only. They do not call SQL
Server, embedding providers, or summary providers directly.

## Database And Retrieval Implementation

- SQL Server 2019 is the minimum supported relational database platform.
- The database requires compatibility level `150` and Full-Text Search.
- SQL Server Full-Text Search provides lexical retrieval over the rebuildable
  maintenance-search projection.
- Embeddings are versioned serialized values stored with relational document and
  source metadata, not a native SQL vector type.
- The ASP.NET Core backend filters a bounded candidate set and calculates cosine
  similarity in memory.
- Reciprocal Rank Fusion combines lexical and eligible semantic rankings.
- UniPM does not require native SQL Server vector features or a separate vector
  database.
- Approved institutional procedures, forms, checklists, and SOPs may later be
  stored separately from maintenance-history records with revision, lifecycle,
  applicability, locator, checksum, and source-provenance metadata. Their
  retrieval and synthesis behavior remain future work until authorization and
  ingestion are approved. OEM retrieval is excluded from the evaluated MVP.

## Software Requirements And Development Tools

The default local database setup is a native Windows SQL Server 2019 instance
with Database Engine Services and Full-Text Search. Windows Authentication is
the preferred local-development example. Docker may be described as optional
development tooling where that is historically accurate, including the retained
SQL Server 2025 Docker experiment, but not as a required production component.

SQL Server Developer Edition was used for development compatibility verification
only. The production SQL Server edition, capacity, backup policy, and Windows
Server deployment configuration remain institutional deployment decisions.

## Deployment Environment And Limitations

The proposed target deployment is an ASP.NET Core API hosted through IIS with a
native Windows SQL Server 2019 instance and Full-Text Search. Docker is removed
from the target deployment diagram and is not required by the proposed
architecture. IIS deployment readiness, production workload testing,
institutional secret management, final RBAC, and operational workflow approval
remain unverified or deferred.

## Testing Environment

TEST-022 records a successful native Windows SQL Server 2019 development
compatibility run: major version 15, compatibility level 150, Full-Text Search,
migration, synthetic seed, Development-user seed, projection rebuild, Full-Text
catalog/index, `CONTAINSTABLE`, and the SQL-enabled backend suite. It is
development compatibility evidence, not production deployment or real-model
quality evidence.
