# RAG-Assisted Inspection History Analysis

## Status

- **Status:** Planned capability; not implemented
- **Preferred working title:** UniPM: A Preventive Maintenance System with
  RAG-Assisted Inspection History Analysis for University General Services
- **Title approval:** Pending adviser and panel approval
- **Requirements source:** [GSD Head Interview - Confirmed Project Direction](https://app.notion.com/p/3ae92377e48b81cc8948d7b199ed7d2f)

This planned capability analyzes acknowledged preventive-maintenance inspection
records. It does not represent the complete maintenance lifecycle, including
repairs, parts, costs, labor, downtime, RMRFs, work orders, or corrective-work
execution.

## Planned Analytical Features

1. Recurring-finding analysis.
2. Operational/Non-operational condition-frequency analysis.
3. Time-based comparison and recurrence intervals.
4. Cross-asset pattern analysis.
5. Location, department, and asset-category distribution.
6. Single-asset inspection-history timelines.

The evaluated categories remain fire extinguishers, fire alarm systems,
emergency lights, and water drinking stations.

## Evidence Boundary

The analysis corpus consists of acknowledged preventive-maintenance inspection
records. Draft and Submitted rows are excluded. Legacy inspections without a
form may remain eligible for continuity. The current finding is query context,
not official evidence, until its form is acknowledged.

Signatory names, positions, signatures, signature data, and signature checksums
are excluded from history analysis, retrieval text, embeddings, prompts, and
outputs. OEM retrieval is excluded from the evaluated MVP. UniPM does not
process RMRFs, track corrective work, or integrate directly with the existing
WMS.

## Required Architecture

SQL and deterministic application code calculate the authoritative facts from
the filtered acknowledged records, including counts, denominators, percentages,
recurrence intervals, timelines, and groupings.

RAG retrieves the exact acknowledged records supporting those computed facts.
Optional generation explains only the computed result model and the displayed
source records. Authorized personnel verify and interpret the result.

The language model must not calculate authoritative statistics, diagnose
equipment, infer causes, approve actions, or mutate records.

Every output must include:

- query scope and date range;
- computed facts;
- RAG-assisted interpretation;
- supporting acknowledged source records and locators;
- limitations and explicit no-diagnosis wording.

## Relationship To The Implemented Review Contract

`POST /api/v1/maintenance-review` is the currently implemented, explicitly
enabled, authenticated, source-bounded review/summarization contract. It
retrieves related acknowledged-history evidence for a current finding and may
return an optional source-cited summary.

This planned analysis capability is broader and remains separate. It requires
deterministic fact computation, scope/date-range reporting, source locators,
and analysis-specific output contracts before implementation begins. The
existing maintenance-review endpoint must not be described as implementing
these analytical features.

## Deferred Implementation Decisions

- exact analytical DTOs and query contracts;
- approved date and grouping semantics;
- authorization and audit requirements for analytical outputs;
- institutional procedure source authorization and ingestion;
- stronger masking for arbitrary free-text personal names;
- client presentation and export behavior.
