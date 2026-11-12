import { appendFile, mkdir, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import { performance } from "node:perf_hooks";

const baseUrl = process.env.UNIPM_API_BASE_URL;
const email = process.env.UNIPM_TEST_EMAIL;
const password = process.env.UNIPM_TEST_PASSWORD;
const sampleCount = Number.parseInt(process.env.UNIPM_SAMPLE_COUNT ?? "30", 10);
const warmupCount = 5;
const outputDirectory = process.env.UNIPM_OUTPUT_DIRECTORY;

if (!baseUrl || !email || !password || !outputDirectory || !Number.isInteger(sampleCount) || sampleCount < 20) {
  throw new Error("Set local API test variables and use a sample count of at least 20.");
}

const origin = "http://localhost:5173";
const endpoints = [
  { name: "asset-registry", path: "/api/v1/assets/registry?page=1&pageSize=25" },
  { name: "preventive-maintenance-scheduling", path: "/api/v1/schedules/registry?page=1&pageSize=25" },
  { name: "inspection-form-review", path: "/api/v1/inspections/registry?page=1&pageSize=25" },
];

await mkdir(outputDirectory, { recursive: true });
const csvPath = path.join(outputDirectory, "api-latency-samples.csv");
await writeFile(csvPath, "endpoint,sample,status,elapsed_ms,response_bytes,item_count\n", "utf8");

async function request(url, options = {}) {
  const startedAt = performance.now();
  const response = await fetch(url, { ...options, cache: "no-store" });
  const body = await response.text();
  const elapsedMs = performance.now() - startedAt;
  let parsed;
  try { parsed = JSON.parse(body); } catch { parsed = undefined; }
  return {
    status: response.status,
    elapsedMs,
    responseBytes: Buffer.byteLength(body, "utf8"),
    itemCount: Array.isArray(parsed?.items) ? parsed.items.length : null,
    totalCount: Number.isInteger(parsed?.totalCount) ? parsed.totalCount : null,
  };
}

const loginResponse = await fetch(new URL("/api/v1/auth/login", baseUrl), {
  method: "POST",
  headers: { "content-type": "application/json", origin },
  body: JSON.stringify({ email, password }),
});
const tokenJson = await loginResponse.json().catch(() => null);
const accessToken = tokenJson?.accessToken;
if (loginResponse.status !== 200 || typeof accessToken !== "string" || accessToken.length === 0) {
  throw new Error("Synthetic development login failed with HTTP " + loginResponse.status + ".");
}

const samples = [];
const summaries = [];
let requestFailures = 0;
for (const endpoint of endpoints) {
  for (let i = 0; i < warmupCount; i += 1) {
    const result = await request(new URL(endpoint.path, baseUrl), {
      headers: { authorization: "Bearer " + accessToken, origin },
    });
    if (result.status !== 200) {
      throw new Error(endpoint.name + " warm-up returned HTTP " + result.status + ".");
    }
  }

  const endpointSamples = [];
  const statusCounts = {};
  for (let sample = 1; sample <= sampleCount; sample += 1) {
    const result = await request(new URL(endpoint.path, baseUrl), {
      headers: { authorization: "Bearer " + accessToken, origin },
    });
    const elapsedMs = Number(result.elapsedMs.toFixed(3));
    const row = { endpoint: endpoint.name, sample, status: result.status, elapsedMs, ...result };
    samples.push(row);
    endpointSamples.push(elapsedMs);
    statusCounts[result.status] = (statusCounts[result.status] ?? 0) + 1;
    if (result.status !== 200) requestFailures += 1;
    await appendFile(
      csvPath,
      endpoint.name + "," + sample + "," + result.status + "," + elapsedMs.toFixed(3) + "," +
      result.responseBytes + "," + (result.itemCount ?? "") + "\n",
      "utf8",
    );
  }

  const sorted = [...endpointSamples].sort((a, b) => a - b);
  const median = sorted.length % 2 === 0
    ? (sorted[sorted.length / 2 - 1] + sorted[sorted.length / 2]) / 2
    : sorted[Math.floor(sorted.length / 2)];
  const p95 = sorted[Math.ceil(0.95 * sorted.length) - 1];
  const finalSample = samples.at(-1);
  summaries.push({
    endpoint: endpoint.name,
    path: endpoint.path,
    sampleCount,
    warmupCount,
    httpStatusCounts: statusCounts,
    medianMs: Number(median.toFixed(3)),
    p95Ms: Number(p95.toFixed(3)),
    maximumMs: Number(sorted.at(-1).toFixed(3)),
    responseBytesLastSample: finalSample.responseBytes,
    itemsLastSample: finalSample.itemCount,
    totalRecordsLastSample: finalSample.totalCount,
  });
}

const metadata = {
  runStartedAtUtc: new Date().toISOString(),
  target: new URL(baseUrl).origin,
  testType: "authenticated local ASP.NET Core API round-trip; request plus complete response body read",
  measuredEndpoints: endpoints.length,
  sampleCountPerEndpoint: sampleCount,
  warmupCountPerEndpoint: warmupCount,
  execution: "serial requests; one Node.js process; no browser; no UI rendering",
  database: "local SQL Server 2019 Developer, isolated synthetic-data database",
  runtime: {
    node: process.version,
    platform: os.platform() + " " + os.release() + " " + os.arch(),
    cpuLogicalCount: os.cpus().length,
    totalMemoryBytes: os.totalmem(),
  },
  loginHttpStatus: loginResponse.status,
  requestFailures,
  summaries,
};
await writeFile(path.join(outputDirectory, "api-latency-summary.json"), JSON.stringify(metadata, null, 2) + "\n", "utf8");
console.log(JSON.stringify({ sampleCountPerEndpoint: sampleCount, requestFailures, summaries }, null, 2));
if (requestFailures > 0) process.exitCode = 1;
