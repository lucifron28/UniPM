// Windows-only local verification. Never connects to Azure or an existing application database.
import assert from 'node:assert/strict'
import { spawn, spawnSync } from 'node:child_process'
import { randomBytes, randomUUID, createHash } from 'node:crypto'
import fs from 'node:fs/promises'
import https from 'node:https'
import net from 'node:net'
import path from 'node:path'
import { createRequire } from 'node:module'
import { fileURLToPath } from 'node:url'

assert.equal(process.platform, 'win32', 'This smoke procedure requires native Windows SQL Server.')
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const zip = path.resolve(process.argv[2] ?? '')
assert.ok(zip.startsWith(path.join(root, 'artifacts', 'azure-staging') + path.sep))
const manifest = JSON.parse(await fs.readFile(path.join(path.dirname(zip), 'provenance.json'), 'utf8'))
const hash = createHash('sha256').update(await fs.readFile(zip)).digest('hex')
assert.equal(hash, manifest.sha256, 'ZIP checksum must match its provenance manifest.')
const ownedRoot = path.join(root, 'artifacts', 'azure-staging', `smoke-${randomUUID()}`)
const extracted = path.join(ownedRoot, 'app')
const certificate = path.join(ownedRoot, 'local-only.pfx')
const database = `UniPM_AzurePackageSmoke_${randomUUID().replaceAll('-', '')}`
const sqlcmd = process.env.UNIPM_LOCAL_SQLCMD ??
  'C:\\Program Files\\Microsoft SQL Server\\Client SDK\\ODBC\\170\\Tools\\Binn\\SQLCMD.EXE'
const require = createRequire(path.join(root, 'web', 'package.json'))
const { chromium } = require('@playwright/test')
const checks = []
let app
let browser
let databaseCreated = false
await fs.mkdir(ownedRoot, { recursive: true })

function sql(query) {
  const result = spawnSync(sqlcmd, ['-S', '.', '-E', '-N', '-C', '-b', '-Q', query], {
    encoding: 'utf8', timeout: 120000, windowsHide: true,
    env: { ...process.env, SQLCMDINI: '' },
  })
  assert.equal(result.status, 0, 'Owned local SQL operation failed; no Azure connection was attempted.')
  return result.stdout
}

async function command(args, env) {
  const child = spawn('dotnet', args, { cwd: extracted, env, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] })
  // Do not persist provider output, credentials, or session responses.
  child.stdout.resume()
  child.stderr.resume()
  await new Promise((resolve, reject) => {
    const timeout = setTimeout(() => {
      child.kill()
      reject(new Error('Owned local maintenance command timed out.'))
    }, 180000)
    child.once('error', error => { clearTimeout(timeout); reject(error) })
    child.once('exit', code => {
      clearTimeout(timeout)
      code === 0 ? resolve() : reject(new Error(`Owned maintenance command exited ${code}.`))
    })
  })
}

try {
  const secret = randomBytes(32).toString('base64url')
  const setup = spawnSync('pwsh', ['-NoProfile', '-Command', `
    $ErrorActionPreference='Stop'
    Expand-Archive -LiteralPath $env:UNIPM_SMOKE_ZIP -DestinationPath $env:UNIPM_SMOKE_APP
    $rsa=[Security.Cryptography.RSA]::Create(2048)
    try {
      $request=[Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=unipm-package.test',$rsa,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
      $cert=$request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddHours(1))
      try { [IO.File]::WriteAllBytes($env:UNIPM_SMOKE_CERT,$cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx,$env:UNIPM_SMOKE_CERT_PASSWORD)) }
      finally { $cert.Dispose() }
    } finally { $rsa.Dispose() }
  `], { encoding: 'utf8', windowsHide: true, timeout: 60000, env: {
    ...process.env, UNIPM_SMOKE_ZIP: zip, UNIPM_SMOKE_APP: extracted,
    UNIPM_SMOKE_CERT: certificate, UNIPM_SMOKE_CERT_PASSWORD: secret,
  } })
  assert.equal(setup.status, 0, 'ZIP extraction or ephemeral certificate setup failed.')
  assert.match(sql("SET NOCOUNT ON; SELECT CONVERT(varchar,SERVERPROPERTY('ProductMajorVersion')) + ':' + CONVERT(varchar,SERVERPROPERTY('IsFullTextInstalled'));"), /15:1/)
  sql(`CREATE DATABASE [${database}];`)
  databaseCreated = true
  sql(`ALTER DATABASE [${database}] SET COMPATIBILITY_LEVEL=150;`)
  const password = `Aa9!${randomBytes(24).toString('hex')}`
  const env = {
    ...process.env,
    ConnectionStrings__DefaultConnection: `Server=.;Database=${database};Integrated Security=True;Encrypt=True;TrustServerCertificate=True`,
    Embeddings__Enabled: 'false', NaturalLanguageAnalytics__Enabled: 'false',
    Observability__MetricsEnabled: 'false', PreventiveMaintenanceScheduleGeneration__WorkerEnabled: 'false',
    InspectionPhotoEvidence__StorageProvider: 'FileSystem',
    ASPNETCORE_ENVIRONMENT: 'Production', DOTNET_ENVIRONMENT: 'Production',
  }
  await command(['UniPM.Api.dll', '--migrate-database'], env)
  checks.push('ZIP maintenance command applied the migration chain to an owned SQL Server 2019 database at compatibility 150.')
  const seedEnv = { ...env, ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development',
    UNIPM_DEV_USER_PASSWORD: password, UNIPM_DEV_SKIP_DEPARTMENT_HEAD_ACCOUNT: 'true' }
  await command(['UniPM.Api.dll', '--seed-development-users'], seedEnv)
  await command(['UniPM.Api.dll', '--seed-demo'], seedEnv)
  const listener = net.createServer()
  await new Promise(resolve => listener.listen(0, '127.0.0.1', resolve))
  const port = listener.address().port
  await new Promise(resolve => listener.close(resolve))
  const origin = `https://unipm-package.test:${port}`
  Object.assign(env, {
    ASPNETCORE_URLS: `https://127.0.0.1:${port}`, UNIPM_WEB_ORIGIN: origin,
    UNIPM_JWT_ISSUER: 'UniPM.PackageSmoke', UNIPM_JWT_AUDIENCE: 'UniPM.PackageSmoke.Clients',
    UNIPM_JWT_SIGNING_KEY: randomBytes(48).toString('base64'),
    Kestrel__Certificates__Default__Path: certificate, Kestrel__Certificates__Default__Password: secret,
    Logging__LogLevel__Default: 'Warning',
  })
  app = spawn('dotnet', ['UniPM.Api.dll'], { cwd: extracted, env, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] })
  app.stdout.resume()
  app.stderr.resume()
  app.on('error', () => {})
  async function request(urlPath, { method = 'GET', body, token, cookie } = {}) {
    const headers = { Origin: origin }
    if (token) headers.Authorization = `Bearer ${token}`
    if (cookie) headers.Cookie = cookie
    const data = body === undefined ? undefined : JSON.stringify(body)
    if (data) headers['Content-Type'] = 'application/json'
    return await new Promise((resolve, reject) => {
      const req = https.request(`${origin}${urlPath}`, { method, headers, rejectUnauthorized: false,
        // Only this local request uses a synthetic hostname and ephemeral certificate.
        lookup: (_host, options, callback) => options.all
          ? callback(null, [{ address: '127.0.0.1', family: 4 }])
          : callback(null, '127.0.0.1', 4), timeout: 10000,
      }, response => {
        const chunks = []
        response.on('data', chunk => chunks.push(chunk))
        response.on('end', () => resolve({ status: response.statusCode, headers: response.headers,
          text: Buffer.concat(chunks).toString('utf8') }))
      })
      req.on('error', reject)
      req.on('timeout', () => req.destroy(new Error('Local HTTPS request timed out.')))
      req.end(data)
    })
  }
  const deadline = Date.now() + 60000
  while (true) {
    try { assert.equal((await request('/health/live')).status, 200); break }
    catch (error) {
      if (app.exitCode !== null || Date.now() > deadline) throw error
      // Condition-based startup polling, not a delay to hide a failed assertion.
      await new Promise(resolve => setTimeout(resolve, 250))
    }
  }
  for (const route of ['/', '/assets', '/reports/overview']) {
    const response = await request(route)
    assert.equal(response.status, 200)
    assert.match(response.headers['content-type'], /text\/html/)
    assert.match(response.headers['cache-control'], /no-store/)
  }
  assert.equal((await request('/api/info')).status, 200)
  assert.equal((await request('/health/ready')).status, 200)
  for (const route of ['/api/v1/not-a-route', '/assets/missing.js', '/openapi/v1.json']) {
    const response = await request(route)
    assert.equal(response.status, 404)
    assert.doesNotMatch(response.text, /<!doctype html/i)
  }
  const html = (await request('/')).text
  const asset = html.match(/src="(\/assets\/[^\"]+\.js)"/)?.[1]
  assert.ok(asset, 'Expected a Vite script in the packaged HTML.')
  const javascript = await request(asset)
  assert.equal(javascript.status, 200)
  assert.match(javascript.headers['cache-control'], /immutable/)
  assert.doesNotMatch(javascript.text, /(?:localhost|127\.0\.0\.1):5000/)
  checks.push('Production ZIP serves React root/nested routes, immutable assets, API info, health, and non-HTML API 404s; OpenAPI is not public.')
  assert.equal((await request('/api/v1/schedules/')).status, 401)
  for (const role of ['gsd', 'inspector', 'supervisor', 'admin']) {
    const login = await request('/api/v1/auth/login', { method: 'POST', body: { email: `${role}@unipm.local`, password } })
    assert.equal(login.status, 200, `${role} login`)
    const cookieHeader = login.headers['set-cookie']?.find(value => value.startsWith('unipm_refresh='))
    assert.ok(cookieHeader)
    assert.match(cookieHeader, /; secure/i)
    assert.match(cookieHeader, /; httponly/i)
    const cookie = cookieHeader.split(';')[0]
    const token = JSON.parse(login.text).accessToken
    assert.ok(token)
    assert.equal((await request('/api/v1/auth/me', { token })).status, 200)
    assert.equal((await request('/api/v1/schedules/', { token })).status, role === 'admin' ? 403 : 200)
    const refresh = await request('/api/v1/auth/refresh', { method: 'POST', cookie })
    assert.equal(refresh.status, 200)
    const rotated = refresh.headers['set-cookie'].find(value => value.startsWith('unipm_refresh=')).split(';')[0]
    assert.equal((await request('/api/v1/auth/logout', { method: 'POST', cookie: rotated })).status, 204)
    assert.equal((await request('/api/v1/auth/refresh', { method: 'POST', cookie: rotated })).status, 401)
  }
  checks.push('All four demo roles log in; Secure/HttpOnly refresh rotates and logout revokes it; anonymous schedules are 401, Admin 403, operational roles 200.')
  browser = await chromium.launch({ args: ['--host-resolver-rules=MAP unipm-package.test 127.0.0.1', '--no-proxy-server'] })
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 900 } })
  const page = await context.newPage()
  const badRequests = []
  page.on('request', req => { if (/https?:\/\/(localhost|127\.0\.0\.1)/i.test(req.url())) badRequests.push(req.url()) })
  await page.goto(`${origin}/`, { waitUntil: 'networkidle' })
  await page.getByRole('button', { name: /sign in/i }).waitFor()
  await page.goto(`${origin}/assets`, { waitUntil: 'networkidle' })
  await page.getByRole('button', { name: /sign in/i }).waitFor()
  assert.equal(badRequests.length, 0)
  checks.push('Chromium loads the ZIP React UI on root and direct nested navigation, with no localhost requests.')
  await fs.writeFile(path.join(ownedRoot, 'result.json'), JSON.stringify({ sourceCommit: manifest.sourceCommit,
    zipSha256: hash, checks, azureVerified: false, storageSubstitute: 'FileSystem', tls: 'ephemeral local certificate only' }, null, 2))
  console.log(JSON.stringify({ result: 'PASS', checks: checks.length, evidence: path.join(ownedRoot, 'result.json') }))
} finally {
  try {
    await browser?.close()
  } finally {
    try {
      if (app?.pid && app.exitCode === null) {
        const stopped = new Promise(resolve => app.once('exit', resolve))
        app.kill()
        await stopped
      }
    } finally {
      try {
        if (databaseCreated) {
          assert.match(database, /^UniPM_AzurePackageSmoke_[a-f0-9]{32}$/)
          sql(`ALTER DATABASE [${database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${database}];`)
        }
      } finally {
        await fs.rm(certificate, { force: true })
      }
    }
  }
}
