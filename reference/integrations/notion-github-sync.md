# GitHub-to-Notion task synchronization

## Purpose

GitHub Issues and pull requests are the source of truth for software implementation. The UniPM Notion Tasks database is the combined capstone dashboard for development, manuscript, literature, adviser, and academic work.

The integration is intentionally one-way:

```text
GitHub Issues and pull requests -> GitHub Actions -> Notion Tasks
```

Notion-only tasks are not modified because synchronized rows are identified by an immutable `GitHub Node ID` and marked `GitHub Managed`.

## Required repository secrets

Create these in **Repository settings -> Secrets and variables -> Actions**:

| Secret | Value |
|---|---|
| `NOTION_TOKEN` | Internal Notion integration token with read, insert, and update access to the UniPM Tasks data source |
| `NOTION_TASKS_DATA_SOURCE_ID` | `9f5810d4-e75f-494b-bf02-91e4fe0d5b80` |
| `NOTION_USER_MAP_JSON` | Optional JSON map from GitHub login to Notion user ID, such as `{"lucifron28":"notion-user-uuid"}` |

Share the UniPM Tasks database with the internal Notion integration before running the workflow. Never commit tokens or real credentials.

## Label contract

Use one label from each relevant family. Missing labels are allowed and receive safe defaults.

### Area

- `area:backend`
- `area:web`
- `area:mobile`
- `area:rag`
- `area:testing`
- `area:devops`
- `area:documentation`
- `area:academic`

### Priority

- `priority:p0`
- `priority:p1`
- `priority:p2`
- `priority:p3`

Issues without a priority label default to `P2 Medium`.

### Type

- `type:epic`
- `type:feature`
- `type:bug`
- `type:research`
- `type:chore`

The standard `bug` and `enhancement` labels are also recognized.

### Status

- `status:backlog`
- `status:ready`
- `status:in-progress`
- `status:blocked`
- `status:in-review`
- `status:done`

A closed issue always maps to `Done`. An open issue without a status label maps to `Ready`.

## Sprint mapping

A GitHub milestone maps to the Notion `Sprint` field only when its title exactly matches:

- Sprint 1
- Sprint 2
- Sprint 3
- Finalization
- Testing and Evaluation
- Defense Preparation

## Pull-request behavior

Pull requests do not create separate Notion tasks. They update linked issue tasks when the PR body uses a closing keyword:

```text
Closes #42
Fixes #42
Resolves #42
```

An open linked pull request moves the issue task to `In Review`. A merged pull request moves it to `Done`. A pull request with no closing-keyword reference is skipped.

## Issue-body sections

The workflow copies these optional sections into Notion:

```markdown
## Acceptance Criteria
- ...

## Dependencies
- ...
```

`Definition of Done` is also accepted for acceptance criteria, and `Blocked By` is accepted for dependency notes.

## Triggers

The workflow runs on:

- issue creation and relevant issue changes;
- pull-request events through `pull_request_target`, allowing the trusted base workflow to access the Notion secret without running untrusted PR code;
- manual dispatch;
- daily reconciliation at 17:27 UTC.

Manual and scheduled runs reconcile all repository issues and linked pull requests. Event runs update only affected records.

## Failure and recovery

- Notion requests retry HTTP 409, 429, and transient 5xx responses.
- `Retry-After` is respected when supplied.
- `Sync Hash` prevents unchanged records from being rewritten.
- Manual full reconciliation repairs missed events.
- Duplicate Notion rows with the same GitHub Node ID cause a safe failure.

## Initial activation

1. Create a Notion internal integration.
2. Share the UniPM Tasks database with it.
3. Add the required GitHub Actions secrets.
4. Merge the integration pull request.
5. Run **Sync GitHub work to Notion** manually with `full_reconcile=true`.
6. Review created tasks before relying on daily synchronization.
