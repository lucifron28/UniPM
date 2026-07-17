#!/usr/bin/env node

import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";

const NOTION_VERSION = "2025-09-03";
const token = required("NOTION_TOKEN");
const dataSourceId = required("NOTION_TASKS_DATA_SOURCE_ID").replace(/^collection:\/\//, "");
const githubToken = required("GITHUB_TOKEN");
const repository = required("GITHUB_REPOSITORY");
const eventName = process.env.GITHUB_EVENT_NAME ?? "workflow_dispatch";
const eventPath = process.env.GITHUB_EVENT_PATH;
const apiUrl = process.env.GITHUB_API_URL ?? "https://api.github.com";
const userMap = parseJson(process.env.NOTION_USER_MAP_JSON, {});
const [owner, repo] = repository.split("/");

const areaMap = {
  "area:academic": "Academic", "area:backend": "Backend", "area:web": "Web",
  "area:mobile": "Mobile", "area:rag": "RAG", "area:testing": "Testing",
  "area:devops": "DevOps", "area:documentation": "Documentation",
};
const priorityMap = {
  "priority:p0": "P0 Critical", "priority:p1": "P1 High",
  "priority:p2": "P2 Medium", "priority:p3": "P3 Low",
};
const typeMap = {
  "type:epic": "Epic", "type:feature": "Feature", "type:bug": "Bug",
  "type:research": "Research", "type:chore": "Chore",
};
const statusMap = {
  "status:backlog": "Backlog", "status:ready": "Ready",
  "status:in-progress": "In Progress", "status:blocked": "Blocked",
  "status:in-review": "In Review", "status:done": "Done",
};
const validSprints = new Set([
  "Sprint 1", "Sprint 2", "Sprint 3", "Finalization",
  "Testing and Evaluation", "Defense Preparation",
]);

main().catch((error) => {
  console.error(error instanceof Error ? error.stack : error);
  process.exitCode = 1;
});

async function main() {
  const payload = eventPath ? JSON.parse(await readFile(eventPath, "utf8")) : {};
  const full = process.env.FULL_RECONCILE === "true" || eventName === "schedule";

  if (full || eventName === "workflow_dispatch") {
    await reconcileIssues();
    await reconcilePullRequests();
  } else if (eventName === "issues") {
    await upsertIssue(payload.issue);
  } else if (["pull_request", "pull_request_target"].includes(eventName)) {
    await syncPullRequest(payload.pull_request);
  }
}

async function reconcileIssues() {
  for (let page = 1; ; page += 1) {
    const items = await github(`/repos/${owner}/${repo}/issues?state=all&per_page=100&page=${page}`);
    for (const issue of items.filter((item) => !item.pull_request)) await upsertIssue(issue);
    if (items.length < 100) break;
  }
}

async function reconcilePullRequests() {
  for (let page = 1; ; page += 1) {
    const items = await github(`/repos/${owner}/${repo}/pulls?state=all&per_page=100&page=${page}`);
    for (const pr of items) await syncPullRequest(pr);
    if (items.length < 100) break;
  }
}

async function syncPullRequest(pr) {
  const numbers = linkedIssueNumbers(pr.body ?? "");
  if (!numbers.length) {
    console.log(`PR #${pr.number}: no closing-keyword issue reference; skipped.`);
    return;
  }

  for (const number of numbers) {
    const issue = await github(`/repos/${owner}/${repo}/issues/${number}`);
    if (issue.pull_request) continue;
    const status = pr.merged_at ? "Done" : pr.state === "open" ? "In Review" : undefined;
    await upsertIssue(issue, { prUrl: pr.html_url, status });
  }
}

async function upsertIssue(issue, overrides = {}) {
  if (!issue?.node_id) throw new Error("Issue payload is missing node_id.");

  const normalized = normalizeIssue(issue, overrides);
  const hash = createHash("sha256").update(JSON.stringify(normalized)).digest("hex");
  const existing = await findByNodeId(issue.node_id);
  const previousHash = existing ? plain(existing.properties?.["Sync Hash"]) : "";

  if (existing && previousHash === hash) {
    console.log(`Issue #${issue.number}: unchanged.`);
    return;
  }

  const properties = notionProperties(normalized, hash, Boolean(existing));
  if (existing) {
    await notion(`/v1/pages/${existing.id}`, { method: "PATCH", body: { properties } });
    console.log(`Issue #${issue.number}: updated ${existing.id}.`);
  } else {
    const page = await notion("/v1/pages", {
      method: "POST",
      body: { parent: { type: "data_source_id", data_source_id: dataSourceId }, properties },
    });
    console.log(`Issue #${issue.number}: created ${page.id}.`);
  }
}

function normalizeIssue(issue, overrides) {
  const labels = (issue.labels ?? [])
    .map((label) => typeof label === "string" ? label : label.name)
    .filter(Boolean)
    .map((label) => label.toLowerCase());

  const status = issue.state === "closed"
    ? "Done"
    : overrides.status ?? mapped(labels, statusMap) ?? "Ready";
  const type = mapped(labels, typeMap)
    ?? (labels.includes("bug") ? "Bug" : labels.includes("enhancement") ? "Feature" : "Chore");
  const sprint = validSprints.has(issue.milestone?.title) ? issue.milestone.title : undefined;
  const assignees = (issue.assignees ?? []).map((a) => userMap[a.login]).filter(Boolean);

  return {
    title: cut(issue.title, 2000),
    status,
    priority: mapped(labels, priorityMap) ?? "P2 Medium",
    area: mapped(labels, areaMap),
    type,
    sprint,
    assignees,
    acceptance: section(issue.body ?? "", ["Acceptance Criteria", "Definition of Done"]),
    dependencies: section(issue.body ?? "", ["Dependencies", "Blocked By"]),
    issueUrl: issue.html_url,
    prUrl: overrides.prUrl,
    nodeId: issue.node_id,
    repository,
    issueNumber: issue.number,
  };
}

function notionProperties(item, hash, updating) {
  const p = {
    Task: title(item.title),
    Status: select(item.status),
    Priority: select(item.priority),
    Type: select(item.type),
    "Adviser Status": select("Not Required"),
    "GitHub Issue": { url: item.issueUrl },
    "GitHub Node ID": rich(item.nodeId),
    Repository: rich(item.repository),
    "Issue Number": { number: item.issueNumber },
    "GitHub Managed": { checkbox: true },
    "Last Synced At": { date: { start: new Date().toISOString() } },
    "Sync Status": select("Synced"),
    "Sync Hash": rich(hash),
  };

  if (item.area) p.Area = select(item.area);
  if (item.sprint) p.Sprint = select(item.sprint);
  if (item.prUrl) p["GitHub PR"] = { url: item.prUrl };
  if (item.acceptance) p["Acceptance Criteria"] = rich(item.acceptance);
  if (item.dependencies) p["Dependency Notes"] = rich(item.dependencies);
  if (item.assignees.length) p.Assignee = { people: item.assignees.map((id) => ({ id })) };

  if (updating) {
    if (!item.area) delete p.Area;
    if (!item.sprint) delete p.Sprint;
    if (!item.prUrl) delete p["GitHub PR"];
  }
  return p;
}

async function findByNodeId(nodeId) {
  const result = await notion(`/v1/data_sources/${dataSourceId}/query`, {
    method: "POST",
    body: {
      page_size: 2,
      filter: { property: "GitHub Node ID", rich_text: { equals: nodeId } },
    },
  });
  if (result.results.length > 1) throw new Error(`Duplicate Notion rows for ${nodeId}.`);
  return result.results[0] ?? null;
}

function linkedIssueNumbers(body) {
  const matches = body.matchAll(/(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+(?:https:\/\/github\.com\/[^/\s]+\/[^/\s]+\/issues\/)?#?(\d+)/gi);
  return [...new Set([...matches].map((m) => Number(m[1])))];
}

function section(body, names) {
  const escaped = names.map((name) => name.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")).join("|");
  const match = body.match(new RegExp(`(?:^|\\n)#{1,6}\\s*(?:${escaped})\\s*\\n([\\s\\S]*?)(?=\\n#{1,6}\\s|$)`, "i"));
  return match ? cut(match[1].trim(), 2000) : undefined;
}

function mapped(labels, map) {
  for (const label of labels) if (map[label]) return map[label];
  return undefined;
}

async function notion(path, options = {}) {
  return request(`https://api.notion.com${path}`, {
    method: options.method ?? "GET",
    headers: {
      Authorization: `Bearer ${token}`,
      "Notion-Version": NOTION_VERSION,
      "Content-Type": "application/json",
    },
    body: options.body ? JSON.stringify(options.body) : undefined,
  });
}

async function github(path) {
  return request(`${apiUrl}${path}`, {
    headers: {
      Authorization: `Bearer ${githubToken}`,
      Accept: "application/vnd.github+json",
      "X-GitHub-Api-Version": "2022-11-28",
      "User-Agent": "unipm-notion-sync",
    },
  });
}

async function request(url, init, maxAttempts = 5) {
  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    const response = await fetch(url, init);
    const text = await response.text();
    const body = text ? JSON.parse(text) : null;
    if (response.ok) return body;
    if (![409, 429, 500, 502, 503, 504].includes(response.status) || attempt === maxAttempts) {
      throw new Error(`${init.method ?? "GET"} ${url} failed (${response.status}): ${text}`);
    }
    const retryAfter = Number(response.headers.get("retry-after"));
    const delay = Number.isFinite(retryAfter) ? retryAfter * 1000 : Math.min(1000 * 2 ** (attempt - 1), 15000);
    await new Promise((resolve) => setTimeout(resolve, delay));
  }
}

function title(value) { return { title: [{ type: "text", text: { content: value } }] }; }
function rich(value) { return { rich_text: [{ type: "text", text: { content: String(value) } }] }; }
function select(name) { return { select: { name } }; }
function plain(property) { return (property?.rich_text ?? []).map((x) => x.plain_text ?? "").join(""); }
function cut(value, max) { return value?.length > max ? `${value.slice(0, max - 1)}…` : value ?? ""; }
function parseJson(value, fallback) { return value ? JSON.parse(value) : fallback; }
function required(name) { const value = process.env[name]; if (!value) throw new Error(`Missing ${name}`); return value; }
