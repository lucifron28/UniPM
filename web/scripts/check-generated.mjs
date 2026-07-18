import { execFile } from 'node:child_process'
import { promisify } from 'node:util'

const execFileAsync = promisify(execFile)
const { stdout } = await execFileAsync(
  'git',
  ['status', '--porcelain', '--untracked-files=all', '--', 'src/api/generated'],
  { cwd: new URL('..', import.meta.url) },
)

if (stdout.trim()) {
  throw new Error(
    `Generated API client drift detected:\n${stdout}Run npm run api:generate and commit every generated change.`,
  )
}
