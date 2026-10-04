using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace UniPM.PmAnalytics.InterpretationEval;

internal static class GitSourceShaVerifier
{
    private static readonly Regex SourceShaPattern = new(
        @"\A[0-9a-f]{40}\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);

    internal static async Task<string> VerifyAsync(
        string repositoryRoot,
        string expectedSourceSha,
        string gitExecutable = "git",
        CancellationToken cancellationToken = default)
    {
        if (!SourceShaPattern.IsMatch(expectedSourceSha))
        {
            throw new GitSourceShaVerificationException("InvalidSourceSha");
        }

        string fullRepositoryRoot;
        try
        {
            fullRepositoryRoot = Path.GetFullPath(repositoryRoot);
        }
        catch (Exception exception) when (exception is ArgumentException
            or IOException
            or NotSupportedException)
        {
            throw new GitSourceShaVerificationException("RepositoryUnavailable");
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = gitExecutable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add("-C");
        process.StartInfo.ArgumentList.Add(fullRepositoryRoot);
        process.StartInfo.ArgumentList.Add("rev-parse");
        process.StartInfo.ArgumentList.Add("--verify");
        process.StartInfo.ArgumentList.Add("HEAD^{commit}");

        try
        {
            if (!process.Start())
            {
                throw new GitSourceShaVerificationException("GitUnavailable");
            }
        }
        catch (Win32Exception)
        {
            throw new GitSourceShaVerificationException("GitUnavailable");
        }
        catch (InvalidOperationException)
        {
            throw new GitSourceShaVerificationException("GitUnavailable");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CommandTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new GitSourceShaVerificationException("GitVerificationTimeout");
        }
        catch
        {
            TryKill(process);
            throw;
        }

        var output = await standardOutput;
        _ = await standardError;
        if (process.ExitCode != 0 || !SourceShaPattern.IsMatch(output.Trim()))
        {
            throw new GitSourceShaVerificationException("SourceRevisionUnavailable");
        }

        var actualSourceSha = output.Trim().ToLowerInvariant();
        if (!string.Equals(actualSourceSha, expectedSourceSha, StringComparison.OrdinalIgnoreCase))
        {
            throw new GitSourceShaVerificationException("SourceShaMismatch");
        }

        return actualSourceSha;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
    }
}

internal sealed class GitSourceShaVerificationException(string code)
    : Exception("The evaluator source revision could not be verified.")
{
    internal string Code { get; } = code;
}
